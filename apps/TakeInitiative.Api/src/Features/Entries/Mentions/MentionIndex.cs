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
/// </summary>
public record MentionCount(int Count, DateTimeOffset? LastMentionedAt);

/// <summary>One article block that mentions an entry, on the entry whose article holds it.</summary>
public record BlockMention(Entry Entry, ArticleBlock Block);

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
/// 15g adds merged ids.
/// </para>
/// </summary>
public static class MentionIndex
{
    /// <summary>
    /// The notes the viewer can see that mention any of <paramref name="entryIds"/>, for a
    /// timeline: the newest <paramref name="take"/> posted before <paramref name="before"/>
    /// (all when null), returned oldest first.
    /// </summary>
    public static async Task<MentionedNotesPage> NotesMentioning(
        IQuerySession session, Guid campaignId, IReadOnlyCollection<Guid> entryIds, Member viewer,
        DateTimeOffset? before, int take, CancellationToken ct)
    {
        var query = session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId)
            .Where(MentioningAny<SessionNote>(nameof(SessionNote.MentionedEntryIds), entryIds))
            .Where(SessionNoteVisibility.VisibleTo(viewer));
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
    /// Per entry id: how many notes and article blocks the viewer can see mention it, and when
    /// the latest such note was posted. A note or block that mentions an entry twice counts
    /// once, and an article's mentions of its own entry are not counted. Only the id list and
    /// <c>PostedAt</c> of each note are loaded, and they are grouped in memory, which is fine at
    /// a campaign's scale. Counts are per viewer, because a count over notes or blocks the
    /// viewer cannot see would reveal that they exist.
    /// <paramref name="visibleEntries"/> are the campaign's entries the viewer can see, when
    /// the caller has already loaded them.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, MentionCount>> CountsFor(
        IQuerySession session, Guid campaignId, Member viewer, CancellationToken ct, IReadOnlyList<Entry>? visibleEntries = null)
    {
        var rows = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId)
            .Where(SessionNoteVisibility.VisibleTo(viewer))
            .Select(n => new NoteMentions(n.MentionedEntryIds, n.PostedAt))
            .ToListAsync(ct);
        visibleEntries ??= await session.Query<Entry>()
            .Where(e => e.CampaignId == campaignId)
            .Where(EntryVisibility.VisibleTo(viewer))
            .ToListAsync(ct);

        var fromNotes = rows
            .SelectMany(r => r.MentionedEntryIds.Select(id => (EntryId: id, PostedAt: (DateTimeOffset?)r.PostedAt)));
        var fromBlocks = visibleEntries
            .Where(e => e.ArticleMentionIds.Length > 0)
            .SelectMany(e => ArticleView.VisibleBlocks(e, viewer)
                .SelectMany(b => MentionParser.EntryIds(b.Text))
                .Where(id => id != e.Id)
                .Select(id => (EntryId: id, PostedAt: (DateTimeOffset?)null)));

        return fromNotes.Concat(fromBlocks)
            .GroupBy(x => x.EntryId)
            .ToDictionary(g => g.Key, g => new MentionCount(g.Count(), g.Max(x => x.PostedAt)));
    }

    /// <summary>
    /// The article blocks the viewer can see that mention any of <paramref name="entryIds"/>,
    /// in article order, leaving out an article's mentions of its own entry. Entries are
    /// found with the GIN index on <see cref="Entry.ArticleMentionIds"/> and the entry read
    /// rule, and blocks are filtered in memory with <see cref="EntryVisibility.CanSeeBlock"/>.
    /// The timeline lists them (by entry), and connections (step 19) build on it.
    /// </summary>
    public static async Task<IReadOnlyList<BlockMention>> BlocksMentioning(
        IQuerySession session, Guid campaignId, IReadOnlyCollection<Guid> entryIds, Member viewer, CancellationToken ct)
    {
        var ids = entryIds.ToHashSet();
        var entries = await session.Query<Entry>()
            .Where(e => e.CampaignId == campaignId)
            .Where(MentioningAny<Entry>(nameof(Entry.ArticleMentionIds), entryIds))
            .Where(EntryVisibility.VisibleTo(viewer))
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

        return entries
            .Where(e => !ids.Contains(e.Id))
            .SelectMany(e => ArticleView.VisibleBlocks(e, viewer)
                .Where(b => MentionParser.EntryIds(b.Text).Any(ids.Contains))
                .Select(b => new BlockMention(e, b)))
            .ToList();
    }

    private record NoteMentions(Guid[] MentionedEntryIds, DateTimeOffset PostedAt);

    /// <summary>
    /// "Mentions at least one of these ids": jsonb's <c>?|</c> ("has any of these strings as
    /// an element") on the same expression as the GIN index on the id list
    /// (<see cref="SessionNote.MentionedEntryIds"/> or <see cref="Entry.ArticleMentionIds"/>),
    /// so Postgres can use it. Marten's own translation of <c>Any(id =&gt; ids.Contains(id))</c>
    /// unnests every row in the table instead, so this is a raw SQL fragment.
    /// </summary>
    private static Expression<Func<T, bool>> MentioningAny<T>(string field, IReadOnlyCollection<Guid> entryIds) where T : notnull
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
