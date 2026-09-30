namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// SRD 5.2, bundled with the app: every monster has a stat block the app draws itself. A
/// singleton over <see cref="SrdCatalog"/>.
/// </summary>
public class SrdReferenceProvider(SrdCatalog catalog) : IReferenceProvider
{
    public string Key => SrdCatalog.ProviderKey;
    public string Label => catalog.Source.Document;
    public bool HasStatBlocks => true;
    public ReferenceAttribution Attribution => catalog.Attribution;

    /// <summary>In memory: the bundled catalogue, so the task is always already completed.</summary>
    public Task<IReadOnlyList<ReferenceMatch>> Search(string text, int take, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ReferenceMatch>>(ReferenceMatcher.Search(text, catalog.Candidates, take)
            .Select(m => new ReferenceMatch(m.Item.Summary, m.Category, m.Similarity))
            .ToList());

    public Task<ReferenceItem?> Get(string id, CancellationToken ct)
        => Task.FromResult<ReferenceItem?>(catalog.Get(id) is { } monster
            ? new ReferenceItem(monster.Summary, monster.StatBlock, catalog.Attribution)
            : null);

    public Task<ReferenceSummary?> Find(string id, CancellationToken ct) => Task.FromResult<ReferenceSummary?>(catalog.Get(id)?.Summary);
}
