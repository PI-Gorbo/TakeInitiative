namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// The file <c>scripts/5etools/build-5etools-index.mjs</c> writes (21a.3), as read. It holds only
/// allowlisted index fields: names, categories, books, pages, short labels, links and, for
/// monsters, three Stats numbers. No 5eTools text.
/// </summary>
/// <param name="Format">1. Anything else fails startup.</param>
/// <param name="Sources">Each book abbreviation the rows use, mapped to its title.</param>
public record FiveEToolsIndex(
    int Format,
    string? FiveEToolsVersion,
    IReadOnlyDictionary<string, string>? Sources,
    IReadOnlyList<FiveEToolsRow>? Items)
{
    public const int CurrentFormat = 1;
}

/// <summary>One row of the index: <c>id, name, category, source, page, url, label, stats</c>.</summary>
/// <param name="Id">"monster_goblin-boss_xmm", stable across rebuilds.</param>
/// <param name="Source">The book's abbreviation: "MM".</param>
/// <param name="Url">The deep link to the item's page on 5etools.</param>
/// <param name="Label">"CR 13 · Large Aberration", "Level 3 Evocation", "Uncommon Wondrous Item", or null.</param>
/// <param name="Stats">Monsters only, and only when all three resolved.</param>
public record FiveEToolsRow(
    string Id,
    string Name,
    ReferenceCategory Category,
    string Source,
    int? Page,
    string Url,
    string? Label,
    FiveEToolsStats? Stats);

/// <summary>A monster's AC, hit dice ("19d10+76") and initiative bonus.</summary>
public record FiveEToolsStats(int? Ac, string? Hp, int? InitiativeBonus);
