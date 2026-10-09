namespace TakeInitiative.Api.Features.Connections;

/// <summary>What a piece of evidence is (glossary: Evidence).</summary>
public enum EvidenceKind
{
    /// <summary>An article block the viewer can see. The article's own entry counts as mentioned in it.</summary>
    Block,
    /// <summary>A session note the viewer can see.</summary>
    Note,
    /// <summary>A started combat, through the combatants the viewer can see (glossary: Fought together).</summary>
    Combat,
}

/// <summary>
/// One place two or more entries co-occur, before it is paired up: a note, an article block or
/// a combat, with the raw entry ids it names and a reference back to it.
/// </summary>
/// <param name="Kind">What it is.</param>
/// <param name="Id">The note's, the block's or the combat's id.</param>
/// <param name="EntryIds">The ids as stored: mentions, the article's own entry, or combatants' entries. Not yet resolved or filtered.</param>
/// <param name="At">The note's <c>PostedAt</c> or the combat's <c>StartedAt</c>; null for a block, which has no time.</param>
/// <param name="ArticleEntryId">For a block, the entry whose article holds it.</param>
public record ConnectionSource(EvidenceKind Kind, Guid Id, IReadOnlyCollection<Guid> EntryIds, DateTimeOffset? At, Guid? ArticleEntryId = null);

/// <summary>An undirected pair of entries, keyed <c>(min, max)</c>.</summary>
public readonly record struct EntryPair
{
    public Guid A { get; }
    public Guid B { get; }

    public EntryPair(Guid x, Guid y)
    {
        (A, B) = x.CompareTo(y) <= 0 ? (x, y) : (y, x);
    }

    public bool Contains(Guid id) => A == id || B == id;

    /// <summary>The end that is not <paramref name="id"/>.</summary>
    public Guid Other(Guid id) => A == id ? B : A;
}

/// <summary>One connection: a pair and its evidence, in the order the sources came in.</summary>
public record Connection(EntryPair Pair, IReadOnlyList<ConnectionSource> Evidence)
{
    /// <summary>How many pieces of evidence the viewer has (glossary: Weight). Always the number of rows.</summary>
    public int Weight => Evidence.Count;
    public int Notes => Evidence.Count(e => e.Kind == EvidenceKind.Note);
    public int Blocks => Evidence.Count(e => e.Kind == EvidenceKind.Block);
    public int Combats => Evidence.Count(e => e.Kind == EvidenceKind.Combat);
    /// <summary>The newest note's or combat's time; null when only blocks connect the pair.</summary>
    public DateTimeOffset? LastAt => Evidence.Max(e => e.At);
}

/// <summary>
/// What counts as a connection (19a.1, design §6), pure. Each source's ids are resolved through
/// merges (a merged id means its target, 15g), ids that are not a listed entry the viewer can
/// see are dropped, and the set is de-duplicated. A source left with <i>n</i> ≥ 2 ids gives each
/// of its <i>n(n−1)/2</i> pairs one piece of evidence: once per pair however often it repeats a
/// mention, and never an entry with itself.
/// <para>
/// The caller hands in only sources the viewer can see (<see cref="ConnectionIndex"/>), so the
/// weight and the evidence rows come from this one run and cannot disagree (19's Notes, "Weight
/// equals rows").
/// </para>
/// </summary>
public static class ConnectionPairs
{
    /// <param name="sources">Sources the viewer can see.</param>
    /// <param name="mergeTargets">Each merged id to its target (<see cref="MentionIndex.MergeTargets"/>).</param>
    /// <param name="visibleEntryIds">The ids of the listed entries the viewer can see.</param>
    public static IReadOnlyDictionary<EntryPair, Connection> From(
        IEnumerable<ConnectionSource> sources,
        IReadOnlyDictionary<Guid, Guid> mergeTargets,
        IReadOnlySet<Guid> visibleEntryIds)
    {
        var evidence = new Dictionary<EntryPair, List<ConnectionSource>>();
        foreach (var source in sources)
        {
            var ids = Resolve(source.EntryIds, mergeTargets, visibleEntryIds);
            for (var i = 0; i < ids.Length; i++)
            {
                for (var j = i + 1; j < ids.Length; j++)
                {
                    var pair = new EntryPair(ids[i], ids[j]);
                    if (!evidence.TryGetValue(pair, out var list))
                    {
                        evidence[pair] = list = [];
                    }
                    list.Add(source);
                }
            }
        }
        return evidence.ToDictionary(p => p.Key, p => new Connection(p.Key, p.Value));
    }

    /// <summary>The distinct visible entries a source names, after merges, in first-mention order.</summary>
    public static Guid[] Resolve(IEnumerable<Guid> ids, IReadOnlyDictionary<Guid, Guid> mergeTargets, IReadOnlySet<Guid> visibleEntryIds)
        => ids
            .Select(id => mergeTargets.GetValueOrDefault(id, id))
            .Where(visibleEntryIds.Contains)
            .Distinct()
            .ToArray();
}
