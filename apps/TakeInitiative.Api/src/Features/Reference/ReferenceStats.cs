namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// The Stats a stat block gives an entry (20a.6), which + Wiki fills for a DM and "Use SRD stats"
/// fills later. HP is the hit dice rather than the average, so each combatant rolls its own (§8);
/// a DM who wants the average types it in. Pure.
/// </summary>
public static class ReferenceStats
{
    public static Stats From(StatBlock block)
        => Stats.Of(Initiative(block.InitiativeBonus), block.HitDice, block.Ac)!;

    /// <summary>"1d20+2", "1d20-1", or "1d20" for a bonus of 0.</summary>
    public static string Initiative(int bonus) => bonus switch
    {
        > 0 => $"1d20+{bonus}",
        < 0 => $"1d20-{-bonus}",
        _ => "1d20",
    };
}
