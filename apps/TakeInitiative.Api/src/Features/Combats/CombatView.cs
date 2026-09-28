using Marten;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// The only way a combat leaves the server, for reads and pushes alike (invariant 8). Pure:
/// the entries the viewer can see come in as <c>visibleEntryIds</c>, a map from each
/// combatant's stored entry id to the entry it resolves to (through merges) when the viewer
/// can see that entry (<see cref="CombatEntries"/>).
/// <list type="bullet">
/// <item>DMs get every field (never <c>Tiebreak</c>).</item>
/// <item>Players get no hidden combatant at all, no turn on one, and per <see cref="PlayersSee"/>:
/// <c>Exact</c> HP, max HP and AC; <c>Band</c> the band only; <c>Nothing</c> none of them.</item>
/// <item>A player's own combatant comes exactly, whatever its PlayersSee, hidden or not.</item>
/// </list>
/// </summary>
public static class CombatView
{
    /// <summary>Whether <paramref name="viewer"/> may see the combat at all: players only once it has started.</summary>
    public static bool CanSee(Combat combat, Member viewer)
        => viewer.Role == Role.DM || combat.StartedAt is not null;

    /// <summary>Whether <paramref name="viewer"/> may see this combatant's row.</summary>
    public static bool CanSee(Combatant combatant, Member viewer)
        => viewer.Role == Role.DM || !combatant.Hidden || IsOwn(combatant, viewer);

    private static bool IsOwn(Combatant combatant, Member viewer)
        => combatant.OwnerMemberId is { } owner && owner == viewer.MemberId;

    /// <summary>The combatants <paramref name="viewer"/> sees, listed as every response lists them.</summary>
    public static IEnumerable<Combatant> VisibleCombatants(Combat combat, Member viewer)
        => CombatOrder.Listed(combat).Where(c => CanSee(c, viewer));

    public static CombatResponse For(Combat combat, Member viewer, IReadOnlyDictionary<Guid, Guid> visibleEntryIds)
    {
        var turn = combat.TurnCombatantId is { } turnId && combat.Find(turnId) is { } holder && CanSee(holder, viewer)
            ? turnId
            : (Guid?)null;
        return new CombatResponse
        {
            Id = combat.Id,
            CampaignId = combat.CampaignId,
            SessionId = combat.SessionId,
            Name = combat.Name,
            Status = combat.Status,
            Round = combat.Round,
            TurnCombatantId = turn,
            CreatedAt = combat.CreatedAt,
            StartedAt = combat.StartedAt,
            FinishedAt = combat.FinishedAt,
            Combatants = VisibleCombatants(combat, viewer).Select(c => For(c, viewer, visibleEntryIds)).ToArray(),
        };
    }

    public static CombatantResponse For(Combatant combatant, Member viewer, IReadOnlyDictionary<Guid, Guid> visibleEntryIds)
    {
        var exact = viewer.Role == Role.DM || IsOwn(combatant, viewer);
        var showHp = exact || combatant.PlayersSee == PlayersSee.Exact;
        var showBand = showHp || combatant.PlayersSee == PlayersSee.Band;
        return new CombatantResponse
        {
            Id = combatant.Id,
            Name = combatant.Name,
            EntryId = combatant.EntryId is { } entryId && visibleEntryIds.TryGetValue(entryId, out var resolved) ? resolved : null,
            OwnerMemberId = combatant.OwnerMemberId,
            Initiative = combatant.Initiative,
            Waiting = combatant.IsWaiting,
            Hp = showHp ? combatant.Hp : null,
            MaxHp = showHp ? combatant.MaxHp : null,
            Band = showBand ? HpBands.Of(combatant.Hp, combatant.MaxHp) : null,
            Ac = showHp ? combatant.Ac : null,
            Hidden = exact && combatant.Hidden,
            PlayersSee = combatant.PlayersSee,
            Conditions = [.. combatant.Conditions],
            InitiativeRoll = exact ? combatant.InitiativeRoll : null,
        };
    }

    /// <summary>The list row. The count is of the combatants the viewer can see.</summary>
    public static CombatSummaryResponse Summary(Combat combat, Member viewer, int sessionNumber) => new()
    {
        Id = combat.Id,
        Name = combat.Name,
        Status = combat.Status,
        Round = combat.Round,
        SessionId = combat.SessionId,
        SessionNumber = sessionNumber,
        CombatantCount = combat.Combatants.Count(c => CanSee(c, viewer)),
        CreatedAt = combat.CreatedAt,
        StartedAt = combat.StartedAt,
        FinishedAt = combat.FinishedAt,
    };
}

/// <summary>
/// Resolves a combat's entry ids for <see cref="CombatView"/>: each stored id to the entry it
/// is now (following merges, 15g), and then per viewer, only the entries they can see
/// (<see cref="EntryVisibility.CanSee"/>, the one read rule).
/// </summary>
public static class CombatEntries
{
    private const int MaxMergeHops = 32;

    /// <summary>Each stored entry id of <paramref name="combats"/> to the entry it resolves to. Missing ones are left out.</summary>
    public static async Task<IReadOnlyDictionary<Guid, Entry>> Resolve(IQuerySession session, IEnumerable<Combat> combats, CancellationToken ct)
    {
        var ids = combats.SelectMany(c => c.EntryIds).Distinct().ToArray();
        var loaded = new Dictionary<Guid, Entry>();
        var pending = ids;
        for (var hops = 0; pending.Length > 0 && hops < MaxMergeHops; hops++)
        {
            foreach (var entry in await session.LoadManyAsync<Entry>(ct, pending))
            {
                loaded[entry.Id] = entry;
            }
            pending = loaded.Values
                .Where(e => e.MergedIntoId is { } into && !loaded.ContainsKey(into))
                .Select(e => e.MergedIntoId!.Value)
                .Distinct()
                .ToArray();
        }

        var resolved = new Dictionary<Guid, Entry>();
        foreach (var id in ids)
        {
            var entry = loaded.GetValueOrDefault(id);
            for (var hops = 0; entry?.MergedIntoId is { } into && hops < MaxMergeHops; hops++)
            {
                entry = loaded.GetValueOrDefault(into);
            }
            if (entry is not null && entry.MergedIntoId is null)
            {
                resolved[id] = entry;
            }
        }
        return resolved;
    }

    public static Task<IReadOnlyDictionary<Guid, Entry>> Resolve(IQuerySession session, Combat combat, CancellationToken ct)
        => Resolve(session, [combat], ct);

    /// <summary>The <c>visibleEntryIds</c> for <see cref="CombatView.For(Combat, Member, IReadOnlyDictionary{Guid, Guid})"/>.</summary>
    public static IReadOnlyDictionary<Guid, Guid> VisibleTo(IReadOnlyDictionary<Guid, Entry> resolved, Member viewer)
        => resolved
            .Where(pair => EntryVisibility.CanSee(pair.Value, viewer))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Id);

    /// <summary>Loads and redacts in one go, for a read or a write's response.</summary>
    public static async Task<CombatResponse> ViewFor(IQuerySession session, Combat combat, Member viewer, CancellationToken ct)
        => CombatView.For(combat, viewer, VisibleTo(await Resolve(session, combat, ct), viewer));
}
