namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// Runs the registered providers for one viewer and puts their sections in the order ⌘K shows
/// them. Wiki results come first (§11), which is why <c>AddSearch</c> registers
/// <see cref="WikiSearchProvider"/> before the others, but the order here does not depend on
/// that: it is the section order, so a provider added later cannot move an existing section.
/// </summary>
public class SearchService(IEnumerable<ISearchProvider> providers)
{
    /// <summary>The order the sections are shown in. Step 18 added Combats and step 20 Reference, last so wiki results come first (§11).</summary>
    private static readonly SearchSectionKey[] Order =
    [
        SearchSectionKey.Entries, SearchSectionKey.Notes, SearchSectionKey.Images, SearchSectionKey.Sessions,
        SearchSectionKey.Combats, SearchSectionKey.Reference,
    ];

    public async Task<SearchSection[]> SearchAsync(SearchQuery query, SearchContext context, CancellationToken ct)
    {
        var found = new List<SearchSection>();
        // One connection for the whole search, opened on first use and closed here: the providers run
        // one after another, so the four or five statements of an all-sections search have no reason
        // to take a connection from the pool each.
        await using var connection = new SearchConnection(context.Session);
        context = context with { Connection = connection };

        // One after another on one session: a Marten session is not thread-safe, and a provider
        // that found nothing costs one round trip at most.
        foreach (var provider in providers)
        {
            if (!provider.Sections.Any(context.Wanted.Contains))
            {
                continue;
            }
            found.AddRange(await provider.SearchAsync(query, context, ct));
        }

        return [.. Order
            .Select(key => found.FirstOrDefault(s => s.Key == key))
            .Where(section => section is { Hits.Length: > 0 })
            .Select(section => section!)];
    }
}
