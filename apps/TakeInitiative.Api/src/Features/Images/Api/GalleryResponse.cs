using Marten;

namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// A page of a gallery (glossary: Gallery): image notes the caller can see, newest page
/// first and oldest first within a page, as a timeline. The web flattens them into images,
/// so a note with three images is three tiles that share one caption.
/// </summary>
public record GalleryResponse
{
    /// <summary>The page's image notes, oldest first.</summary>
    public required GalleryItem[] Items { get; init; }
    /// <summary>Whether there are older image notes: ask again with <c>before</c> = the first item's <c>postedAt</c>.</summary>
    public required bool HasOlder { get; init; }
    /// <summary>
    /// How many images the whole gallery holds for the caller, on every page (a note with
    /// three images counts three). Only notes the caller can see count.
    /// </summary>
    public required int ImageCount { get; init; }

    public const int DefaultTake = 30;
    public const int MaxTake = 60;

    /// <summary>
    /// The response for <paramref name="notes"/> (a page, oldest first), with each note's
    /// session number, and the images of every note in <paramref name="all"/> (the gallery's
    /// query without paging) counted. Only the image lists are loaded for the count.
    /// </summary>
    public static async Task<GalleryResponse> For(
        IQuerySession session, IReadOnlyList<SessionNote> notes, bool hasOlder, IQueryable<SessionNote> all, CancellationToken ct)
    {
        var imageLists = await all.Select(n => new NoteImages(n.Images)).ToListAsync(ct);
        var sessionIds = notes.Select(n => n.SessionId).Distinct().ToArray();
        var numbers = sessionIds.Length == 0
            ? new Dictionary<Guid, int>()
            : (await session.LoadManyAsync<Session>(ct, sessionIds)).ToDictionary(s => s.Id, s => s.Number);
        return new GalleryResponse
        {
            Items = notes
                .Select(n => new GalleryItem { Note = SessionNoteResponse.From(n), SessionNumber = numbers[n.SessionId] })
                .ToArray(),
            HasOlder = hasOlder,
            ImageCount = imageLists.Sum(n => n.Images?.Length ?? 0),
        };
    }
}

internal record NoteImages(NoteImage[]? Images);

public record GalleryItem
{
    public required SessionNoteResponse Note { get; init; }
    public required int SessionNumber { get; init; }
}
