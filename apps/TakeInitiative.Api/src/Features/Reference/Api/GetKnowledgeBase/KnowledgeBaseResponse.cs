using TakeInitiative.Api.Features.Reference.KnowledgeBase;

namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// One page of the knowledge base (26e): the rows, how many there are in total under the filters, and
/// the counts the two filters need beside their values.
/// </summary>
public record KnowledgeBaseResponse
{
    /// <summary>The page's rows, in the order they should be shown.</summary>
    public required KnowledgeBaseItemResponse[] Items { get; init; }

    /// <summary>How many rows match the filters, on every page — what "2,847 items" reads from.</summary>
    public required int Total { get; init; }

    /// <summary>What choosing each filter value would show.</summary>
    public required KnowledgeBaseFacetsResponse Facets { get; init; }
}

/// <summary>
/// One row of the knowledge base as the page lists it. Everything here is an identifier, a caption or
/// a link: there is no rules text, no description and no stat block, because the table holds none.
/// </summary>
public record KnowledgeBaseItemResponse
{
    /// <summary>The corpus's key: <c>5etools</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>What the UI calls that corpus: <c>5eTools</c>. Null for a provider that is no longer registered.</summary>
    public string? ProviderLabel { get; init; }

    /// <summary>The row's id within its provider, stable across a re-ingest: <c>monster_beholder_mm</c>.</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required ReferenceCategory Category { get; init; }

    /// <summary>The muted line under the name: <c>CR 13 · Large Aberration</c>. Null where the parser built none.</summary>
    public string? Label { get; init; }

    /// <summary>The source book's abbreviation, which is also the book filter's value: <c>MM</c>.</summary>
    public required string Book { get; init; }

    /// <summary>The book's full title, for a tooltip: <c>Monster Manual (2014)</c>. Null where the source names none.</summary>
    public string? BookTitle { get; init; }

    /// <summary>The page in that book, where the source states one.</summary>
    public int? Page { get; init; }

    /// <summary>Where the row links out to. A tap on the row opens this in a new tab.</summary>
    public required string Url { get; init; }

    /// <summary>The row's artwork, served by 5eTools' CDN and never stored by us (26g). Null until then.</summary>
    public string? ImageUrl { get; init; }

    public static KnowledgeBaseItemResponse From(KnowledgeBaseItemRow row, string? providerLabel) => new()
    {
        Provider = row.Provider,
        ProviderLabel = providerLabel,
        Id = row.Id,
        Name = row.Name,
        Category = row.Category,
        Label = row.Label,
        Book = row.SourceBook,
        BookTitle = row.SourceTitle,
        Page = row.Page,
        Url = row.Url,
        ImageUrl = row.ImageUrl,
    };
}

/// <summary>
/// The filter counts. Each list drops its own filter and keeps the other, so the number beside a chip
/// is what choosing it would show rather than what the current page holds.
/// </summary>
public record KnowledgeBaseFacetsResponse
{
    /// <summary>A count per category, under the current book and search.</summary>
    public required KnowledgeBaseCategoryFacetResponse[] Categories { get; init; }

    /// <summary>A count per source book, under the current category and search, by abbreviation.</summary>
    public required KnowledgeBaseBookFacetResponse[] Books { get; init; }
}

public record KnowledgeBaseCategoryFacetResponse
{
    public required ReferenceCategory Category { get; init; }
    public required int Count { get; init; }
}

public record KnowledgeBaseBookFacetResponse
{
    /// <summary>The book's abbreviation, and the value to send back as <c>book</c>.</summary>
    public required string Book { get; init; }

    /// <summary>The book's full title, where the source names one.</summary>
    public string? BookTitle { get; init; }

    public required int Count { get; init; }
}
