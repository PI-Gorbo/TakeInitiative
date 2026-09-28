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

    public IReadOnlyList<ReferenceMatch> Search(string text, int take)
        => ReferenceMatcher.Search(text, catalog.Candidates, take)
            .Select(m => new ReferenceMatch(m.Item.Summary, m.Category, m.Similarity))
            .ToList();

    public ReferenceItem? Get(string id)
        => catalog.Get(id) is { } monster
            ? new ReferenceItem(monster.Summary, monster.StatBlock, catalog.Attribution)
            : null;

    public ReferenceSummary? Find(string id) => catalog.Get(id)?.Summary;
}
