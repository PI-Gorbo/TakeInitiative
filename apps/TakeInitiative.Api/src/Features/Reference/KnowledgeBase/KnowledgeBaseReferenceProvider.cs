namespace TakeInitiative.Api.Features.Reference.KnowledgeBase;

/// <summary>
/// The 5eTools corpus (26d₂): search-only, and read from <c>knowledge_base_item</c> rather than from
/// a file at startup. Its rows link out to 5etools; the app never shows 5eTools content, so
/// <see cref="Get" /> is always null and there is no stat block. <see cref="Find" /> answers the
/// summary, which holds only stored columns, for + Wiki and the item endpoint.
/// </summary>
/// <remarks>
/// <para>
/// <b>An empty table is not an error.</b> Before 26 the provider disappeared when
/// <c>Reference:FiveETools:IndexPath</c> was unset, which is how a deployment with nothing ingested
/// behaved — one log line, no rows, ⌘K showing the SRD alone. A table with no rows behaves exactly
/// the same way, and it is now a state the app can *say* something about: 26e's browse endpoint
/// answers a total of zero, and 26f's page reads that as "no reference material yet" instead of
/// nothing at all. That was the point of moving the corpus into Postgres (step 26, "Why the index
/// moves into Postgres").
/// </para>
/// <para>
/// <b>It is scoped, not a singleton</b>, because it reads the request's Marten session. The SRD
/// provider is still a singleton over an in-memory catalogue;
/// <see cref="IReferenceProvider" /> makes no promise either way and <c>ReferenceCatalog</c> was
/// already scoped for exactly this.
/// </para>
/// <para>
/// <b>The attribution is not a licence.</b> It names where the row was found and says the link opens
/// elsewhere; the web renders it as a note rather than as a credit, which is why
/// <c>LicenseName</c> and <c>LicenseUrl</c> are empty. Nothing in it is read from 5eTools' data.
/// </para>
/// </remarks>
public class KnowledgeBaseReferenceProvider(KnowledgeBaseQueries queries) : IReferenceProvider
{
    /// <summary>The provider key in a route, in an entry's source and in the table's first key column.</summary>
    public const string ProviderKey = "5etools";

    /// <summary>The site the rows link to, and the attribution's source url.</summary>
    public const string BaseUrl = "https://5e.tools/";

    /// <summary>The note under a row. Not a licence: see the class remarks.</summary>
    public static ReferenceAttribution TheAttribution { get; } = new(
        "Found in the 5eTools index. Opens 5etools in a new tab.", "", "", BaseUrl);

    public string Key => ProviderKey;

    public string Label => "5eTools";

    public bool HasStatBlocks => false;

    public ReferenceAttribution Attribution => TheAttribution;

    /// <summary>
    /// The corpus's best matches for <paramref name="text" />, ranked on <c>SearchSql.MatchCategory</c>'s
    /// ladder in Postgres — see <see cref="KnowledgeBaseQueries" /> for why that ladder and not another.
    /// </summary>
    public async Task<IReadOnlyList<ReferenceMatch>> Search(string text, int take, CancellationToken ct)
    {
        var matches = await queries.SearchAsync(ProviderKey, text, take, ct);
        return [.. matches.Select(match => new ReferenceMatch(match.Item.Summary, match.Rung, match.Similarity))];
    }

    /// <summary>
    /// Always null. There is no stat block in the table and nothing the app is allowed to draw from
    /// it, so the item endpoint answers <see cref="Find" />'s summary with <c>statBlock: null</c>.
    /// </summary>
    public Task<ReferenceItem?> Get(string id, CancellationToken ct) => Task.FromResult<ReferenceItem?>(null);

    public async Task<ReferenceSummary?> Find(string id, CancellationToken ct)
        => (await queries.FindAsync(ProviderKey, id, ct))?.Summary;
}
