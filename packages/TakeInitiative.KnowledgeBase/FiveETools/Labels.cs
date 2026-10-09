using System.Text;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// The enums the script maps itself (<c>build-5etools-index.mjs</c> lines 49-71).
/// </summary>
/// <remarks>
/// <para>
/// These tables are the reason a label can be written at all. 5eTools' own display strings are
/// their content; a size letter, a creature type, a spell school, an item rarity and an item type
/// code are identifiers, and mapping them here means the index carries our words, not theirs. A
/// value that is not in a table is either dropped (creature types, item types, rarities) or fails
/// the build (sizes, schools) — never passed through.
/// </para>
/// </remarks>
internal static class Labels
{
    public static readonly Dictionary<string, string> Sizes = new(StringComparer.Ordinal)
    {
        ["F"] = "Fine", ["D"] = "Diminutive", ["T"] = "Tiny", ["S"] = "Small", ["M"] = "Medium",
        ["L"] = "Large", ["H"] = "Huge", ["G"] = "Gargantuan", ["C"] = "Colossal", ["V"] = "Varies",
    };

    public static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "aberration", "beast", "celestial", "construct", "dragon", "elemental", "fey", "fiend",
        "giant", "humanoid", "monstrosity", "ooze", "plant", "undead",
    };

    public static readonly Dictionary<string, string> Schools = new(StringComparer.Ordinal)
    {
        ["A"] = "Abjuration", ["C"] = "Conjuration", ["D"] = "Divination", ["E"] = "Enchantment",
        ["V"] = "Evocation", ["I"] = "Illusion", ["N"] = "Necromancy", ["T"] = "Transmutation",
        ["P"] = "Psionic",
    };

    public static readonly Dictionary<string, string> Rarities = new(StringComparer.Ordinal)
    {
        ["common"] = "Common", ["uncommon"] = "Uncommon", ["rare"] = "Rare",
        ["very rare"] = "Very Rare", ["legendary"] = "Legendary", ["artifact"] = "Artifact",
        ["varies"] = "Varies",
    };

    public static readonly Dictionary<string, string> ItemTypes = new(StringComparer.Ordinal)
    {
        ["A"] = "Ammunition", ["AF"] = "Ammunition", ["AIR"] = "Vehicle", ["AT"] = "Artisan's Tools",
        ["EXP"] = "Explosive", ["FD"] = "Food and Drink", ["G"] = "Adventuring Gear",
        ["GS"] = "Gaming Set", ["HA"] = "Heavy Armor", ["INS"] = "Instrument", ["LA"] = "Light Armor",
        ["M"] = "Melee Weapon", ["MA"] = "Medium Armor", ["MNT"] = "Mount", ["P"] = "Potion",
        ["R"] = "Ranged Weapon", ["RD"] = "Rod", ["RG"] = "Ring", ["S"] = "Shield", ["SC"] = "Scroll",
        ["SCF"] = "Spellcasting Focus", ["SHP"] = "Vehicle", ["SPC"] = "Vehicle", ["ST"] = "Staff",
        ["T"] = "Tools", ["TAH"] = "Tack and Harness", ["TG"] = "Trade Good", ["VEH"] = "Vehicle",
        ["WD"] = "Wand", ["$"] = "Treasure", ["$A"] = "Treasure", ["$C"] = "Treasure",
        ["$G"] = "Treasure",
    };

    /// <summary>
    /// The challenge ratings a label may show: the three fractions, then 0 through 30. Anything
    /// else — <c>"Unknown"</c>, a number instead of a string, a homebrew value — is no CR at all,
    /// which is also what costs a monster its initiative bonus when it has proficiency.
    /// </summary>
    public static readonly HashSet<string> ChallengeRatings =
        new(new[] { "0", "1/8", "1/4", "1/2" }.Concat(Enumerable.Range(1, 30)
            .Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture))), StringComparer.Ordinal);

    /// <summary>
    /// <c>s.replace(/\b[a-z]/g, c => c.toUpperCase())</c>. JavaScript's <c>\b</c> is ASCII: a
    /// boundary sits where one side is <c>[A-Za-z0-9_]</c> and the other is not.
    /// </summary>
    public static string TitleCase(string value)
    {
        var result = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var atBoundary = i == 0 || !IsWordCharacter(value[i - 1]);
            result.Append(c is >= 'a' and <= 'z' && atBoundary ? char.ToUpperInvariant(c) : c);
        }

        return result.ToString();
    }

    private static bool IsWordCharacter(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_';
}
