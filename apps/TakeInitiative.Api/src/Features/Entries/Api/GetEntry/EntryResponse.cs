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

    /// <summary>The entry as <paramref name="viewer"/> sees it. The article is redacted for them.</summary>
    public static EntryResponse From(Entry entry, Member viewer) => new()
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
    };
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
