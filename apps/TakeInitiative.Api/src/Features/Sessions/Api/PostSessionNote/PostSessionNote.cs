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
    /// <summary>The note's text, or its images' caption. It may be empty when <see cref="ImageIds"/> has an image.</summary>
    public required string Text { get; init; }
    public required Visibility Visibility { get; init; }
    public required bool IsRecap { get; init; }
    /// <summary>Entries to create with the note, each already mentioned in <see cref="Text"/> (see <see cref="NewEntries"/>).</summary>
    public NewEntryRequest[]? NewEntries { get; init; }
    /// <summary>
    /// Images to put on the note, in order, at most 10 (step 16b): ids from
    /// <c>POST images</c>, uploaded by the caller and on no note yet.
    /// </summary>
    public Guid[]? ImageIds { get; init; }
}

public class PostSessionNoteRequestValidator : Validator<PostSessionNoteRequest>
{
    public PostSessionNoteRequestValidator()
    {
        RuleFor(x => x.Text).SessionNoteText(x => x.ImageIds is { Length: > 0 });
        RuleFor(x => x.Visibility).IsInEnum();
        RuleFor(x => x.NewEntries).NewEntriesList();
        RuleFor(x => x.ImageIds).ImageIdsList();
    }
}

public static class SessionNoteTextRule
{
    public const string NeedsTextMessage = "A session note needs some text or an image.";

    /// <summary>
    /// Text is trimmed and at most <see cref="SessionNote.TextMaxLength"/> characters. It is
    /// required unless the note has an image (<paramref name="hasImages"/>), whose caption it is.
    /// </summary>
    public static IRuleBuilderOptions<T, string> SessionNoteText<T>(this IRuleBuilder<T, string> rule, Func<T, bool> hasImages)
        => rule
            .Must((request, text) => text is not null && (hasImages(request) || !string.IsNullOrWhiteSpace(text)))
            .WithMessage(NeedsTextMessage)
            .Must(text => (text ?? "").Trim().Length <= SessionNote.TextMaxLength)
            .WithMessage($"A session note can be at most {SessionNote.TextMaxLength} characters.");
}

/// <summary>
/// Any member posts a session note, to the current session by default. A note posted
/// to an older session is marked added later, decided now and never recomputed.
/// <c>newEntries</c> are created in the same transaction, with the note's visibility, and
/// <c>imageIds</c> are attached in it (<see cref="ImageAttachments"/>).
/// </summary>
public class PostSessionNote(
    IDocumentSession session,
    IHubContext<CampaignHub> hub,
    [FromKeyedServices(SessionGap.ClockKey)] TimeProvider clock) : Endpoint<PostSessionNoteRequest, SessionNoteResponse>
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

        IReadOnlyList<Guid> newEntryIds;
        var noteId = Guid.NewGuid();
        var text = req.Text.Trim();
        await using (var write = await NoteWrite.Begin(session, ct))
        {
            var images = await this.Stage(
                write.Session, req.CampaignId, member, noteId, [], req.ImageIds ?? [], clock.GetUtcNow(), ct);
            await this.SaveImagesAsync(write.Session, ct);

            newEntryIds = await this.AppendNewEntries(
                write.Session, req.CampaignId, member, noteId, text, req.Visibility, req.NewEntries, ct);
            write.Session.Events.StartStream<SessionNote>(noteId, new SessionNotePosted(
                Actor: Actor.Member(member.MemberId),
                CampaignId: req.CampaignId,
                SessionId: target.Id,
                AuthorMemberId: member.MemberId,
                Text: text,
                Visibility: req.Visibility,
                IsRecap: req.IsRecap,
                AddedLater: target.Id != current.Id,
                Images: images.Images));
            await write.Session.SaveChangesAsync(ct);
            await write.CommitAsync(ct);
        }

        var note = (await session.LoadAsync<SessionNote>(noteId, ct))!;
        await hub.NotifySessionNoteUpserted(note);
        await hub.NotifyCreated(session, newEntryIds, ct);
        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }
}
