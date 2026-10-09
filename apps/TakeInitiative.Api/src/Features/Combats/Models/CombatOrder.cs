namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// The initiative order (glossary), pure. Rolled combatants by <c>Initiative</c> descending,
/// then <c>Tiebreak</c> descending, then <c>Id</c>, so the sort is total and stable (13a's
/// rule). Waiting combatants are not in it; they come after, in the order they were added.
/// </summary>
public static class CombatOrder
{
    /// <summary>The upper bound (exclusive) of a drawn <see cref="Combatant.Tiebreak"/>.</summary>
    public const int TiebreakRange = 1 << 30;

    public static int NewTiebreak(Random random) => random.Next(0, TiebreakRange);

    public static IReadOnlyList<Combatant> Ordered(IEnumerable<Combatant> combatants)
        => combatants
            .Where(c => !c.IsWaiting)
            .OrderByDescending(c => c.Initiative)
            .ThenByDescending(c => c.Tiebreak)
            .ThenBy(c => c.Id)
            .ToList();

    public static IReadOnlyList<Combatant> Ordered(Combat combat) => Ordered(combat.Combatants);

    public static IReadOnlyList<Combatant> Waiting(IEnumerable<Combatant> combatants)
        => combatants.Where(c => c.IsWaiting).ToList();

    public static IReadOnlyList<Combatant> Waiting(Combat combat) => Waiting(combat.Combatants);

    /// <summary>The order, then the waiting ones: how every response lists a combat's combatants.</summary>
    public static IReadOnlyList<Combatant> Listed(Combat combat) => [.. Ordered(combat), .. Waiting(combat)];

    /// <summary>
    /// Who has the turn after <paramref name="combatantId"/> in <paramref name="order"/>, and
    /// whether that wrapped past the end (a new round). Null when the order has no one else.
    /// </summary>
    public static (Guid? Next, bool Wrapped) After(IReadOnlyList<Combatant> order, Guid combatantId)
    {
        var index = order.ToList().FindIndex(c => c.Id == combatantId);
        if (index < 0 || order.Count < 2)
        {
            return (null, false);
        }
        return index + 1 < order.Count ? (order[index + 1].Id, false) : (order[0].Id, true);
    }

    /// <summary>
    /// Where the turn goes when <paramref name="combatantId"/> ends it (18b.2): the next in the
    /// order, or past the last one back to the top, which is a new round. A lone combatant
    /// takes the next round's turn too. Null only when it is not in the order.
    /// </summary>
    public static (Guid? Next, bool Wrapped) NextTurn(IReadOnlyList<Combatant> order, Guid combatantId)
    {
        if (order.Count == 1 && order[0].Id == combatantId)
        {
            return (combatantId, true);
        }
        return After(order, combatantId);
    }

    /// <summary>A new initiative and tiebreak for one combatant, from a reorder.</summary>
    public sealed record Placement(Guid CombatantId, int Initiative, int Tiebreak);

    /// <summary>
    /// A DM drags <paramref name="movedId"/> to just after <paramref name="afterId"/>, or to the
    /// top when it is null (18b.4). It takes the initiative of the neighbour it lands next to
    /// (the one above it, or the one below when it goes to the top) and a tiebreak between
    /// its neighbours'. When no integer is left between them, every combatant on that
    /// initiative is respaced evenly across the tiebreak range, in the new order.
    /// <para>
    /// Returns one placement per combatant that changes, the moved one first; none when it
    /// is already there. Both ids must be in <paramref name="order"/> (placed, not waiting).
    /// </para>
    /// </summary>
    public static IReadOnlyList<Placement> Place(IReadOnlyList<Combatant> order, Guid movedId, Guid? afterId)
    {
        var moved = order.Single(c => c.Id == movedId);
        var rest = order.Where(c => c.Id != movedId).ToList();
        var at = afterId is { } id ? rest.FindIndex(c => c.Id == id) + 1 : 0;
        if (at == order.ToList().FindIndex(c => c.Id == movedId) || rest.Count == 0)
        {
            return [];
        }

        var above = at > 0 ? rest[at - 1] : null;
        var below = at < rest.Count ? rest[at] : null;
        var initiative = above?.Initiative ?? below!.Initiative!.Value;

        // The tiebreak must sort under the one above and over the one below, among those with
        // the same initiative: anything with a different initiative is already on the right side.
        var high = above is not null && above.Initiative == initiative ? above.Tiebreak : TiebreakRange;
        var low = below is not null && below.Initiative == initiative ? below.Tiebreak : -1;
        if (high - low >= 2)
        {
            return [new Placement(movedId, initiative, low + (high - low) / 2)];
        }

        // No room: respace everyone on this initiative, in the order they will have.
        var newOrder = rest.ToList();
        newOrder.Insert(at, moved with { Initiative = initiative });
        var ties = newOrder.Where(c => c.Initiative == initiative).ToList();
        var step = TiebreakRange / (ties.Count + 1);
        var placements = ties
            .Select((c, i) => (Combatant: c, Tiebreak: (ties.Count - i) * step))
            .Where(p => p.Combatant.Id == movedId || p.Combatant.Tiebreak != p.Tiebreak)
            .Select(p => new Placement(p.Combatant.Id, initiative, p.Tiebreak))
            .ToList();
        return [.. placements.Where(p => p.CombatantId == movedId), .. placements.Where(p => p.CombatantId != movedId)];
    }
}
