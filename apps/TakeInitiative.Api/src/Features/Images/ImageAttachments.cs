using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Marten.Exceptions;

namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// Putting images on a session note (step 16b), for <c>POST notes</c>, <c>PUT notes/{id}</c>
/// and <c>DELETE notes/{id}</c>. The image documents are changed in the same transaction as
/// the note's event (<see cref="NoteWrite"/>), so the note and its images commit together and
/// share a correlation id.
/// <list type="bullet">
/// <item>More than <see cref="SessionNote.MaxImages"/> ids, or the same id twice, is a 400
/// (<c>errors.imageIds</c>).</item>
/// <item>An id that is not an image of this campaign, uploaded by the caller, not deleted and
/// on no note is a 400 with <see cref="UnavailableMessage"/>, the same message for every case,
/// so another member's image id tells the caller nothing.</item>
/// <item>Two notes racing for one upload (two tabs): the <see cref="Image"/> document's
/// optimistic concurrency fails the loser's save, which <see cref="SaveImagesAsync"/> turns into
/// a 409 with <see cref="ConflictMessage"/>.</item>
/// </list>
/// </summary>
public static class ImageAttachments
{
    public const string ErrorKey = "imageIds";
    public const string UnavailableMessage = "An image could not be attached. Upload it again.";
    public const string ConflictMessage = "An image was attached to another note at the same time. Upload it again.";

    /// <summary>At most <see cref="SessionNote.MaxImages"/>, each id once.</summary>
    public static IRuleBuilderOptions<T, Guid[]?> ImageIdsList<T>(this IRuleBuilder<T, Guid[]?> rule)
        => rule
            .Must(ids => ids is null || ids.Length <= SessionNote.MaxImages)
            .WithMessage($"A session note can have at most {SessionNote.MaxImages} images.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Length)
            .WithMessage("The same image cannot be on a note twice.");

    /// <summary>The result of <see cref="Stage"/>: the note's new list, and the images it no longer has.</summary>
    public record Staged(NoteImage[] Images, IReadOnlyList<Image> Removed)
    {
        /// <summary>Whether the list differs from <paramref name="current"/>, in membership or order.</summary>
        public bool Differs(IReadOnlyList<NoteImage> current)
            => !Images.Select(i => i.ImageId).SequenceEqual(current.Select(i => i.ImageId));
    }

    /// <summary>
    /// Makes <paramref name="imageIds"/> the whole, ordered image list of note
    /// <paramref name="noteId"/>, which has <paramref name="current"/> now (<c>[]</c> for a new
    /// note). New ids are checked and attached, and removed ones are marked deleted. Nothing is
    /// saved: the caller saves through <see cref="SaveImagesAsync"/>, then the note's event, commits
    /// and calls <see cref="PurgeRemoved"/>.
    /// </summary>
    public static async Task<Staged> Stage<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IDocumentSession session, Guid campaignId, Member author,
        Guid noteId, IReadOnlyList<NoteImage> current, IReadOnlyList<Guid> imageIds, DateTimeOffset now, CancellationToken ct)
        where TRequest : notnull
    {
        var kept = current.ToDictionary(i => i.ImageId);
        var added = imageIds.Where(id => !kept.ContainsKey(id)).ToArray();
        var loaded = added.Length == 0
            ? new Dictionary<Guid, Image>()
            : (await LoadAll(session, added, ct)).ToDictionary(i => i.Id);

        var at = Microseconds.Truncate(now);
        var images = new List<NoteImage>(imageIds.Count);
        foreach (var id in imageIds)
        {
            if (kept.TryGetValue(id, out var onNote))
            {
                images.Add(onNote);
                continue;
            }
            if (!loaded.TryGetValue(id, out var image) || !IsAttachable(image, campaignId, author))
            {
                endpoint.ThrowError(new ValidationFailure(ErrorKey, UnavailableMessage), StatusCodes.Status400BadRequest);
            }
            image.NoteId = noteId;
            image.AttachedAt = at;
            session.Update(image);
            images.Add(new NoteImage(image.Id, image.Width, image.Height));
        }

        var removedIds = current.Select(i => i.ImageId).Except(imageIds).ToArray();
        var removed = removedIds.Length == 0 ? [] : await MarkDeleted(session, removedIds, at, ct);
        return new Staged([.. images], removed);
    }

    /// <summary>Marks every image of a note deleted, for the note's delete. The caller saves and purges.</summary>
    public static async Task<IReadOnlyList<Image>> StageNoteDeleted(IDocumentSession session, Guid noteId, DateTimeOffset now, CancellationToken ct)
    {
        var images = await session.Query<Image>()
            .Where(i => i.NoteId == noteId && i.DeletedAt == null)
            .ToListAsync(ct);
        var at = Microseconds.Truncate(now);
        foreach (var image in images)
        {
            image.DeletedAt = at;
            session.Update(image);
        }
        return [.. images];
    }

    /// <summary>
    /// Saves the staged images, in <see cref="NoteWrite"/>'s first batch (before any event is
    /// queued). A stale <see cref="Image"/> (attached, removed or swept by someone else since it
    /// was loaded) is a 409, and the transaction rolls back.
    /// </summary>
    public static async Task SaveImagesAsync<TRequest, TResponse>(
        this Endpoint<TRequest, TResponse> endpoint, IDocumentSession session, CancellationToken ct)
        where TRequest : notnull
    {
        try
        {
            await session.SaveChangesAsync(ct);
        }
        catch (Exception e) when (IsConcurrency(e))
        {
            endpoint.ThrowError(new ValidationFailure(ErrorKey, ConflictMessage), StatusCodes.Status409Conflict);
        }
    }

    /// <summary>
    /// Deletes the blobs and documents of images marked deleted by a save that has committed.
    /// A failure only delays it: the images stay marked and the sweeper retries.
    /// </summary>
    public static async Task PurgeRemoved(this ImageSweeper sweeper, IReadOnlyList<Image> removed, ILogger logger, CancellationToken ct)
    {
        foreach (var image in removed)
        {
            try
            {
                await sweeper.Purge(image, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Could not delete image {ImageId} now; the sweeper retries", image.Id);
            }
        }
    }

    private static bool IsAttachable(Image image, Guid campaignId, Member author)
        => image.CampaignId == campaignId
            && image.UploaderMemberId == author.MemberId
            && image.DeletedAt is null
            && image.NoteId is null;

    private static async Task<IReadOnlyList<Image>> MarkDeleted(IDocumentSession session, Guid[] ids, DateTimeOffset at, CancellationToken ct)
    {
        var images = await LoadAll(session, ids, ct);
        foreach (var image in images.Where(i => i.DeletedAt is null))
        {
            image.DeletedAt = at;
            session.Update(image);
        }
        return [.. images];
    }

    private static async Task<IReadOnlyList<Image>> LoadAll(IDocumentSession session, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var images = new List<Image>();
        foreach (var id in ids)
        {
            if (await session.LoadAsync<Image>(id, ct) is { } image)
            {
                images.Add(image);
            }
        }
        return images;
    }

    private static bool IsConcurrency(Exception e) => e switch
    {
        ConcurrencyException => true,
        AggregateException aggregate => aggregate.InnerExceptions.Any(IsConcurrency),
        _ => false,
    };
}
