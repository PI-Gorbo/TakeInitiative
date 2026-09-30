namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// The allowlist: every key an index file is permitted to carry, ported from
/// <c>build-5etools-index.mjs</c> lines 36-42.
/// </summary>
/// <remarks>
/// <para>
/// 5eTools' data is copyrighted and is not licensed for reuse. The index is therefore built field by
/// field out of this list — identifiers, a source book and page, a label assembled from enums this
/// repository maps itself, a link, and for a monster three numbers. No rules text, no description,
/// no stat block, no image and no other 5eTools field is ever written, and nothing in the parser
/// copies or spreads one of their objects into a row.
/// </para>
/// <para>
/// The lists are not documentation. <see cref="FiveEToolsParser" /> checks the keys it is about to
/// write against them on every build, so widening what the index carries cannot happen by editing a
/// record: it fails the build until someone changes this file too. Per step 21's notes, that is a
/// decision for the user rather than an implementation detail.
/// </para>
/// </remarks>
public static class FiveEToolsAllowlist
{
    /// <summary>The keys of the index file itself.</summary>
    public static readonly IReadOnlyList<string> TopKeys =
        ["format", "fiveEToolsVersion", "counts", "sources", "items"];

    /// <summary>The keys of one row.</summary>
    public static readonly IReadOnlyList<string> ItemKeys =
        ["id", "name", "category", "source", "page", "url", "label", "stats"];

    /// <summary>The keys of a monster's stats.</summary>
    public static readonly IReadOnlyList<string> StatsKeys = ["ac", "hp", "initiativeBonus"];

    /// <summary>The three categories a row may have, in the order rows are sorted by.</summary>
    public static readonly IReadOnlyList<string> Categories = ["Monster", "Spell", "Item"];

    /// <summary>The page on the reference site each category lives on.</summary>
    public static readonly IReadOnlyDictionary<string, string> Pages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Monster"] = "bestiary",
        ["Spell"] = "spells",
        ["Item"] = "items",
    };
}
