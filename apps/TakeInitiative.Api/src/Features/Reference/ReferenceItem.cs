namespace TakeInitiative.Api.Features.Reference;

/// <summary>What kind of thing a reference item is. Step 20 has monsters only; step 21 adds spells, items and so on.</summary>
public enum ReferenceCategory
{
    Monster,
}

/// <summary>
/// One reference item as a search row shows it and as + Wiki needs it (glossary: Reference item).
/// </summary>
/// <param name="Provider">The provider's <see cref="IReferenceProvider.Key"/>.</param>
/// <param name="Id">The item's id within its provider, stable across rebuilds: "goblin-warrior".</param>
/// <param name="Detail">The muted line after the name: "CR 1/4 · Small Fey".</param>
/// <param name="Url">A link out, for a search-only provider. Null for the SRD, which is shown in the app.</param>
/// <param name="SuggestedKind">The kind of entry + Wiki creates: a monster is a Character.</param>
/// <param name="Stats">
/// The Stats + Wiki fills for a DM, or null. On the summary rather than the stat block so that a
/// search-only provider, which has no stat block, can still offer them.
/// </param>
public record ReferenceSummary(
    string Provider,
    string Id,
    string Name,
    ReferenceCategory Category,
    string Detail,
    string? Url,
    EntryKind SuggestedKind,
    Stats? Stats);

/// <summary>
/// One search result: the item, and how well its name matched, on the same ladder as
/// <c>SearchSql.MatchCategory</c> so the Reference section ranks as the Entries section does.
/// </summary>
/// <param name="Category">0 exact, 1 prefix, 2 word prefix, 3 substring, 4 fuzzy.</param>
/// <param name="Similarity">pg_trgm's <c>word_similarity(query, name)</c>, ported (<see cref="ReferenceMatcher.WordSimilarity"/>).</param>
public record ReferenceMatch(ReferenceSummary Item, int Category, double Similarity);

/// <summary>A reference item in full: the summary, its stat block (null for a search-only provider) and whom to credit.</summary>
public record ReferenceItem(ReferenceSummary Summary, StatBlock? StatBlock, ReferenceAttribution Attribution);

/// <summary>
/// The attribution a provider's licence asks for, shown under every stat-block card. For the SRD
/// it is SRD 5.2's own statement, read from <c>source.json</c>, so the web holds no copy of it.
/// </summary>
public record ReferenceAttribution(string Text, string LicenseName, string LicenseUrl, string SourceUrl);
