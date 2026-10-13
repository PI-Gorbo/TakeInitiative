using Marten;
using Microsoft.AspNetCore.SignalR;
using TakeInitiative.Api.Features.Images;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Utilities.Extensions;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// An entry's primary image (SAM-12): the rule for which image may be one, and the clear that
/// keeps it true afterwards.
/// <para>
/// An image carries no visibility of its own — <see cref="ImageAccess.CanSee"/> reads its note at
/// request time (invariant 5). Rather than redact the primary image per viewer, only an image
/// <b>everyone</b> can see may be primary. That keeps the id safe on
/// <see cref="EntrySummaryResponse"/>, which one payload pushes to the entry's whole audience.
/// The price is that the note can stop being public later, which is what
/// <see cref="ClearStale"/> is for.
/// </para>
/// </summary>
public static class EntryPrimaryImages
{
    /// <summary>The validation key and message. One message for every reason, so another member's image id tells the caller nothing.</summary>
    public const string ErrorKey = "imageId";
    public const string UnavailableMessage = "That image cannot be this entry's primary image. Pick one from its gallery that everyone can see.";

    /// <summary>
    /// Whether <paramref name="note"/>'s images are visible to every member: exactly what
    /// <see cref="SessionNoteVisibility"/> grants a Player. A <c>DM</c> or <c>Me</c> note, or a
    /// hidden one, is seen by some members and not others, so its images cannot stand for an entry.
    /// </summary>
    public static bool IsPublic(SessionNote note) => note.Visibility == Visibility.Everyone && !note.IsHidden;

    /// <summary>
    /// Whether <paramref name="imageId"/> may be <paramref name="entry"/>'s primary image: a live
    /// image of this campaign, on a note everyone can see, whose caption mentions the entry. The
    /// last part is the entry's gallery rule (16d) — <see cref="Entry.MentionIds"/>, so a mention
    /// of a merged entry counts, exactly as the gallery shows it.
    /// </summary>
    public static async Task<bool> CanBePrimary(IQuerySession session, Entry entry, Guid imageId, CancellationToken ct)
    {
        var image = await session.LoadAsync<Image>(imageId, ct);
        if (image is null || image.CampaignId != entry.CampaignId || image.DeletedAt is not null || image.NoteId is not { } noteId)
        {
            return false;
        }
        var note = await session.LoadAsync<SessionNote>(noteId, ct);
        if (note is null || note.CampaignId != entry.CampaignId || !IsPublic(note))
        {
            return false;
        }
        var mentioned = MentionParser.EntryIds(note.Text);
        return entry.MentionIds().Any(mentioned.Contains);
    }

    /// <summary>
    /// Clears the primary image of every entry pointing at one of <paramref name="imageIds"/>, for
    /// a note that has stopped being one everyone can see — hidden, moved to <c>DM</c> or <c>Me</c>,
    /// deleted, or edited to drop the image. The events are appended to
    /// <paramref name="session"/> and committed with the caller's own write, so an entry never
    /// serves an id its readers cannot fetch. Returns the entry ids that were cleared, to hand to
    /// <see cref="NotifyCleared"/> after the save.
    /// <para>
    /// Clearing rather than checking on every read is deliberate: it writes the truth once, gives
    /// history a row, and leaves every reader of the summary free.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> ClearStale(
        IDocumentSession session, Guid campaignId, IReadOnlyCollection<Guid> imageIds, Actor actor, CancellationToken ct)
    {
        if (imageIds.Count == 0)
        {
            return [];
        }
        var ids = imageIds.Distinct().ToArray();
        var entryIds = await session.Query<Entry>()
            .Where(e => e.CampaignId == campaignId && e.PrimaryImageId!.Value.IsOneOf(ids))
            .Select(e => e.Id)
            .ToListAsync(ct);
        foreach (var entryId in entryIds)
        {
            session.Events.Append(entryId, new EntryPrimaryImageCleared(actor));
        }
        return [.. entryIds];
    }

    /// <summary>
    /// <see cref="ClearStale"/> for one note: the images it has now. Used where the whole note is
    /// leaving public view (hidden, moved, deleted) rather than losing particular images.
    /// </summary>
    public static Task<IReadOnlyList<Guid>> ClearStaleFor(
        IDocumentSession session, SessionNote note, Actor actor, CancellationToken ct)
        => ClearStale(session, note.CampaignId, [.. note.Images.Select(i => i.ImageId)], actor, ct);

    /// <summary>
    /// <c>entryUpserted</c> for each entry <see cref="ClearStale"/> cleared, read back after the
    /// save so the payload is the entry without its primary image.
    /// </summary>
    public static async Task NotifyCleared(
        IHubContext<CampaignHub> hub, IQuerySession session, IReadOnlyList<Guid> entryIds, CancellationToken ct)
    {
        foreach (var entryId in entryIds)
        {
            if (await session.LoadAsync<Entry>(entryId, ct) is { } entry)
            {
                await hub.NotifyEntryUpserted(entry);
            }
        }
    }
}
