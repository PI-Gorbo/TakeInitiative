using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record PutSessionNoteRequest
{
    public Guid CampaignId { get; init; }
    public Guid NoteId { get; init; }
    public required string Text { get; init; }
    public required bool IsRecap { get; init; }
    /// <summary>Entries to create with the edit, each already mentioned in <see cref="Text"/> (see <see cref="NewEntries"/>).</summary>
    public NewEntryRequest[]? NewEntries { get; init; }
    /// <summary>
    /// The note's whole, ordered image list (step 16b), at most 10. Leave it out to keep the
    /// images. New ids are attached, and images left out are deleted for real.
    /// </summary>
    public Guid[]? ImageIds { get; init; }
}

public class PutSessionNoteRequestValidator : Validator<PutSessionNoteRequest>
{
    public PutSessionNoteRequestValidator()
    {
        // With imageIds left out the note keeps its images, so the handler checks the text.
        RuleFor(x => x.Text).SessionNoteText(x => x.ImageIds is null || x.ImageIds.Length > 0);
        RuleFor(x => x.NewEntries).NewEntriesList();
        RuleFor(x => x.ImageIds).ImageIdsList();
    }
}

/// <summary>
/// The author edits their note. An unchanged note appends nothing. Last write wins: only
/// the author can edit, so a conflict is the same person on two devices. <c>newEntries</c>
/// are created in the same transaction, with the note's current visibility. Only the author
/// changes the images: removed ones are marked deleted in the same transaction, and their
/// blobs deleted after it (the sweeper retries a failure).
/// </summary>
public class PutSessionNote(
    IDocumentSession session,
    IHubContext<CampaignHub> hub,
    ImageSweeper sweeper,
    [FromKeyedServices(SessionGap.ClockKey)] TimeProvider clock,
    ILogger<PutSessionNote> logger) : Endpoint<PutSessionNoteRequest, SessionNoteResponse>
{
    public override void Configure()
    {
        Put("/api/campaigns/{CampaignId}/notes/{NoteId}");
    }

    public override async Task HandleAsync(PutSessionNoteRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var note = await this.RequireVisibleNote(session, req.CampaignId, req.NoteId, member, ct);
        this.RequireAuthor(note, member);

        var text = req.Text.Trim();
        IReadOnlyList<Guid> newEntryIds;
        ImageAttachments.Staged? images;
        bool edited;
        await using (var write = await NoteWrite.Begin(session, ct))
        {
            images = req.ImageIds is null
                ? null
                : await this.Stage(write.Session, req.CampaignId, member, note.Id, note.Images, req.ImageIds, clock.GetUtcNow(), ct);
            if (text.Length == 0 && (images?.Images ?? note.Images).Length == 0)
            {
                ThrowError(new ValidationFailure("text", SessionNoteTextRule.NeedsTextMessage), StatusCodes.Status400BadRequest);
            }
            var imagesChanged = images is not null && images.Differs(note.Images);
            if (imagesChanged)
            {
                await this.SaveImagesAsync(write.Session, ct);
            }

            newEntryIds = await this.AppendNewEntries(
                write.Session, req.CampaignId, member, note.Id, text, NewEntries.VisibilityFrom(note), req.NewEntries, ct);
            edited = note.Text != text || note.IsRecap != req.IsRecap || imagesChanged;
            if (edited)
            {
                write.Session.Events.Append(note.Id, new SessionNoteEdited(
                    Actor.Member(member.MemberId), text, req.IsRecap, imagesChanged ? images!.Images : null));
            }
            if (edited || newEntryIds.Count > 0)
            {
                await write.Session.SaveChangesAsync(ct);
            }
            await write.CommitAsync(ct);
        }
        await sweeper.PurgeRemoved(images?.Removed ?? [], logger, ct);

        if (edited)
        {
            note = (await session.LoadAsync<SessionNote>(note.Id, ct))!;
            await hub.NotifySessionNoteUpserted(note);
        }
        await hub.NotifyCreated(session, newEntryIds, ct);

        await SendAsync(SessionNoteResponse.From(note), cancellation: ct);
    }
}
