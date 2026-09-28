using Marten;

namespace TakeInitiative.Api.Features.Connections;

/// <summary>
/// One viewer's connections, as <see cref="ConnectionIndex"/> reads them: the pairs, and what
/// was loaded to find them, for the endpoints to draw the response from.
/// </summary>
/// <param name="Entries">The campaign's listed entries the viewer can see, by id.</param>
/// <param name="Notes">The note sources read (projections, no text), for the graph's mention counts.</param>
/// <param name="Combats">The started combats read, by id, for drawing a combat as evidence.</param>
/// <param name="Pairs">Every connection among the sources read.</param>
public record ConnectionRead(
    IReadOnlyDictionary<Guid, Entry> Entries,
    IReadOnlyList<NoteMentions> Notes,
    IReadOnlyDictionary<Guid, Combat> Combats,
    IReadOnlyDictionary<EntryPair, Connection> Pairs);

/// <summary>
/// Reads the sources of connections (19a.2) for one campaign and one viewer, with the rules the
/// sources already have, and pairs them up with <see cref="ConnectionPairs"/>. Nothing is stored
/// (invariant 7): a hide, a visibility change, a merge or a new secret block shows at once.
/// <list type="bullet">
/// <item>notes: <see cref="SessionNoteVisibility.VisibleTo"/>, as <see cref="NoteMentions"/>;</item>
/// <item>blocks: <see cref="ArticleView.VisibleBlocks"/> of the listed entries the viewer can see
/// (<see cref="EntryVisibility.Listed"/>), each with the article's own entry joined to it;</item>
/// <item>combats: started ones only (a Draft is a plan, not a fact, and a discarded Draft never
/// started), through <see cref="CombatView.VisibleCombatants"/>, so a hidden combatant connects
/// nothing for a player (invariant 8).</item>
/// </list>
/// </summary>
public static class ConnectionIndex
{
    /// <summary>
    /// The sources that name <paramref name="entry"/> (or an entry merged into it), plus its own
    /// article's blocks: the Connections panel's and the evidence's read. Notes and combats are
    /// narrowed with the GIN <c>?|</c> fragment (<see cref="MentionIndex.MentioningAny"/>).
    /// </summary>
    public static async Task<ConnectionRead> ForEntry(
        IQuerySession session, Guid campaignId, Entry entry, Member viewer, CancellationToken ct)
    {
        var ids = entry.MentionIds();
        var entries = await session.Query<Entry>().Listed(campaignId, viewer).ToListAsync(ct);

        var notes = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId)
            .Where(MentionIndex.MentioningAny<SessionNote>(nameof(SessionNote.MentionedEntryIds), ids))
            .Where(SessionNoteVisibility.VisibleTo(viewer))
            .Select(NoteMentions.Projection)
            .ToListAsync(ct);

        var combats = await session.Query<Combat>()
            .Where(c => c.CampaignId == campaignId)
            .Where(c => c.StartedAt != null)
            .Where(MentionIndex.MentioningAny<Combat>(nameof(Combat.EntryIds), ids))
            .ToListAsync(ct);

        var idSet = ids.ToHashSet();
        var articles = entries.Where(e => e.Id == entry.Id || e.ArticleMentionIds.Any(idSet.Contains));
        return Pair(entries, notes, articles, combats, viewer);
    }

    /// <summary>
    /// Every source in the campaign: the graph's read. It loads every visible note's id list, as
    /// <see cref="MentionIndex.CountsFor"/> does for the wiki home, which is fine at a campaign's
    /// scale (19's Notes, "No stored edges").
    /// </summary>
    public static async Task<ConnectionRead> ForCampaign(
        IQuerySession session, Guid campaignId, Member viewer, CancellationToken ct)
    {
        var entries = await session.Query<Entry>().Listed(campaignId, viewer).ToListAsync(ct);

        var notes = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaignId)
            .Where(SessionNoteVisibility.VisibleTo(viewer))
            .Select(NoteMentions.Projection)
            .ToListAsync(ct);

        var combats = await session.Query<Combat>()
            .Where(c => c.CampaignId == campaignId)
            .Where(c => c.StartedAt != null)
            .ToListAsync(ct);

        return Pair(entries, notes, entries.Where(e => e.ArticleMentionIds.Length > 0), combats, viewer);
    }

    /// <summary>The sources, as the viewer sees them, paired up. Pure, so the unit tests can drive it.</summary>
    public static ConnectionRead Pair(
        IReadOnlyList<Entry> entries, IReadOnlyList<NoteMentions> notes, IEnumerable<Entry> articles,
        IReadOnlyList<Combat> combats, Member viewer)
    {
        var started = combats.Where(c => c.StartedAt is not null && c.Status != CombatStatus.Draft).ToList();
        var sources = NoteSources(notes)
            .Concat(articles.SelectMany(e => BlockSources(e, viewer)))
            .Concat(started.Select(c => CombatSource(c, viewer)));

        var pairs = ConnectionPairs.From(
            sources,
            MentionIndex.MergeTargets(entries),
            entries.Select(e => e.Id).ToHashSet());

        return new ConnectionRead(
            entries.ToDictionary(e => e.Id),
            notes,
            started.ToDictionary(c => c.Id),
            pairs);
    }

    private static IEnumerable<ConnectionSource> NoteSources(IEnumerable<NoteMentions> notes)
        => notes
            .Where(n => n.MentionedEntryIds.Length >= 2)
            .Select(n => new ConnectionSource(EvidenceKind.Note, n.Id, n.MentionedEntryIds, n.PostedAt));

    /// <summary>
    /// The visible blocks of <paramref name="entry"/>'s article, each naming the entries it
    /// mentions and the entry itself: "Brother of @Tharden." in Gundren's article connects
    /// Gundren and Tharden (§6). The unit is the block, so two entries mentioned in different
    /// blocks of one article are not connected to each other.
    /// </summary>
    public static IEnumerable<ConnectionSource> BlockSources(Entry entry, Member viewer)
        => ArticleView.VisibleBlocks(entry, viewer)
            .Select(b => new ConnectionSource(
                EvidenceKind.Block, b.Id, [entry.Id, .. MentionParser.EntryIds(b.Text)], null, entry.Id));

    /// <summary>A started combat, naming the entries of the combatants the viewer can see.</summary>
    public static ConnectionSource CombatSource(Combat combat, Member viewer)
        => new(
            EvidenceKind.Combat,
            combat.Id,
            CombatView.VisibleCombatants(combat, viewer).Where(c => c.EntryId is not null).Select(c => c.EntryId!.Value).ToArray(),
            combat.StartedAt);
}
