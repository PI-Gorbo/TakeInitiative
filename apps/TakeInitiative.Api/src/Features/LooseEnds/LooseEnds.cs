using Marten;

namespace TakeInitiative.Api.Features.LooseEnds;

/// <summary>
/// One loose end as <see cref="LooseEnds.For"/> finds it: a note (<see cref="Note"/>) or an entry
/// (<see cref="Entry"/>, with its <see cref="MentionCount"/> for the viewer), and the session it
/// belongs to, if any.
/// </summary>
public record LooseEnd(LooseEndKind Kind, Guid? SessionId, SessionNote? Note, Entry? Entry, int MentionCount);

/// <summary>What <see cref="LooseEnds.For"/> read: the loose ends, and the viewer's listed entries for drawing them.</summary>
public record LooseEndRead(IReadOnlyList<LooseEnd> Items, IReadOnlyDictionary<Guid, Entry> Entries);

/// <summary>
/// Loose ends (design §5, 19b): the viewer's to-dos that keep the wiki linked. Derived on every
/// read from the sources and never stored (invariant 7), so there is nothing to dismiss and
/// nothing to keep in step: a loose end leaves when its rule stops matching.
/// <para>
/// A loose end is listed only to someone who can resolve it (19's Notes, "Who sees a loose end"):
/// a note to its author, hidden or not; an entry to the members who can edit it
/// (<see cref="EntryPermissions.CanEdit"/>) among those who can see it
/// (<see cref="EntryVisibility.Listed"/>, which also drops merged entries).
/// </para>
/// </summary>
public static class LooseEnds
{
    /// <summary>At most this many spans go to the matcher in one request, newest notes first.</summary>
    public const int MaxSpans = 500;
    /// <summary>The matcher's options for link suggestions: the best two per span, so a rejected substring match does not hide a fuzzy one.</summary>
    public static readonly EntryMatchOptions MatchOptions = new(Take: 2, MinSimilarity: 0.6);

    private static readonly Func<SessionNote, bool> IsUntaggedImageNote = SessionNote.UntaggedImageNote.Compile();
    private static readonly Func<SessionNote, bool> IsUnlinked = SessionNote.Unlinked.Compile();

    /// <summary>
    /// Every loose end of <paramref name="viewer"/> in the campaign: notes newest first, then
    /// entries by mention count, then name (an entry both <c>Other</c> and empty is two). The list
    /// and the counts both read this, so the counts are the list grouped.
    /// </summary>
    public static async Task<LooseEndRead> For(IQuerySession session, Guid campaignId, Member viewer, CancellationToken ct)
    {
        var viewerId = viewer.MemberId;
        var notes = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId && n.AuthorMemberId == viewerId)
            .Where(SessionNote.MentionsNothing)
            .ToListAsync(ct);

        var entries = await session.Query<Entry>().Listed(campaignId, viewer).ToListAsync(ct);
        var counts = await MentionIndex.CountsFor(session, campaignId, viewer, ct, entries);

        var loose = entries
            .Where(e => EntryPermissions.CanEdit(e, viewer))
            .SelectMany(e => EntryKinds(e, viewer, counts).Select(kind => (Entry: e, Kind: kind)))
            .ToList();

        // An entry's session is its creating note's (15b), when that note still exists and the
        // viewer can see it: a note they cannot see does not get to place anything on a divider.
        // A List, not an array: Marten cannot translate C# 14's span-based Contains on an array.
        var fromNoteIds = loose.Select(x => x.Entry.CreatedFromNoteId).OfType<Guid>().Distinct().ToList();
        var noteSessions = fromNoteIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : (await session.Query<SessionNote>()
                .Where(n => n.CampaignId == campaignId && fromNoteIds.Contains(n.Id))
                .Where(SessionNoteVisibility.VisibleTo(viewer))
                .Select(NoteMentions.Projection)
                .ToListAsync(ct))
                .ToDictionary(n => n.Id, n => n.SessionId);

        var items = notes
            .OrderByDescending(n => n.PostedAt)
            .Select(n => NoteKind(n) is { } kind ? new LooseEnd(kind, n.SessionId, n, null, 0) : null)
            .OfType<LooseEnd>()
            .Concat(loose
                .Select(x => new LooseEnd(
                    x.Kind,
                    x.Entry.CreatedFromNoteId is { } noteId && noteSessions.TryGetValue(noteId, out var sessionId) ? sessionId : null,
                    null,
                    x.Entry,
                    counts.GetValueOrDefault(x.Entry.Id)?.Count ?? 0))
                .OrderByDescending(x => x.MentionCount)
                .ThenBy(x => x.Entry!.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Kind))
            .ToList();

        return new LooseEndRead(items, entries.ToDictionary(e => e.Id));
    }

    /// <summary>A note's loose end, if it is one: an image note or a text note that mentions nothing.</summary>
    public static LooseEndKind? NoteKind(SessionNote note)
        => IsUntaggedImageNote(note) ? LooseEndKind.UntaggedImageNote
            : IsUnlinked(note) ? LooseEndKind.UnlinkedNote
            : null;

    /// <summary>
    /// An entry's loose ends for a viewer who can edit it: <see cref="LooseEndKind.OtherKind"/> for
    /// kind Other, and <see cref="LooseEndKind.EmptyArticle"/> when the viewer can see a mention of
    /// it and no text in its article.
    /// </summary>
    public static IEnumerable<LooseEndKind> EntryKinds(Entry entry, Member viewer, IReadOnlyDictionary<Guid, MentionCount> counts)
    {
        if (entry.Kind == EntryKind.Other)
        {
            yield return LooseEndKind.OtherKind;
        }
        if (counts.GetValueOrDefault(entry.Id) is { Count: >= 1 } && ArticleView.IsEmptyFor(entry, viewer))
        {
            yield return LooseEndKind.EmptyArticle;
        }
    }

    /// <summary>
    /// Link suggestions (19b.2) for each of <paramref name="notes"/>, by note id: the spans of every
    /// note (<see cref="LinkSpans.From"/>, at most <see cref="MaxSpans"/> in all, in the order the
    /// notes are given) go to the entry matcher in <b>one</b> call, which applies the viewer's
    /// visibility and drops merged entries. A note with no text gets none.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<LinkMatch>>> SuggestLinks(
        EntryMatcher matcher, IQuerySession session, Guid campaignId, Member viewer,
        IEnumerable<SessionNote> notes, CancellationToken ct)
    {
        var spans = new List<(Guid NoteId, LinkSpan Span)>();
        foreach (var note in notes)
        {
            var room = MaxSpans - spans.Count;
            if (room <= 0)
            {
                break;
            }
            spans.AddRange(LinkSpans.From(note.Text).Take(room).Select(s => (note.Id, s)));
        }
        if (spans.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<LinkMatch>>();
        }

        var matches = await matcher.MatchAsync(session, campaignId, viewer, spans.Select(s => s.Span.Text).ToList(), MatchOptions, ct);

        return spans
            .Select((s, i) => (s.NoteId, Match: matches[i]
                .Where(m => LinkSpans.Accepts(s.Span.Text, m))
                .Select(m => new LinkMatch(s.Span, m.EntryId, m.Similarity))
                .FirstOrDefault()))
            .Where(x => x.Match is not null)
            .GroupBy(x => x.NoteId)
            .ToDictionary(g => g.Key, g => LinkSpans.Pick(g.Select(x => x.Match!)));
    }
}
