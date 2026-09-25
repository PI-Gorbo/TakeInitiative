using System.Net;
using FastEndpoints;
using Marten;

namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// The read rule for images (invariant 5), run on every request for an image's bytes,
/// including one that would be answered "not modified". Nothing about who can see an
/// image is stored on it: the rule reads the image's state and its note at
/// request time, so a change to the note takes effect on the next request.
/// </summary>
public static class ImageAccess
{
    public const string NotFoundMessage = "There is no image with the given id.";

    /// <summary>
    /// An image of this campaign that <paramref name="viewer"/> can see. Anything else,
    /// including another campaign's image id, is a 404 and never a 403, so an image's
    /// existence does not leak.
    /// </summary>
    public static async Task<Image> RequireVisibleImage<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IQuerySession session, Guid campaignId, Guid imageId, Member viewer, CancellationToken ct)
        where TRequest : notnull
    {
        var image = await session.LoadAsync<Image>(imageId, ct);
        if (image is null || image.CampaignId != campaignId || !await CanSee(session, image, viewer, ct))
        {
            endpoint.ThrowError(NotFoundMessage, (int)HttpStatusCode.NotFound);
        }
        return image;
    }

    /// <summary>
    /// Whether <paramref name="viewer"/> can see <paramref name="image"/>, checked in order:
    /// a deleted image is seen by nobody; an image on no note by its uploader only (not even
    /// a DM, since it is an unsent draft); an image on a note by exactly who can see the note,
    /// through <see cref="SessionNoteVisibility.CanSee"/>, the function the stream uses. The
    /// note is read now, so hiding it, changing its visibility or deleting it takes effect on
    /// the next request, with nothing copied onto the image that could drift.
    /// </summary>
    public static async Task<bool> CanSee(IQuerySession session, Image image, Member viewer, CancellationToken ct)
    {
        if (image.DeletedAt is not null)
        {
            return false;
        }
        if (image.NoteId is not { } noteId)
        {
            return image.UploaderMemberId == viewer.MemberId;
        }
        var note = await session.LoadAsync<SessionNote>(noteId, ct);
        return note is not null && note.CampaignId == image.CampaignId && SessionNoteVisibility.CanSee(note, viewer);
    }
}
