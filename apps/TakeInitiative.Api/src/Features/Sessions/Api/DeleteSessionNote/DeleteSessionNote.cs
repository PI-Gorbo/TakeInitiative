using FastEndpoints;
using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Sessions;

public record DeleteSessionNoteRequest
{
    public Guid CampaignId { get; init; }
    public Guid NoteId { get; init; }
}

/// <summary>
/// The author deletes their note. The document goes; the stream keeps every event. Its
/// images are marked deleted in the same transaction and deleted for real after it (the
/// sweeper retries a failure); the note's events keep only their ids.
/// </summary>
public class DeleteSessionNote(
    IDocumentSession session,
    IHubContext<CampaignHub> hub,
    ImageSweeper sweeper,
    [FromKeyedServices(SessionGap.ClockKey)] TimeProvider clock,
    ILogger<DeleteSessionNote> logger) : Endpoint<DeleteSessionNoteRequest>
{
    public override void Configure()
    {
        Delete("/api/campaigns/{CampaignId}/notes/{NoteId}");
        Description(b => b.ClearDefaultProduces(StatusCodes.Status200OK).Produces(StatusCodes.Status204NoContent));
    }

    public override async Task HandleAsync(DeleteSessionNoteRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var note = await this.RequireVisibleNote(session, req.CampaignId, req.NoteId, member, ct);
        this.RequireAuthor(note, member);

        IReadOnlyList<Image> images;
        await using (var write = await NoteWrite.Begin(session, ct))
        {
            images = await ImageAttachments.StageNoteDeleted(write.Session, note.Id, clock.GetUtcNow(), ct);
            await this.SaveImagesAsync(write.Session, ct);
            write.Session.Events.Append(note.Id, new SessionNoteDeleted(Actor.Member(member.MemberId)));
            await write.Session.SaveChangesAsync(ct);
            await write.CommitAsync(ct);
        }
        await hub.NotifySessionNoteRemoved(note);
        await sweeper.PurgeRemoved(images, logger, ct);

        await SendNoContentAsync(ct);
    }
}
