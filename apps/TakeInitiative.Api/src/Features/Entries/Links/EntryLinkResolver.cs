using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Features.Reference.KnowledgeBase;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// A knowledge-base link's row, as it is <b>now</b> (27b). Everything on it comes from the corpus;
/// nothing is stored on the link.
/// </summary>
/// <param name="ProviderLabel">What the UI calls the provider ("5eTools"), or its key when the provider has gone.</param>
/// <param name="Name">The row's name, "Beholder". Null when the row has gone.</param>
/// <param name="Detail">The muted line, "CR 13 · Large Aberration · MM". Null when the row has gone.</param>
/// <param name="Url">Where the link opens. Null when the row has gone, so there is nowhere to go.</param>
/// <param name="BookTitle">The book's full title, for a tooltip.</param>
/// <param name="ImageUrl">The row's artwork (26g), by url.</param>
/// <param name="HasStatBlock">Whether the provider draws the item in the app, rather than linking out.</param>
/// <param name="Stale">
/// The row is gone, or a prune marked it <c>stale</c>. Either way the link renders as "no longer in
/// your knowledge base" and is still removable, and the two cases read identically on purpose: to
/// the member there is no difference between a row that was deleted and a row that was kept because
/// they linked to it.
/// </param>
public sealed record EntryLinkItem(
    string ProviderLabel,
    string? Name,
    string? Detail,
    string? Url,
    string? BookTitle,
    string? ImageUrl,
    bool HasStatBlock,
    bool Stale);

/// <summary>
/// Resolves an entry's knowledge-base links against the corpus (27b), in <b>one</b> query per
/// request rather than one per link.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why resolving at all.</b> A knowledge-base link stores only <c>provider</c> and
/// <c>itemId</c> — see <see cref="EntryLink"/>. Denormalising the row onto the link would save this
/// class and make a re-ingest unable to correct a url or a page number anywhere, which is the whole
/// reason the corpus moved into Postgres in step 26.
/// </para>
/// <para>
/// <b>Two sources, one query.</b> Most providers keep their rows in <c>knowledge_base_item</c>, so
/// every link naming one of those is answered by a single <see cref="KnowledgeBaseQueries.ByIdsAsync"/>
/// over all of them at once. A provider that is not table-backed — the bundled SRD, which is a
/// catalogue in the assembly — is asked through <see cref="ReferenceCatalog"/> instead, which costs
/// nothing because its answer is already in memory. Validation lets a link name any registered
/// provider (27c), so leaving the second case out would make every <c>srd52</c> link permanently
/// stale.
/// </para>
/// <para>
/// <b>Stale is a state, not a failure.</b> A key with no row, and a row a prune marked, both resolve
/// to <see cref="EntryLinkItem.Stale"/>. Nothing throws and nothing is hidden: the link is still in
/// the list and still removable, because it is the member's and the ingest does not get to erase it.
/// </para>
/// </remarks>
public class EntryLinkResolver(KnowledgeBaseQueries queries, ReferenceCatalog reference)
{
    /// <summary>
    /// Every knowledge-base link in <paramref name="links"/>, resolved, by
    /// <see cref="EntryLink.Id"/>. External links are not in the result: there is nothing to
    /// resolve about them. Empty in, empty out, and no query at all.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, EntryLinkItem>> ResolveAsync(
        IEnumerable<EntryLink> links, CancellationToken ct)
    {
        // The links to resolve, each with its provider and the key it names. The key takes the
        // provider's own spelling of itself where the provider is registered: the table's primary key
        // is case-sensitive, so a link stored as "5eTOOLS" would otherwise miss its row and read as
        // stale. A link missing either half of its key cannot be written by any endpoint, so it is
        // left out rather than guessed at.
        var wanted = new List<(EntryLink Link, IReferenceProvider? Provider, (string Provider, string Id) Key)>();
        foreach (var link in links)
        {
            if (link is { Kind: EntryLinkKind.KnowledgeBase, Provider: { } named, ItemId: { } itemId })
            {
                var provider = reference.Get(named);
                wanted.Add((link, provider, (provider?.Key ?? named, itemId)));
            }
        }

        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, EntryLinkItem>();
        }

        // Which of them keep their rows in the table, and which answer from memory. Asking the
        // catalog for a table-backed provider's missing row would be the per-link query this class
        // exists to avoid, so the two are kept apart.
        var rows = await queries.ByIdsAsync(
            [.. wanted.Where(x => x.Provider is KnowledgeBaseReferenceProvider).Select(x => x.Key).Distinct()],
            ct);
        var byKey = rows.ToDictionary(
            row => (row.Item.Provider, row.Item.Id),
            row => row,
            KeyComparer);

        var resolved = new Dictionary<Guid, EntryLinkItem>();
        foreach (var (link, provider, key) in wanted)
        {
            var label = provider?.Label ?? key.Provider;

            if (byKey.TryGetValue(key, out var row))
            {
                resolved[link.Id] = new EntryLinkItem(
                    ProviderLabel: label,
                    Name: row.Item.Name,
                    Detail: row.Item.Summary.Detail,
                    // A staled row keeps its last-known name and detail — that is what lets the member
                    // recognise which link has gone — but it loses its url and its card. The source
                    // the url points into is no longer in their data, so there is nowhere honest to
                    // send them, and 27's layout says a stale link renders with no ↗ at all.
                    Url: row.Stale ? null : row.Item.Url,
                    BookTitle: row.Item.SourceTitle,
                    ImageUrl: row.Item.ImageUrl,
                    HasStatBlock: !row.Stale && (provider?.HasStatBlocks ?? false),
                    Stale: row.Stale);
                continue;
            }

            // Not in the table. Either the provider is not table-backed — the SRD — or the row has
            // gone. The first is an in-memory lookup; the second is the stale case.
            var item = provider is null or KnowledgeBaseReferenceProvider
                ? null
                : await provider.Find(key.Id, ct);

            resolved[link.Id] = new EntryLinkItem(
                ProviderLabel: label,
                Name: item?.Name,
                Detail: item?.Detail,
                Url: item?.Url,
                BookTitle: item?.BookTitle,
                ImageUrl: null,
                HasStatBlock: item is not null && (provider?.HasStatBlocks ?? false),
                Stale: item is null);
        }

        return resolved;
    }

    /// <summary>
    /// <c>(provider, id)</c> as the table compares it: the provider case-insensitively, because
    /// <see cref="ReferenceCatalog.Get"/> does, and the id exactly, because it is the parser's.
    /// </summary>
    private static readonly IEqualityComparer<(string Provider, string Id)> KeyComparer =
        new ProviderIdComparer();

    private sealed class ProviderIdComparer : IEqualityComparer<(string Provider, string Id)>
    {
        public bool Equals((string Provider, string Id) x, (string Provider, string Id) y)
            => string.Equals(x.Provider, y.Provider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Id, y.Id, StringComparison.Ordinal);

        public int GetHashCode((string Provider, string Id) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Provider),
                StringComparer.Ordinal.GetHashCode(obj.Id));
    }
}
