using System.Net;
using FastEndpoints;
using Marten;

namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// The read rule for images (invariant 5), run on every request for an image's bytes,
/// including one that would be answered "not modified". Nothing about who can see an
/// image is stored on it: the rule reads the image's state, and from 16b its note, at
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
    /// a DM, since it is an unsent draft).
    /// </summary>
    public static Task<bool> CanSee(IQuerySession session, Image image, Member viewer, CancellationToken ct)
    {
        if (image.DeletedAt is not null)
        {
            return Task.FromResult(false);
        }
        if (image.NoteId is null)
        {
            return Task.FromResult(image.UploaderMemberId == viewer.MemberId);
        }
        // 16b: load the note by image.NoteId (a missing note is false) and return
        // SessionNoteVisibility.CanSee(note, viewer), the function the stream uses. Until
        // then no image is on a note, so this is never reached.
        return Task.FromResult(false);
    }
}
