using FastEndpoints;
using FluentValidation;
using Marten;
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
}

public class PostSessionNoteRequestValidator : Validator<PostSessionNoteRequest>
{
    public PostSessionNoteRequestValidator()
    {
        RuleFor(x => x.Text).SessionNoteText();
        RuleFor(x => x.Visibility).IsInEnum();
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
/// </summary>
public class PostSessionNote(IDocumentSession session) : Endpoint<PostSessionNoteRequest, SessionNoteResponse>
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
        session.Events.StartStream<SessionNote>(noteId, new SessionNotePosted(
            Actor: Actor.Member(member.MemberId),
            CampaignId: req.CampaignId,
            SessionId: target.Id,
            AuthorMemberId: member.MemberId,
            Text: req.Text.Trim(),
            Visibility: req.Visibility,
            IsRecap: req.IsRecap,
            AddedLater: target.Id != current.Id));
        await session.SaveChangesAsync(ct);

        var note = (await session.LoadAsync<SessionNote>(noteId, ct))!;
        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }
}
