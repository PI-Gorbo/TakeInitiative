namespace TakeInitiative.KnowledgeBase;

/// <summary>
/// One row of the knowledge base: everything the app is allowed to know about a piece of reference
/// material it does not own.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of it — an id, a name, a category, a source book and page, a short label built
/// from enums, a link out, and for a monster three numbers. There is no rules text, no description,
/// no stat block and no image. The row's purpose is to be findable, linkable and to link out; a DM
/// who wants to read the thing follows <see cref="Url" /> to the people who wrote it.
/// </para>
/// <para>
/// The shape is not an implementation detail. It is the allowlist
/// (<see cref="FiveETools.FiveEToolsAllowlist.ItemKeys" />) that the 5eTools parser validates every
/// row against, so a field added here without a decision behind it fails the build rather than
/// quietly widening what the index carries.
/// </para>
/// </remarks>
public sealed record KnowledgeBaseItem
{
    /// <summary>
    /// <c>monster_beholder_mm</c> — the category, the name as a slug and the source book. This is
    /// the table's primary key (26c) and what a user's entry link points at (step 27), so it has to
    /// be stable across a re-ingest and identical to what the Node script produced.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The name, exactly as the source spells it.</summary>
    public required string Name { get; init; }

    /// <summary><c>Monster</c>, <c>Spell</c> or <c>Item</c>.</summary>
    public required string Category { get; init; }

    /// <summary>The source book's abbreviation, such as <c>MM</c>.</summary>
    public required string Source { get; init; }

    /// <summary>The page in that book, where the source states one.</summary>
    public int? Page { get; init; }

    /// <summary>The deep link out to the reference site.</summary>
    public required string Url { get; init; }

    /// <summary>
    /// The muted line under the name: <c>CR 13 · Large Aberration</c>, <c>Level 3 Evocation</c>,
    /// <c>Uncommon Wondrous Item</c>. Built from enums this repository maps itself, never from the
    /// source's own prose. Null where nothing could be built.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// The three numbers a monster needs to be dropped into an encounter. Null for every spell and
    /// item, and null for a monster whose AC, hit points or initiative could not all be read.
    /// </summary>
    public KnowledgeBaseItemStats? Stats { get; init; }
}

/// <summary>
/// A monster's rollable numbers. All three or none: half a stat block would be worse than none,
/// because the encounter builder would trust it.
/// </summary>
/// <param name="Ac">Armour class.</param>
/// <param name="Hp">Hit points as dice — <c>10d10+30</c>, always <c>NdM</c> with at most one modifier.</param>
/// <param name="InitiativeBonus">The bonus to add to an initiative roll.</param>
public sealed record KnowledgeBaseItemStats(int Ac, string Hp, int InitiativeBonus);
