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
}
