using System.Text.Json.Serialization;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// One entry, for its page and as every entry write's response: the fields of
/// <see cref="EntrySummaryResponse"/>, plus the caller's view of the article (15e) and the
/// stats from 15g. It is per viewer: every write answers with the caller's view.
/// Flat rather than derived from the summary: a derived record with no fields of its own
/// generates <c>Summary &amp; Record&lt;string, never&gt;</c> in <c>schema.d.ts</c>, which
/// makes every field <c>never</c>.
/// </summary>
public record EntryResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required EntryKind Kind { get; init; }
    public required string[] Aliases { get; init; }
    public required Visibility Visibility { get; init; }
    public required EditAccess EditAccess { get; init; }
    public required Guid CreatorMemberId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    /// <summary>The member whose player character this is, if any (15g).</summary>
    public Guid? ClaimedByMemberId { get; init; }
    /// <summary>Every entry merged into this one (15g): mentions of these ids mean this entry.</summary>
    public required Guid[] MergedFromIds { get; init; }
    /// <summary>The article as the caller can see it (15e): hidden blocks are absent.</summary>
    public required ArticleResponse Article { get; init; }
    /// <summary>
    /// The stats, when there are some and the caller may read them (<see cref="EntryStats"/>):
    /// on a claimed entry everyone who sees it, on an unclaimed one the DMs only. Otherwise
    /// absent, so "none" and "not for you" look the same.
    /// </summary>
    public StatsResponse? Stats { get; init; }
    /// <summary>
    /// The reference item the entry was made from (20b), when there is one and the caller may read
    /// it (<see cref="EntrySources"/>, the same rule as <see cref="Stats"/>). Otherwise the key is left out, so
    /// a player's JSON does not even say that sources exist.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EntrySourceResponse? Source { get; init; }
    /// <summary>
    /// The links the caller may read (27b), oldest first, with each knowledge-base link resolved
    /// against the corpus. The rule is <see cref="EntryLinks"/>' — the same one as
    /// <see cref="Source"/> — so on an unclaimed Character this is empty for anyone but a DM, and
    /// "no links" and "not your links" look the same.
    /// </summary>
    public required EntryLinkResponse[] Links { get; init; }

    /// <summary>
    /// The entry as <paramref name="viewer"/> sees it. The article, the stats, the source and the
    /// links are redacted for them; <paramref name="reference"/> names the source's provider and
    /// item, and <paramref name="links"/> resolves the knowledge-base links in one query.
    /// </summary>
    public static async Task<EntryResponse> From(
        Entry entry, Member viewer, ReferenceCatalog reference, EntryLinkResolver links, CancellationToken ct)
    {
        var readable = EntryLinks.For(entry, viewer);
        var items = await links.ResolveAsync(readable, ct);
        return new EntryResponse
        {
            Id = entry.Id,
            Name = entry.Name,
            Kind = entry.Kind,
            Aliases = [.. entry.Aliases],
            Visibility = entry.Visibility,
            EditAccess = entry.EditAccess,
            CreatorMemberId = entry.CreatorMemberId,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            ClaimedByMemberId = entry.ClaimedByMemberId,
            MergedFromIds = entry.MergedFromIds,
            Article = ArticleResponse.From(entry, viewer),
            Stats = EntryStats.For(entry, viewer) is { } stats ? StatsResponse.From(stats) : null,
            Source = EntrySources.For(entry, viewer) is { } source ? await EntrySourceResponse.From(source, reference, ct) : null,
            Links = [.. readable.Select(link => EntryLinkResponse.From(link, items.GetValueOrDefault(link.Id)))],
        };
    }
}

/// <summary>
/// One link on an entry (27b), as its reader sees it. The two kinds share one shape, flattened the
/// way <see cref="EntryChange"/> is: <see cref="Kind"/> says which fields are set.
/// <list type="bullet">
/// <item><c>External</c>: <see cref="Label"/> and <see cref="Url"/>, exactly as they were typed.
/// The label is never markdown, and the web opens the url with
/// <c>rel="noopener noreferrer"</c>.</item>
/// <item><c>KnowledgeBase</c>: <see cref="Provider"/> and <see cref="ItemId"/> are the stored link;
/// everything else is read from the corpus on this request, so a re-ingest that corrects a page
/// number shows here without touching the entry. <see cref="Stale"/> means the row has gone or a
/// prune marked it, and then <see cref="Url"/> and <see cref="Name"/> are null — the link renders
/// as "no longer in your knowledge base" and is still removable.</item>
/// </list>
/// </summary>
public record EntryLinkResponse
{
    public required Guid Id { get; init; }
    public required EntryLinkKind Kind { get; init; }
    public required DateTimeOffset AddedAt { get; init; }
    public required Guid AddedByMemberId { get; init; }
    /// <summary>Where the link opens. The member's url for an external link, the row's for a knowledge-base one, null when that row has gone.</summary>
    public string? Url { get; init; }
    /// <summary>The member's own label, on an external link only. Never rendered as markdown.</summary>
    public string? Label { get; init; }
    /// <summary>The provider's key, on a knowledge-base link: <c>5etools</c>.</summary>
    public string? Provider { get; init; }
    /// <summary>What the UI calls that provider, or its key when the provider has gone from the app.</summary>
    public string? ProviderLabel { get; init; }
    /// <summary>The row's id within its provider: <c>monster_beholder_mm</c>.</summary>
    public string? ItemId { get; init; }
    /// <summary>The row's name now, "Beholder". Null when it has gone.</summary>
    public string? Name { get; init; }
    /// <summary>The muted line under the name, "CR 13 · Large Aberration · MM". Null when the row has gone.</summary>
    public string? Detail { get; init; }
    /// <summary>The book's full title, for the row's tooltip.</summary>
    public string? BookTitle { get; init; }
    /// <summary>The row's artwork (26g), by url.</summary>
    public string? ImageUrl { get; init; }
    /// <summary>Whether the web can open the item's card in the app rather than a new tab: its provider draws stat blocks and the row is still there.</summary>
    public bool HasStatBlock { get; init; }
    /// <summary>The knowledge-base row is gone, or a prune marked it. Always false for an external link.</summary>
    public required bool Stale { get; init; }

    /// <summary>
    /// <paramref name="link"/> as its reader sees it. <paramref name="item"/> is
    /// <see cref="EntryLinkResolver"/>'s answer for a knowledge-base link, and null for an external
    /// one — or for a knowledge-base link the resolver was not given, which reads as stale rather
    /// than as a link with no destination.
    /// </summary>
    public static EntryLinkResponse From(EntryLink link, EntryLinkItem? item) => new()
    {
        Id = link.Id,
        Kind = link.Kind,
        AddedAt = link.AddedAt,
        AddedByMemberId = link.AddedByMemberId,
        Url = link.Kind == EntryLinkKind.External ? link.Url : item?.Url,
        Label = link.Kind == EntryLinkKind.External ? link.Label : null,
        Provider = link.Provider,
        ProviderLabel = link.Kind == EntryLinkKind.KnowledgeBase ? item?.ProviderLabel ?? link.Provider : null,
        ItemId = link.ItemId,
        Name = item?.Name,
        Detail = item?.Detail,
        BookTitle = item?.BookTitle,
        ImageUrl = item?.ImageUrl,
        HasStatBlock = item?.HasStatBlock ?? false,
        Stale = link.Kind == EntryLinkKind.KnowledgeBase && (item?.Stale ?? true),
    };
}

/// <summary>
/// Where an entry came from (20b): a reference item. <see cref="Name"/> and
/// <see cref="ProviderLabel"/> are read from the reference data now, so a rebuild's spelling
/// shows; <see cref="Name"/> is null when the item has gone from the data, and the label falls
/// back to the provider's key when the provider has.
/// </summary>
public record EntrySourceResponse
{
    public required string Provider { get; init; }
    public required string ProviderLabel { get; init; }
    public required string ExternalId { get; init; }
    public string? Name { get; init; }
    public required string Url { get; init; }
    /// <summary>Where the item is printed, "MM p. 28", while it is still in the data. Null for the SRD.</summary>
    public string? Detail { get; init; }
    /// <summary>The book's full title, "Monster Manual (2014)", for the source line's tooltip. Null for the SRD.</summary>
    public string? BookTitle { get; init; }
    /// <summary>Whether the web can link to the item's stat-block card: its provider draws them, and the item is still in the data.</summary>
    public required bool HasStatBlock { get; init; }

    public static async Task<EntrySourceResponse> From(EntrySource source, ReferenceCatalog reference, CancellationToken ct)
    {
        var provider = reference.Get(source.Provider);
        var item = provider is null ? null : await provider.Find(source.ExternalId, ct);
        return new EntrySourceResponse
        {
            Provider = source.Provider,
            ProviderLabel = provider?.Label ?? source.Provider,
            ExternalId = source.ExternalId,
            Name = item?.Name,
            Url = source.Url,
            Detail = item?.Book,
            BookTitle = item?.BookTitle,
            HasStatBlock = item is not null && provider!.HasStatBlocks,
        };
    }
}

/// <summary>A Character's stat line: two dice expressions and an armour class, each optional.</summary>
public record StatsResponse
{
    public string? InitiativeRoll { get; init; }
    public string? MaxHp { get; init; }
    public int? Ac { get; init; }

    public static StatsResponse From(Stats stats) => new()
    {
        InitiativeRoll = stats.InitiativeRoll,
        MaxHp = stats.MaxHp,
        Ac = stats.Ac,
    };
}

/// <summary>
/// An article as one viewer sees it: the blocks they can see, in order, and the etag of that
/// view, which <c>PUT article</c> sends back. Blocks they cannot see are absent, with no
/// placeholder and no count (invariant 5).
/// </summary>
public record ArticleResponse
{
    public required string Etag { get; init; }
    public required ArticleBlockResponse[] Blocks { get; init; }

    public static ArticleResponse From(Entry entry, Member viewer)
    {
        var blocks = ArticleView.VisibleBlocks(entry, viewer);
        return new ArticleResponse
        {
            Etag = ArticleEtag.Of(blocks),
            Blocks = blocks.Select(ArticleBlockResponse.From).ToArray(),
        };
    }
}

/// <summary>
/// One block. An ordinary block is <c>Everyone</c> with no quote; a secret block is <c>DM</c>
/// or <c>Me</c>, relative to <see cref="OwnerMemberId"/>; a quote has <see cref="Quote"/>.
/// </summary>
public record ArticleBlockResponse
{
    public required Guid Id { get; init; }
    public required string Text { get; init; }
    public required Visibility Visibility { get; init; }
    public required Guid OwnerMemberId { get; init; }
    public QuoteResponse? Quote { get; init; }

    public static ArticleBlockResponse From(ArticleBlock block) => new()
    {
        Id = block.Id,
        Text = block.Text,
        Visibility = block.Visibility,
        OwnerMemberId = block.OwnerMemberId,
        Quote = block.Quote is { } q ? QuoteResponse.From(q) : null,
    };
}

/// <summary>Where a quote came from: its note (link with <c>?note=</c>), session and author, and who promoted it when.</summary>
public record QuoteResponse
{
    public required Guid NoteId { get; init; }
    public required Guid SessionId { get; init; }
    public required int SessionNumber { get; init; }
    public required Guid AuthorMemberId { get; init; }
    public required Guid PromotedByMemberId { get; init; }
    public required DateTimeOffset PromotedAt { get; init; }

    public static QuoteResponse From(ArticleQuote quote) => new()
    {
        NoteId = quote.NoteId,
        SessionId = quote.SessionId,
        SessionNumber = quote.SessionNumber,
        AuthorMemberId = quote.AuthorMemberId,
        PromotedByMemberId = quote.PromotedByMemberId,
        PromotedAt = quote.PromotedAt,
    };
}
