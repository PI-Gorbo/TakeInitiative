namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// An entry's article (glossary: Article): its blocks, in order. Ordinary text stays one
/// block until a secret block or a quote splits it.
/// </summary>
public record Article
{
    public IReadOnlyList<ArticleBlock> Blocks { get; init; } = [];

    public const int MaxBlocks = 200;
    public const int MaxCharacters = 50_000;
}

/// <summary>
/// One part of an article (glossary: Block): ordinary text, a secret block or a quote.
/// <see cref="Text"/> is markdown and may contain mentions. Who sees it is
/// <c>Audience.Of(Visibility, OwnerMemberId)</c>, inside the entry's own audience
/// (<see cref="EntryVisibility.CanSeeBlock"/>).
/// <list type="bullet">
/// <item>An ordinary block is <c>Everyone</c> with no <see cref="Quote"/>.</item>
/// <item>A secret block is <c>DM</c> or <c>Me</c>, relative to its owner.</item>
/// <item>A quote has <see cref="Quote"/> set. Its owner is the quoted note's author and its
/// visibility the note's audience when it was promoted, so the quote reaches exactly the
/// people who could read the note.</item>
/// </list>
/// The owner of any other block is the member who wrote it.
/// </summary>
public record ArticleBlock
{
    public Guid Id { get; init; }
    public string Text { get; init; } = "";
    public Visibility Visibility { get; init; }
    public Guid OwnerMemberId { get; init; }
    public ArticleQuote? Quote { get; init; }
}

/// <summary>
/// Where a quote came from (glossary: Quote). Set by promote and never changed by an edit.
/// </summary>
public record ArticleQuote
{
    public Guid NoteId { get; init; }
    public Guid SessionId { get; init; }
    public int SessionNumber { get; init; }
    public Guid AuthorMemberId { get; init; }
    public Guid PromotedByMemberId { get; init; }
    /// <summary>Kept at Postgres's precision, like <see cref="SessionNote.PostedAt"/>.</summary>
    public DateTimeOffset PromotedAt { get; init; }
}

public static class Microseconds
{
    /// <summary>
    /// Drops sub-microsecond ticks, so a timestamp equals itself after a round trip through a
    /// Postgres <c>timestamptz</c> (.NET ticks are 100 ns on Linux).
    /// </summary>
    public static DateTimeOffset Truncate(DateTimeOffset value) => new(value.Ticks - value.Ticks % 10, value.Offset);
}
