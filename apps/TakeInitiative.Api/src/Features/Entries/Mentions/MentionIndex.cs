using System.Linq.Expressions;
using Marten;
using Marten.Linq.MatchesSql;
using NpgsqlTypes;
using Weasel.Postgresql;
using Weasel.Postgresql.SqlGeneration;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>How often, and how recently, the notes a viewer can see mention one entry.</summary>
public record MentionCount(int Count, DateTimeOffset LastMentionedAt);

/// <summary>A page of the notes that mention an entry, oldest first.</summary>
public record MentionedNotesPage(IReadOnlyList<SessionNote> Notes, bool HasOlder);

/// <summary>
/// The mention index (glossary: Mention index): which session notes, and from 15e which
/// article blocks, mention which entry. It is a query, not a stored document. The ids live
/// on their sources (<see cref="SessionNote.MentionedEntryIds"/>), and a mention's visibility
/// is its source's, read with the source's own rule (<see cref="SessionNoteVisibility"/>),
/// so it cannot drift from the source the way a copied visibility could.
/// <para>
/// Every query is scoped to one campaign and one viewer. Callers join the results to the
/// entries that viewer can see, so unknown ids and ids from other campaigns never match.
/// 15e adds article blocks (<c>EntryVisibility.CanSeeBlock</c>) and 15g merged ids.
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
            .Where(MentioningAny(entryIds))
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
    /// Per entry id: how many notes the viewer can see mention it, and when the latest was
    /// posted. A note that mentions an entry twice counts once. Only the id list and
    /// <c>PostedAt</c> of each note are loaded, and they are grouped in memory, which is fine
    /// at a campaign's scale. Counts are per viewer, because a count over notes the viewer
    /// cannot see would reveal that those notes exist.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, MentionCount>> CountsFor(
        IQuerySession session, Guid campaignId, Member viewer, CancellationToken ct)
    {
        var rows = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId)
            .Where(SessionNoteVisibility.VisibleTo(viewer))
            .Select(n => new NoteMentions(n.MentionedEntryIds, n.PostedAt))
            .ToListAsync(ct);

        return rows
            .SelectMany(r => r.MentionedEntryIds.Select(id => (EntryId: id, r.PostedAt)))
            .GroupBy(x => x.EntryId)
            .ToDictionary(g => g.Key, g => new MentionCount(g.Count(), g.Max(x => x.PostedAt)));
    }

    private record NoteMentions(Guid[] MentionedEntryIds, DateTimeOffset PostedAt);

    /// <summary>
    /// "Mentions at least one of these ids": jsonb's <c>?|</c> ("has any of these strings as
    /// an element") on the same expression as the GIN index on
    /// <see cref="SessionNote.MentionedEntryIds"/>, so Postgres can use it. Marten's own
    /// translation of <c>Any(id =&gt; ids.Contains(id))</c> unnests every note in the table
    /// instead, so this is a raw SQL fragment.
    /// </summary>
    private static Expression<Func<SessionNote, bool>> MentioningAny(IReadOnlyCollection<Guid> entryIds)
    {
        var fragment = new MentionsAnyFragment(entryIds.Distinct().Select(id => id.ToString()).ToArray());
        return n => n.MatchesSql(fragment);
    }

    private sealed class MentionsAnyFragment(string[] ids) : ISqlFragment
    {
        public void Apply(ICommandBuilder builder)
        {
            builder.Append("(d.data ->> 'MentionedEntryIds')::jsonb ?| ");
            builder.AppendParameter(ids, NpgsqlDbType.Array | NpgsqlDbType.Text);
        }
    }
}
