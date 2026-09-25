namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// One viewer's view of an article: the blocks they can see (<see cref="EntryVisibility.CanSeeBlock"/>),
/// in order. Every read of an article, its etag, its pushes and its history go through this,
/// so a block outside the viewer's audience is absent everywhere (invariant 5).
/// </summary>
public static class ArticleView
{
    public static IReadOnlyList<ArticleBlock> VisibleBlocks(Entry entry, Member viewer)
        => VisibleBlocks(entry, entry.Article.Blocks, viewer);

    /// <summary>The blocks of <paramref name="blocks"/> (any version of the entry's article) that the viewer can see.</summary>
    public static IReadOnlyList<ArticleBlock> VisibleBlocks(Entry entry, IEnumerable<ArticleBlock> blocks, Member viewer)
        => blocks.Where(b => EntryVisibility.CanSeeBlock(entry, b, viewer)).ToList();
}

/// <summary>One version of an article, for history: when, who, and the whole article after it.</summary>
public record ArticleVersion(DateTimeOffset At, Actor Actor, IReadOnlyList<ArticleBlock> Blocks);

/// <summary>
/// An article's history for one viewer (used by 15g's <c>GET entries/{id}/history</c>): each
/// version lists only the blocks the viewer can see, and a version that changed nothing they
/// can see (its etag equals the one before) is left out. So an edit inside a secret block
/// leaves no trace, not even a timestamp, for someone outside it.
/// </summary>
public static class ArticleHistory
{
    public static IReadOnlyList<ArticleVersion> For(Entry entry, IEnumerable<ArticleVersion> versions, Member viewer)
    {
        var result = new List<ArticleVersion>();
        var previous = ArticleEtag.Of([]);
        foreach (var version in versions)
        {
            var visible = ArticleView.VisibleBlocks(entry, version.Blocks, viewer);
            var etag = ArticleEtag.Of(visible);
            if (etag != previous)
            {
                result.Add(version with { Blocks = visible });
                previous = etag;
            }
        }
        return result;
    }
}
