using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// A reference monster's rules text, laid out as SRD 5.2 lays it out (glossary: Stat block; not
/// Stats, which is an entry's three-field stat line). This is the schema <c>scripts/srd/build-srd52.mjs</c>
/// writes, one monster per line of <c>Reference/Srd52/monsters.json</c>, so the API never depends
/// on Open5e's model.
/// <para>
/// Text fields are plain text. Their only markup is <c>_italic_</c> and <c>**bold**</c> runs, blank
/// lines between paragraphs and <c>- </c> list items; the build script fails on anything else.
/// Empty strings mean "none" (no resistances, no languages).
/// </para>
/// </summary>
public record StatBlock
{
    /// <summary>The SRD's slug: "goblin-warrior".</summary>
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Where the SRD lists it: the Monsters chapter or the Animals appendix.</summary>
    public required StatBlockCategory Category { get; init; }

    /// <summary>"Small", "Medium", … as the SRD capitalises them.</summary>
    public required string Size { get; init; }

    /// <summary>"Fey", "Beast", …</summary>
    public required string Type { get; init; }

    /// <summary>"Chaotic Neutral", "Unaligned", …</summary>
    public required string Alignment { get; init; }

    public required int Ac { get; init; }

    /// <summary>What the AC comes from ("natural armor"), or empty.</summary>
    public required string AcNote { get; init; }

    /// <summary>SRD 5.2's initiative modifier: +2 prints as "Initiative +2 (12)".</summary>
    public required int InitiativeBonus { get; init; }

    /// <summary>The average hit points.</summary>
    public required int Hp { get; init; }

    /// <summary>The hit dice without spaces, a dice expression: "3d6", "4d8-4", "20d10+40".</summary>
    public required string HitDice { get; init; }

    public required StatBlockSpeed Speed { get; init; }
    public required AbilityScores Abilities { get; init; }

    /// <summary>5.2's SAVE column: every ability's saving throw modifier, proficient or not.</summary>
    public required AbilityScores Saves { get; init; }

    /// <summary>The skills with a bonus, by camel-cased name ("stealth", "sleightOfHand"), in the SRD's alphabetical order.</summary>
    public required IReadOnlyDictionary<string, int> Skills { get; init; }

    public required string Vulnerabilities { get; init; }
    public required string Resistances { get; init; }

    /// <summary>Damage immunities. 5.2 prints them on one line with <see cref="ConditionImmunities"/>, split by "; ".</summary>
    public required string Immunities { get; init; }
    public required string ConditionImmunities { get; init; }

    /// <summary>"Darkvision 60 ft.; Passive Perception 9".</summary>
    public required string Senses { get; init; }
    public required string Languages { get; init; }

    /// <summary>The challenge rating as the SRD prints it: "0", "1/8", "1/4", "1/2", "1" … "30".</summary>
    public required string Cr { get; init; }
    public required int Xp { get; init; }

    /// <summary>The proficiency bonus.</summary>
    public required int Pb { get; init; }

    public required IReadOnlyList<StatBlockTrait> Traits { get; init; }

    /// <summary>Actions, bonus actions, reactions and legendary actions, grouped by kind in that order and in the SRD's order within a kind.</summary>
    public required IReadOnlyList<StatBlockAction> Actions { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<StatBlockCategory>))]
public enum StatBlockCategory
{
    Monster,
    Animal,
}

/// <summary>Speeds in feet; null when the creature has none of that kind.</summary>
public record StatBlockSpeed
{
    public int? Walk { get; init; }
    public int? Fly { get; init; }
    public int? Swim { get; init; }
    public int? Climb { get; init; }
    public int? Burrow { get; init; }

    /// <summary>Whether the fly speed is "(hover)".</summary>
    public bool Hover { get; init; }
}

/// <summary>A value per ability: the scores, or the saving throw modifiers.</summary>
public record AbilityScores
{
    public required int Str { get; init; }
    public required int Dex { get; init; }
    public required int Con { get; init; }
    public required int Int { get; init; }
    public required int Wis { get; init; }
    public required int Cha { get; init; }
}

public record StatBlockTrait
{
    /// <summary>"Legendary Resistance (3/Day)".</summary>
    public required string Name { get; init; }
    public required string Text { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<StatBlockActionKind>))]
public enum StatBlockActionKind
{
    Action,
    BonusAction,
    Reaction,
    LegendaryAction,
}

public record StatBlockAction
{
    public required StatBlockActionKind Kind { get; init; }

    /// <summary>The name with its usage limit, as the SRD prints it: "Fire Breath (Recharge 5–6)".</summary>
    public required string Name { get; init; }
    public required string Text { get; init; }
}
