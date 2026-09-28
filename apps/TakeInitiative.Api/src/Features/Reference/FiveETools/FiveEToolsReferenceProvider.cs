namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// The 5eTools index (21b.3): search-only. Its rows link out to 5etools; the app never shows 5eTools
/// content, so <see cref="Get"/> is always null and there is no stat block. <see cref="Find"/>
/// answers the summary, which holds only index fields, for + Wiki and the item endpoint. When the
/// index did not load it answers nothing at all, so the Reference section is step 20's.
/// </summary>
public class FiveEToolsReferenceProvider(FiveEToolsCatalog catalog) : IReferenceProvider
{
    public string Key => FiveEToolsCatalog.ProviderKey;
    public string Label => "5eTools";
    public bool HasStatBlocks => false;
    public ReferenceAttribution Attribution => catalog.Attribution;

    public IReadOnlyList<ReferenceMatch> Search(string text, int take)
        => catalog.IsOn
            ? ReferenceMatcher.Search(text, catalog.Candidates, take)
                .Select(m => new ReferenceMatch(m.Item, m.Category, m.Similarity))
                .ToList()
            : [];

    public ReferenceItem? Get(string id) => null;

    public ReferenceSummary? Find(string id) => catalog.Find(id);
}
