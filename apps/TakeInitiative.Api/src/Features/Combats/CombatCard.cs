using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Marten;

namespace TakeInitiative.Api.Features.Combats;

/// <summary>
/// A combat drawn in its session in the session stream (glossary: Combat card, 18e), and in an
/// entry's COMBATS. Built only from the viewer's own <see cref="CombatResponse"/>
/// (<see cref="From"/>), so a card holds nothing the combat page would not show the same viewer.
/// </summary>
public record CombatCard
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required string Name { get; init; }
    public required CombatStatus Status { get; init; }
    public required int Round { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    /// <summary>The combatants the viewer can see: one row per entry (<c>4× @Goblin</c>), then plain names as they are, in the combat's order.</summary>
    public required CombatCardCombatant[] Combatants { get; init; }

    /// <summary>
    /// Groups the viewer's combatants by entry, in the order the combat lists them. A group of
    /// several takes the first combatant's name without its copy number ("Goblin 1" → "Goblin"),
    /// which the web shows only when it cannot name the entry itself.
    /// </summary>
    public static CombatCard From(CombatResponse combat)
    {
        var rows = new List<CombatCardCombatant>();
        var byEntry = new Dictionary<Guid, int>();
        foreach (var c in combat.Combatants)
        {
            if (c.EntryId is { } entryId && byEntry.TryGetValue(entryId, out var at))
            {
                rows[at] = rows[at] with { Count = rows[at].Count + 1, Name = CopyNumber.Replace(rows[at].Name, "") };
                continue;
            }
            if (c.EntryId is { } id)
            {
                byEntry[id] = rows.Count;
            }
            rows.Add(new CombatCardCombatant { Name = c.Name, EntryId = c.EntryId, Count = 1 });
        }
        return new CombatCard
        {
            Id = combat.Id,
            SessionId = combat.SessionId,
            Name = combat.Name,
            Status = combat.Status,
            Round = combat.Round,
            CreatedAt = combat.CreatedAt,
            StartedAt = combat.StartedAt,
            FinishedAt = combat.FinishedAt,
            Combatants = [.. rows],
        };
    }

    private static readonly Regex CopyNumber = new(@"\s+\d+$", RegexOptions.Compiled);

    /// <summary>The cards of <paramref name="combats"/> <paramref name="viewer"/> can see, each built from their own view, in the given order.</summary>
    public static async Task<IReadOnlyList<CombatCard>> For(IQuerySession session, IReadOnlyList<Combat> combats, Member viewer, CancellationToken ct)
    {
        var visible = combats.Where(c => CombatView.CanSee(c, viewer)).ToList();
        if (visible.Count == 0)
        {
            return [];
        }
        var entries = CombatEntries.VisibleTo(await CombatEntries.Resolve(session, visible, ct), viewer);
        return visible.Select(c => From(CombatView.For(c, viewer, entries))).ToList();
    }
}

/// <summary>One line of a <see cref="CombatCard"/>: an entry with how many combatants it has, or a plain name.</summary>
public record CombatCardCombatant
{
    public required string Name { get; init; }
    /// <summary>The entry, when the viewer can see it. Otherwise the name is plain text.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? EntryId { get; init; }
    public required int Count { get; init; }
}
