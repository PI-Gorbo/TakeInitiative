using TakeInitiative.Api.Features.Entries;

namespace TakeInitiative.Api.Features.Reference.KnowledgeBase;

/// <summary>
/// One <c>knowledge_base_item</c> row as the API reads it (26d₂): the columns the ingest wrote, and
/// nothing else. It is the read side of the package's <c>KnowledgeBaseRow</c> — the write side keeps
/// the content hash and the batch, which no reader has any use for.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Summary" /> is the whole point of the record: ⌘K's row, the item endpoint and + Wiki
/// all want a <see cref="ReferenceSummary" />, and the mapping from a stored row to one has to be in
/// exactly one place. It is the mapping <c>FiveEToolsCatalog.Summarise</c> did from the file-backed
/// index, moved here unchanged, so a row that came out of Postgres reads identically to the one that
/// came out of the JSON file in 21b.
/// </para>
/// <para>
/// <b>There is no <c>detail</c> column and no second string.</b> <c>label</c> is the muted row line
/// as the parser emitted it ("CR 13 · Large Aberration"); the summary's <c>Detail</c> appends the
/// source book to it, which is what the web's <c>referenceHitLine</c> splits back apart.
/// </para>
/// </remarks>
public sealed record KnowledgeBaseItemRow
{
    /// <summary>The corpus: <c>5etools</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>The parser's id, and half the primary key: <c>monster_beholder_mm</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The name, exactly as the source spells it.</summary>
    public required string Name { get; init; }

    public required ReferenceCategory Category { get; init; }

    /// <summary>The source book's abbreviation: <c>MM</c>.</summary>
    public required string SourceBook { get; init; }

    /// <summary>That book's full title, or null where the source's catalogue names none.</summary>
    public string? SourceTitle { get; init; }

    /// <summary>The page in that book, where the source states one.</summary>
    public int? Page { get; init; }

    /// <summary>The muted line under the name: <c>CR 13 · Large Aberration</c>. Null where the parser built none.</summary>
    public string? Label { get; init; }

    /// <summary>The deep link out.</summary>
    public required string Url { get; init; }

    /// <summary>The row's artwork, by URL, from 26g. Null everywhere until then.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>A monster's three rollable numbers, or null for a spell, an item, or a monster missing any of them.</summary>
    public Stats? Stats { get; init; }

    /// <summary>Where the item is printed, for an entry's source line: <c>MM p. 28</c>, or just <c>MM</c>.</summary>
    public string Book => Page is { } page ? $"{SourceBook} p. {page}" : SourceBook;

    /// <summary>
    /// What a ⌘K row and + Wiki show of this row. Only stored columns reach it; there is no rules
    /// text, no description and no stat block, because there is none in the table to begin with.
    /// </summary>
    public ReferenceSummary Summary => new(
        Provider: Provider,
        Id: Id,
        Name: Name,
        Category: Category,
        Detail: string.IsNullOrWhiteSpace(Label) ? SourceBook : $"{Label} · {SourceBook}",
        Url: Url,
        SuggestedKind: Category switch
        {
            ReferenceCategory.Monster => EntryKind.Character,
            ReferenceCategory.Item => EntryKind.Item,
            _ => EntryKind.Other,
        },
        Stats: Stats,
        Book: Book,
        BookTitle: SourceTitle);
}

/// <summary>
/// A row and where the match ladder put it: the shape <see cref="KnowledgeBaseReferenceProvider" />
/// turns into a <see cref="ReferenceMatch" />.
/// </summary>
/// <param name="Rung">0 exact, 1 prefix, 2 word prefix, 3 substring, 4 fuzzy — <c>SearchSql.MatchCategory</c>'s ladder.</param>
/// <param name="Similarity">
/// <c>word_similarity(query, folded name)</c>, and 1 for an exact match, which is what
/// <c>ReferenceMatcher</c> gives the SRD's rows so the two providers' numbers are comparable when
/// the Reference section merges them.
/// </param>
public sealed record KnowledgeBaseMatchRow(KnowledgeBaseItemRow Item, int Rung, double Similarity);

/// <summary>One page of the browse query (26e), with the total the page came out of.</summary>
public sealed record KnowledgeBasePage(IReadOnlyList<KnowledgeBaseItemRow> Items, int Total);

/// <summary>
/// One value of one filter and how many rows carry it, with the other filter still applied — so the
/// count beside "Monsters" is what choosing Monsters would show, not what it shows now.
/// </summary>
/// <param name="Value">The category (<c>Monster</c>) or the source book (<c>MM</c>).</param>
/// <param name="Title">The book's full title, for a book facet. Always null for a category.</param>
/// <param name="Count">How many rows.</param>
public sealed record KnowledgeBaseFacet(string Value, string? Title, int Count);

/// <summary>The counts the browse page's two filters need, and the total for the page itself.</summary>
public sealed record KnowledgeBaseFacets(
    int Total,
    IReadOnlyList<KnowledgeBaseFacet> Categories,
    IReadOnlyList<KnowledgeBaseFacet> Books);
