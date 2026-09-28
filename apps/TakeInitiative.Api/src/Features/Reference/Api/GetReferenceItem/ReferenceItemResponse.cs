namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// One reference item in full (20b), for the stat-block card: what the row showed, the stat block,
/// and the attribution the card prints under it. The same for every member of every campaign. A
/// search-only provider's item (5eTools, 21b.5) has no stat block: <see cref="StatBlock"/> is null
/// and the web links out instead of drawing a card.
/// </summary>
public record ReferenceItemResponse
{
    public required ReferenceSummaryResponse Summary { get; init; }
    public required StatBlock? StatBlock { get; init; }
    public required ReferenceAttributionResponse Attribution { get; init; }

    public static ReferenceItemResponse From(IReferenceProvider provider, ReferenceItem item) => new()
    {
        Summary = ReferenceSummaryResponse.From(provider, item.Summary),
        StatBlock = item.StatBlock,
        Attribution = new ReferenceAttributionResponse
        {
            Text = item.Attribution.Text,
            LicenseName = item.Attribution.LicenseName,
            LicenseUrl = item.Attribution.LicenseUrl,
            SourceUrl = item.Attribution.SourceUrl,
        },
    };
}

/// <summary>
/// A reference item's summary. <see cref="Stats"/> is what + Wiki fills for a DM and what 20d's
/// "Use SRD stats" puts in the form, so the web never derives its own.
/// </summary>
public record ReferenceSummaryResponse
{
    public required string Provider { get; init; }
    public required string ProviderLabel { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ReferenceCategory Category { get; init; }
    public required string Detail { get; init; }
    public string? Url { get; init; }
    public required bool HasStatBlock { get; init; }
    public required EntryKind SuggestedKind { get; init; }
    public StatsResponse? Stats { get; init; }

    public static ReferenceSummaryResponse From(IReferenceProvider provider, ReferenceSummary summary) => new()
    {
        Provider = provider.Key,
        ProviderLabel = provider.Label,
        Id = summary.Id,
        Name = summary.Name,
        Category = summary.Category,
        Detail = summary.Detail,
        Url = summary.Url,
        HasStatBlock = provider.HasStatBlocks,
        SuggestedKind = summary.SuggestedKind,
        Stats = summary.Stats is { } stats ? StatsResponse.From(stats) : null,
    };
}

/// <summary>Whom to credit: the statement exactly as the provider's licence asks for it, with its links.</summary>
public record ReferenceAttributionResponse
{
    public required string Text { get; init; }
    public required string LicenseName { get; init; }
    public required string LicenseUrl { get; init; }
    public required string SourceUrl { get; init; }
}
