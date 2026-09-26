using FastEndpoints;
using Marten;
using Marten.Exceptions;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Images;

public record DeleteImageRequest
{
    public Guid CampaignId { get; init; }
    public Guid ImageId { get; init; }
}

/// <summary>
/// The uploader removes an image that is on no note (removing an attachment in the
/// composer). Once it is on a note, the note is edited instead. The image is marked
/// deleted first, so a blob store failure only delays the delete: the sweeper retries.
/// </summary>
public class DeleteImage(
    IDocumentSession session,
    ImageSweeper sweeper,
    [FromKeyedServices(SessionGap.ClockKey)] TimeProvider clock,
    ILogger<DeleteImage> logger) : Endpoint<DeleteImageRequest>
{
    public const string OnNoteMessage = "This image is on a note. Edit the note to remove it.";

    public override void Configure()
    {
        Delete("/api/campaigns/{CampaignId}/images/{ImageId}");
        Description(b => b.ClearDefaultProduces(StatusCodes.Status200OK).Produces(StatusCodes.Status204NoContent));
    }

    public override async Task HandleAsync(DeleteImageRequest req, CancellationToken ct)
    {
        var userId = this.GetUserIdOrThrowUnauthorized();
        var (_, member) = await this.RequireMember(session, req.CampaignId, userId, ct);
        var image = await this.RequireVisibleImage(session, req.CampaignId, req.ImageId, member, ct);
        if (image.NoteId is not null)
        {
            ThrowError(OnNoteMessage, StatusCodes.Status409Conflict);
        }
        if (image.UploaderMemberId != member.MemberId)
        {
            ThrowError("Only the uploader of an image can remove it.", StatusCodes.Status403Forbidden);
        }

        image.DeletedAt = Microseconds.Truncate(clock.GetUtcNow());
        session.Update(image);
        try
        {
            await session.SaveChangesAsync(ct);
        }
        catch (ConcurrencyException)
        {
            // It was attached to a note (16b) or swept in the meantime.
            ThrowError("This image changed while it was being removed. Try again.", StatusCodes.Status409Conflict);
        }

        try
        {
            await sweeper.Purge(image, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Could not delete image {ImageId} now; the sweeper retries", image.Id);
        }

        await SendNoContentAsync(ct);
    }
}
