using System.Linq.Expressions;
using Marten;
using Marten.Linq.MatchesSql;
using NpgsqlTypes;
using Weasel.Postgresql;
using Weasel.Postgresql.SqlGeneration;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// How often the notes and article blocks a viewer can see mention one entry, and when the
/// latest note that does was posted (null when only articles mention it: blocks have no time).
/// <see cref="NoteCount"/> is the notes alone, and <see cref="LastSessionId"/> the session of the
/// latest of them (25g's "N notes · last in Session X").
/// </summary>
public record MentionCount(int Count, DateTimeOffset? LastMentionedAt, int NoteCount = 0, Guid? LastSessionId = null);

/// <summary>One article block that mentions an entry, on the entry whose article holds it.</summary>
public record BlockMention(Entry Entry, ArticleBlock Block);

/// <summary>
/// A note as the mention index reads it for counting: its id list and when it was posted, and
/// no text. <see cref="MentionIndex.CountsFor"/> reads every visible note in this shape, and
/// connections (19a) read the same shape, so a campaign-wide read never loads note text.
/// </summary>
public record NoteMentions(Guid Id, Guid SessionId, Guid[] MentionedEntryIds, DateTimeOffset PostedAt)
{
    /// <summary>The Marten projection: only these four fields leave the database.</summary>
    public static readonly Expression<Func<SessionNote, NoteMentions>> Projection
        = n => new NoteMentions(n.Id, n.SessionId, n.MentionedEntryIds, n.PostedAt);
}

/// <summary>A page of the notes that mention an entry, oldest first.</summary>
public record MentionedNotesPage(IReadOnlyList<SessionNote> Notes, bool HasOlder);

/// <summary>
/// The mention index (glossary: Mention index): which session notes and article blocks
/// mention which entry. It is a query, not a stored document. The ids live on their sources
/// (<see cref="SessionNote.MentionedEntryIds"/>, <see cref="Entry.ArticleMentionIds"/>), and a
/// mention's visibility is its source's, read with the source's own rule
/// (<see cref="SessionNoteVisibility"/>, <see cref="EntryVisibility.CanSeeBlock"/>), so it
/// cannot drift from the source the way a copied visibility could.
/// <para>
/// Every query is scoped to one campaign and one viewer. Callers join the results to the
/// entries that viewer can see, so unknown ids and ids from other campaigns never match.
/// </para>
/// <para>
/// Merges (15g) never rewrite text (invariant 6). A merged entry's id stays in the notes and
/// blocks that mention it, and resolves to the entry it was merged into: callers ask for
/// <see cref="Entry.MentionIds"/> (the entry's id and its <see cref="Entry.MergedFromIds"/>),
/// and <see cref="CountsFor"/> counts a merged id for its target.
/// </para>
/// </summary>
public static class MentionIndex
{
    /// <summary>
    /// The notes the viewer can see that mention any of <paramref name="entryIds"/>, for a
    /// timeline: the newest <paramref name="take"/> posted before <paramref name="before"/>
    /// (all when null), returned oldest first. With <paramref name="imagesOnly"/>, only image
    /// notes, whose captions mention the entry: an entry's gallery (16d).
    /// </summary>
    public static async Task<MentionedNotesPage> NotesMentioning(
        IQuerySession session, Guid campaignId, IReadOnlyCollection<Guid> entryIds, Member viewer,
        DateTimeOffset? before, int take, CancellationToken ct, bool imagesOnly = false)
    {
        var query = NotesMentioningQuery(session, campaignId, entryIds, viewer, imagesOnly);
        if (before is { } cursor)
        {
            query = query.Where(n => n.PostedAt < cursor);
        }

        var newestFirst = await query
            .OrderByDescending(n => n.PostedAt)
            .Take(take + 1)
            .ToListAsync(ct);

        return new MentionedNotesPage(
            newestFirst.Take(take).Reverse().ToList(),
            HasOlder: newestFirst.Count > take);
    }

    /// <summary>
    /// Every note the viewer can see that mentions any of <paramref name="entryIds"/>, as a
    /// query, and with <paramref name="imagesOnly"/> only image notes. The entry gallery
    /// (16d) counts its images with it.
    /// </summary>
    public static IQueryable<SessionNote> NotesMentioningQuery(
        IQuerySession session, Guid campaignId, IReadOnlyCollection<Guid> entryIds, Member viewer, bool imagesOnly = false)
    {
        var query = session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId)
            .Where(MentioningAny<SessionNote>(nameof(SessionNote.MentionedEntryIds), entryIds))
            .Where(SessionNoteVisibility.VisibleTo(viewer));
        return imagesOnly ? query.Where(SessionNote.WithImages) : query;
    }

    /// <summary>
    /// Per entry id: how many notes and article blocks the viewer can see mention it, and when
    /// the latest such note was posted. A note or block that mentions an entry twice counts
    /// once, and an article's mentions of its own entry are not counted. A merged id counts
    /// for the entry it was merged into, so a note that mentions both counts once. Only the id list and
    /// <c>PostedAt</c> of each note are loaded, and they are grouped in memory, which is fine at
    /// a campaign's scale. Counts are per viewer, because a count over notes or blocks the
    /// viewer cannot see would reveal that they exist.
    /// <paramref name="visibleEntries"/> are the campaign's listed entries the viewer can see
    /// (<see cref="EntryVisibility.Listed"/>), when the caller has already loaded them.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, MentionCount>> CountsFor(
        IQuerySession session, Guid campaignId, Member viewer, CancellationToken ct, IReadOnlyList<Entry>? visibleEntries = null)
    {
        var rows = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId)
            .Where(SessionNoteVisibility.VisibleTo(viewer))
            .Select(NoteMentions.Projection)
            .ToListAsync(ct);
        visibleEntries ??= await session.Query<Entry>()
            .Listed(campaignId, viewer)
            .ToListAsync(ct);

        return CountsFrom(rows, visibleEntries, viewer);
    }

    /// <summary>
    /// <see cref="CountsFor"/> over notes the caller has already read (every note the viewer can
    /// see, as <see cref="NoteMentions"/>) and the viewer's listed entries. The connection graph
    /// (19a) reads the same rows for its edges and sizes its nodes with this, so it reads the
    /// notes once.
    /// </summary>
    public static IReadOnlyDictionary<Guid, MentionCount> CountsFrom(
        IReadOnlyList<NoteMentions> notes, IReadOnlyList<Entry> visibleEntries, Member viewer)
        => Aggregate(notes, visibleEntries, viewer, MergeTargets(visibleEntries), only: null);

    /// <summary>
    /// <see cref="CountsFor"/> restricted to <paramref name="entries"/>: the same rule, the same
    /// per-viewer counts, over only the entries the caller has in hand. ⌘K's Entries section uses
    /// it to order at most 50 candidates, where counting the whole campaign would read every note
    /// and article for nothing.
    /// <para>
    /// Both sides are narrowed through the GIN <c>?|</c> fragment on the source's id list, so
    /// Postgres reads only the notes and articles that mention one of these entries — including the
    /// ids merged into them, because a mention of a merged entry means its target (15g).
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, MentionCount>> CountsForEntries(
        IQuerySession session, Guid campaignId, Member viewer, IReadOnlyList<Entry> entries, CancellationToken ct)
    {
        var mentionIds = entries.SelectMany(e => e.MentionIds()).Distinct().ToArray();
        if (mentionIds.Length == 0)
        {
            return new Dictionary<Guid, MentionCount>();
        }

        var rows = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId)
            .Where(SessionNoteVisibility.VisibleTo(viewer))
            .Where(MentioningAny<SessionNote>(nameof(SessionNote.MentionedEntryIds), mentionIds))
            .Select(NoteMentions.Projection)
            .ToListAsync(ct);

        // The entries whose articles mention one of these, which are not the same entries: a
        // block in someone else's article counts for the entry it mentions.
        var mentioningEntries = await session.Query<Entry>()
            .Listed(campaignId, viewer)
            .Where(MentioningAny<Entry>(nameof(Entry.ArticleMentionIds), mentionIds))
            .ToListAsync(ct);

        return Aggregate(rows, mentioningEntries, viewer, MergeTargets(entries), entries.Select(e => e.Id).ToHashSet());
    }

    /// <summary>
    /// The counting rule, over notes the viewer can see and the visible blocks of the articles that
    /// mention them. <paramref name="only"/> keeps the result to a given set of entries; null counts
    /// everything the sources mention.
    /// </summary>
    private static IReadOnlyDictionary<Guid, MentionCount> Aggregate(
        IReadOnlyList<NoteMentions> notes, IReadOnlyList<Entry> mentioningEntries, Member viewer,
        IReadOnlyDictionary<Guid, Guid> targets, IReadOnlySet<Guid>? only)
    {
        Guid Resolve(Guid id) => targets.GetValueOrDefault(id, id);
        bool Counted(Guid id) => only is null || only.Contains(id);

        var fromNotes = notes
            .SelectMany(r => r.MentionedEntryIds.Select(Resolve).Distinct().Where(Counted)
                .Select(id => (EntryId: id, PostedAt: (DateTimeOffset?)r.PostedAt, SessionId: (Guid?)r.SessionId)));
        var fromBlocks = mentioningEntries
            .Where(e => e.ArticleMentionIds.Length > 0)
            .SelectMany(e => ArticleView.VisibleBlocks(e, viewer)
                .SelectMany(b => MentionParser.EntryIds(b.Text).Select(Resolve).Distinct())
                .Where(id => id != e.Id)
                .Where(Counted)
                .Select(id => (EntryId: id, PostedAt: (DateTimeOffset?)null, SessionId: (Guid?)null)));

        return fromNotes.Concat(fromBlocks)
            .GroupBy(x => x.EntryId)
            .ToDictionary(g => g.Key, g =>
            {
                var notes = g.Where(x => x.PostedAt is not null).ToList();
                var latest = notes.Count > 0 ? notes.MaxBy(x => x.PostedAt) : default;
                return new MentionCount(g.Count(), latest.PostedAt, notes.Count, latest.SessionId);
            });
    }

    /// <summary>
    /// The article blocks the viewer can see that mention any of <paramref name="entryIds"/>,
    /// in article order, leaving out an article's mentions of its own entry (pass
    /// <see cref="Entry.MentionIds"/>, so its merged ids count as its own) and merged entries,
    /// whose blocks now live in their target's article. Entries are
    /// found with the GIN index on <see cref="Entry.ArticleMentionIds"/> and the entry read
    /// rule, and blocks are filtered in memory with <see cref="EntryVisibility.CanSeeBlock"/>.
    /// The timeline lists them (by entry). Connections (19a) read blocks the same way, with
    /// the article's own entry joining each of its blocks (<c>ConnectionIndex</c>).
    /// </summary>
    public static async Task<IReadOnlyList<BlockMention>> BlocksMentioning(
        IQuerySession session, Guid campaignId, IReadOnlyCollection<Guid> entryIds, Member viewer, CancellationToken ct)
    {
        var ids = entryIds.ToHashSet();
        var entries = await session.Query<Entry>()
            .Listed(campaignId, viewer)
            .Where(MentioningAny<Entry>(nameof(Entry.ArticleMentionIds), entryIds))
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

        return entries
            .Where(e => !ids.Contains(e.Id))
            .SelectMany(e => ArticleView.VisibleBlocks(e, viewer)
                .Where(b => MentionParser.EntryIds(b.Text).Any(ids.Contains))
                .Select(b => new BlockMention(e, b)))
            .ToList();
    }

    /// <summary>Each merged id of <paramref name="entries"/> to the entry it now resolves to.</summary>
    public static IReadOnlyDictionary<Guid, Guid> MergeTargets(IEnumerable<Entry> entries)
        => entries
            .SelectMany(e => e.MergedFromIds.Select(from => (From: from, Into: e.Id)))
            .DistinctBy(x => x.From)
            .ToDictionary(x => x.From, x => x.Into);


    /// <summary>
    /// "Mentions at least one of these ids": jsonb's <c>?|</c> ("has any of these strings as
    /// an element") on the same expression as the GIN index on the id list
    /// (<see cref="SessionNote.MentionedEntryIds"/>, <see cref="Entry.ArticleMentionIds"/> or
    /// <see cref="Combat.EntryIds"/>), so Postgres can use it. Marten's own translation of <c>Any(id =&gt; ids.Contains(id))</c>
    /// unnests every row in the table instead, so this is a raw SQL fragment.
    /// </summary>
    public static Expression<Func<T, bool>> MentioningAny<T>(string field, IReadOnlyCollection<Guid> entryIds) where T : notnull
    {
        var fragment = new MentionsAnyFragment(field, entryIds.Distinct().Select(id => id.ToString()).ToArray());
        return x => x.MatchesSql(fragment);
    }

    private sealed class MentionsAnyFragment(string field, string[] ids) : ISqlFragment
    {
        public void Apply(ICommandBuilder builder)
        {
            // field is a C# property name (nameof), never user input.
            builder.Append($"(d.data ->> '{field}')::jsonb ?| ");
            builder.AppendParameter(ids, NpgsqlDbType.Array | NpgsqlDbType.Text);
        }
    }
}
