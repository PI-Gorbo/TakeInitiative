using FastEndpoints;
using FluentValidation;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record PostSessionNoteRequest
{
    public Guid CampaignId { get; init; }
    /// <summary>The session to post to. Omit it for the current session.</summary>
    public Guid? SessionId { get; init; }
    public required string Text { get; init; }
    public required Visibility Visibility { get; init; }
    public required bool IsRecap { get; init; }
    /// <summary>Entries to create with the note, each already mentioned in <see cref="Text"/> (see <see cref="NewEntries"/>).</summary>
    public NewEntryRequest[]? NewEntries { get; init; }
}

public class PostSessionNoteRequestValidator : Validator<PostSessionNoteRequest>
{
    public PostSessionNoteRequestValidator()
    {
        RuleFor(x => x.Text).SessionNoteText();
        RuleFor(x => x.Visibility).IsInEnum();
        RuleFor(x => x.NewEntries).NewEntriesList();
    }
}

public static class SessionNoteTextRule
{
    /// <summary>Text is trimmed, required, and at most <see cref="SessionNote.TextMaxLength"/> characters.</summary>
    public static IRuleBuilderOptions<T, string> SessionNoteText<T>(this IRuleBuilder<T, string> rule)
        => rule
            .Must(text => !string.IsNullOrWhiteSpace(text)).WithMessage("A session note needs some text.")
            .Must(text => (text ?? "").Trim().Length <= SessionNote.TextMaxLength)
            .WithMessage($"A session note can be at most {SessionNote.TextMaxLength} characters.");
}

/// <summary>
/// Any member posts a session note, to the current session by default. A note posted
/// to an older session is marked added later, decided now and never recomputed.
/// <c>newEntries</c> are created in the same transaction, with the note's visibility.
/// </summary>
public class PostSessionNote(IDocumentSession session, IHubContext<CampaignHub> hub) : Endpoint<PostSessionNoteRequest, SessionNoteResponse>
{
    public override void Configure()
    {
        Post("/api/campaigns/{CampaignId}/notes");
    }

    public override async Task HandleAsync(PostSessionNoteRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var current = await this.RequireCurrentSession(session, req.CampaignId, ct);
        var target = req.SessionId is { } sessionId
            ? await this.RequireSession(session, req.CampaignId, sessionId, ct)
            : current;

        var noteId = Guid.NewGuid();
        var text = req.Text.Trim();
        var newEntryIds = await this.AppendNewEntries(
            session, req.CampaignId, member, noteId, text, req.Visibility, req.NewEntries, ct);
        session.Events.StartStream<SessionNote>(noteId, new SessionNotePosted(
            Actor: Actor.Member(member.MemberId),
            CampaignId: req.CampaignId,
            SessionId: target.Id,
            AuthorMemberId: member.MemberId,
            Text: text,
            Visibility: req.Visibility,
            IsRecap: req.IsRecap,
            AddedLater: target.Id != current.Id));
        await session.SaveChangesAsync(ct);

        var note = (await session.LoadAsync<SessionNote>(noteId, ct))!;
        await hub.NotifySessionNoteUpserted(note);
        await hub.NotifyCreated(session, newEntryIds, ct);
        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }
}
