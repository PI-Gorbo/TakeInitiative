namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// Reference (20b): asks every registered <see cref="IReferenceProvider"/> (SRD 5.2, and in step
/// 21 the 5eTools index) and merges their matches into one section. It needs no SQL and no
/// connection: reference items are the same for every campaign and every member, so there is no
/// visibility to apply. Only the link from an entry to an item is guarded (<see cref="EntrySources"/>).
/// <para>
/// The section is filled only for an unscoped query (an <c>@</c> query is entries only) of two or
/// more characters: one letter would match half the SRD.
/// </para>
/// </summary>
public class ReferenceSearchProvider(ReferenceCatalog catalog) : ISearchProvider
{
    public IReadOnlyList<SearchSectionKey> Sections => [SearchSectionKey.Reference];

    public async Task<IReadOnlyList<SearchSection>> SearchAsync(SearchQuery query, SearchContext context, CancellationToken ct)
    {
        if (!context.Wanted.Contains(SearchSectionKey.Reference)
            || query.Scope != SearchScope.All
            || query.SingleCharacter)
        {
            return [];
        }

        // Ask each provider in registration order and keep that order: the gather is indexed by
        // the provider's position, not by whichever answers first.
        var answers = new IReadOnlyList<ReferenceMatch>[catalog.Providers.Count];
        for (var i = 0; i < catalog.Providers.Count; i++)
        {
            answers[i] = await catalog.Providers[i].Search(query.Text, context.Take + 1, ct);
        }

        // Each provider ranks its own matches; the merge keeps the same ladder across them, then
        // prefers the earlier provider (SRD before 5eTools) on a tie, then the name.
        var matches = catalog.Providers
            .SelectMany((provider, order) => answers[order]
                .Select(match => (Provider: provider, Order: order, Match: match)))
            .OrderBy(m => m.Match.Category)
            .ThenByDescending(m => m.Match.Similarity)
            .ThenBy(m => m.Order)
            .ThenBy(m => m.Match.Item.Name.Length)
            .ThenBy(m => m.Match.Item.Name, StringComparer.Ordinal)
            .Take(context.Take + 1)
            .ToList();
        if (matches.Count == 0)
        {
            return [];
        }

        return
        [
            new SearchSection
            {
                Key = SearchSectionKey.Reference,
                HasMore = matches.Count > context.Take,
                Hits = [.. matches.Take(context.Take).Select(m => Hit(m.Provider, m.Match.Item))],
            },
        ];
    }

    private static SearchHit Hit(IReferenceProvider provider, ReferenceSummary item) => new()
    {
        Kind = SearchHitKind.Reference,
        Reference = new SearchReferenceHit
        {
            Provider = provider.Key,
            ProviderLabel = provider.Label,
            Id = item.Id,
            Name = item.Name,
            Category = item.Category,
            Detail = item.Detail,
            Url = item.Url,
            HasStatBlock = provider.HasStatBlocks,
            SuggestedKind = item.SuggestedKind,
        },
    };
}
