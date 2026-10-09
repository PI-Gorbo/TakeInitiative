namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// The registered reference providers, by key, in registration order: SRD 5.2 first, then (step 21)
/// the 5eTools index. Step 20b's search provider asks each of them, and the item endpoint and
/// + Wiki find an item's provider here. Scoped, so a later provider may be scoped too.
/// </summary>
public class ReferenceCatalog(IEnumerable<IReferenceProvider> providers)
{
    public IReadOnlyList<IReferenceProvider> Providers { get; } = providers.ToList();

    /// <summary>The provider with <paramref name="key"/> (case-insensitive), or null.</summary>
    public IReferenceProvider? Get(string key)
        => Providers.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The item, or null when the provider or the item is unknown, or the provider is search-only.</summary>
    public Task<ReferenceItem?> GetItem(string provider, string id, CancellationToken ct)
        => Get(provider) is { } p ? p.Get(id, ct) : Task.FromResult<ReferenceItem?>(null);

    /// <summary>The item's summary from any provider, search-only ones included, or null.</summary>
    public Task<ReferenceSummary?> FindItem(string provider, string id, CancellationToken ct)
        => Get(provider) is { } p ? p.Find(id, ct) : Task.FromResult<ReferenceSummary?>(null);
}
