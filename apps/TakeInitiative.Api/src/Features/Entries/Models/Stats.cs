namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// A Character entry's optional stat line (glossary: Stats). <see cref="InitiativeRoll"/> and
/// <see cref="MaxHp"/> are dice expressions, checked with <c>IDiceRoller.Check</c>; a plain
/// number is one. Step 18 rolls <see cref="MaxHp"/> when a combatant is added (design §8).
/// At least one field is set: stats with nothing in them are stored as null.
/// </summary>
public record Stats
{
    public string? InitiativeRoll { get; init; }
    public string? MaxHp { get; init; }
    public int? Ac { get; init; }

    public const int ExpressionMaxLength = 100;
    public const int AcMax = 99;

    /// <summary>Trims the expressions; blank ones are null, and all-null stats are null.</summary>
    public static Stats? Of(string? initiativeRoll, string? maxHp, int? ac)
    {
        var stats = new Stats
        {
            InitiativeRoll = string.IsNullOrWhiteSpace(initiativeRoll) ? null : initiativeRoll.Trim(),
            MaxHp = string.IsNullOrWhiteSpace(maxHp) ? null : maxHp.Trim(),
            Ac = ac,
        };
        return stats is { InitiativeRoll: null, MaxHp: null, Ac: null } ? null : stats;
    }
}

/// <summary>
/// Who reads and writes an entry's stats (15g.3, design §4). Both assume nothing: each checks
/// that the viewer can see the entry, and stats exist only on <c>Character</c> entries.
/// <list type="bullet">
/// <item>Claimed (a player character): everyone who can see the entry reads them, and the
/// claimer and the DMs write them.</item>
/// <item>Unclaimed (an NPC or a monster): the DMs only, for both. A monster's HP and AC are
/// combat secrets (invariant 8), so for anyone else <c>stats</c> is absent, the same as when
/// there are none.</item>
/// </list>
/// </summary>
public static class EntryStats
{
    public static bool CanRead(Entry entry, Member viewer)
        => entry.Kind == EntryKind.Character
            && EntryVisibility.CanSee(entry, viewer)
            && (entry.ClaimedByMemberId is not null || viewer.Role == Role.DM);

    public static bool CanWrite(Entry entry, Member viewer)
        => entry.Kind == EntryKind.Character
            && EntryVisibility.CanSee(entry, viewer)
            && (viewer.Role == Role.DM || (entry.ClaimedByMemberId is { } claimer && claimer == viewer.MemberId));

    /// <summary>The stats as <paramref name="viewer"/> may read them: null when there are none or they may not.</summary>
    public static Stats? For(Entry entry, Member viewer) => CanRead(entry, viewer) ? entry.Stats : null;
}
