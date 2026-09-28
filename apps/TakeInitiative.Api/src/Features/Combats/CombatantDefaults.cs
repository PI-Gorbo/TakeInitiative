using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>One pick of <c>POST combatants</c>: an entry, or a plain name, with what the request sets.</summary>
public sealed record CombatantPick
{
    /// <summary>The entry, already loaded and checked (merges followed), or null for a plain name.</summary>
    public Entry? Entry { get; init; }
    public string? Name { get; init; }
    public int Count { get; init; } = 1;
    public string? InitiativeRoll { get; init; }
    /// <summary>A dice expression, rolled once per combatant.</summary>
    public string? MaxHp { get; init; }
    public int? Ac { get; init; }
    public bool? Hidden { get; init; }
    public PlayersSee? PlayersSee { get; init; }
}

/// <summary>
/// Turns picks into waiting combatants (18a.3), pure over <see cref="IDiceRoller"/> and a
/// <see cref="Random"/> for the tiebreaks.
/// <list type="bullet">
/// <item>Names are the entry's (or the given) name, numbered <c>Goblin 1</c>…<c>Goblin 4</c> for a
/// count above 1, continuing past names already in the combat (<c>Goblin 5</c> next); a single
/// pick whose name is taken becomes <c>Goblin 2</c>.</item>
/// <item>Stats come from <see cref="EntryStats.For"/>, so a player never reads an NPC's. Max HP is
/// rolled once per combatant, and HP starts at it. What the request sets wins.</item>
/// <item>The owner is the entry's claimer; PlayersSee is <c>Exact</c> with an owner and <c>Band</c>
/// without.</item>
/// <item>A combatant from an entry that is not <c>Everyone</c> starts hidden, so adding a DM
/// entry never reveals its name (invariant 5).</item>
/// </list>
/// </summary>
public static class CombatantDefaults
{
    public static Result<IReadOnlyList<Combatant>> Build(
        IReadOnlyList<CombatantPick> picks, Member caller, IEnumerable<Combatant> existing, IDiceRoller dice, Random random)
    {
        var taken = existing.Select(c => c.Name).ToList();
        var added = new List<Combatant>();
        foreach (var pick in picks)
        {
            var entry = pick.Entry;
            var stats = entry is null ? null : EntryStats.For(entry, caller);
            var baseName = Combat.NormaliseName(pick.Name ?? entry?.Name ?? "");
            var maxHpRoll = Blank(pick.MaxHp) ?? stats?.MaxHp;
            var owner = entry?.ClaimedByMemberId;

            foreach (var name in Names(baseName, pick.Count, taken))
            {
                int? maxHp = null;
                if (maxHpRoll is not null)
                {
                    var rolled = dice.EvaluateRoll(maxHpRoll);
                    if (rolled.IsFailure)
                    {
                        return Result.Failure<IReadOnlyList<Combatant>>(rolled.Error);
                    }
                    maxHp = Math.Clamp(rolled.Value.Total, Combatant.MaxHpMin, Combatant.HpMax);
                }
                taken.Add(name);
                added.Add(new Combatant
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    EntryId = entry?.Id,
                    OwnerMemberId = owner,
                    InitiativeRoll = Blank(pick.InitiativeRoll) ?? stats?.InitiativeRoll ?? Combatant.DefaultInitiativeRoll,
                    Tiebreak = CombatOrder.NewTiebreak(random),
                    Hp = maxHp,
                    MaxHp = maxHp,
                    Ac = pick.Ac ?? stats?.Ac,
                    Hidden = pick.Hidden ?? (entry is not null && entry.Visibility != Visibility.Everyone),
                    PlayersSee = pick.PlayersSee ?? (owner is null ? PlayersSee.Band : PlayersSee.Exact),
                    Conditions = [],
                });
            }
        }
        return Result.Success<IReadOnlyList<Combatant>>(added);
    }

    /// <summary>
    /// The names for <paramref name="count"/> copies of <paramref name="baseName"/>, given the
    /// names already <paramref name="taken"/> (case-insensitive). A bare <c>Goblin</c> counts as
    /// <c>Goblin 1</c>.
    /// </summary>
    public static IReadOnlyList<string> Names(string baseName, int count, IEnumerable<string> taken)
    {
        var numbered = new Regex($"^{Regex.Escape(baseName)}(?: (\\d+))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var highest = 0;
        foreach (var name in taken)
        {
            var match = numbered.Match(name);
            if (match.Success)
            {
                var number = match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var n) ? n : 1;
                highest = Math.Max(highest, number);
            }
        }
        if (highest == 0 && count == 1)
        {
            return [baseName];
        }
        return Enumerable.Range(highest + 1, count).Select(n => Numbered(baseName, n)).ToList();
    }

    /// <summary><c>Goblin 3</c>, shortening a long name so the whole stays within the limit.</summary>
    private static string Numbered(string baseName, int number)
    {
        var suffix = $" {number}";
        var room = Combatant.NameMaxLength - suffix.Length;
        return (baseName.Length > room ? baseName[..room].TrimEnd() : baseName) + suffix;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
