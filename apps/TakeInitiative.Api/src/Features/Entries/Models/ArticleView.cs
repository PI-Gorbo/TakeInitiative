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
        var list = versions.ToList();
        return Changed(entry, list.Select(v => v.Blocks), viewer)
            .Select(c => list[c.Index] with { Blocks = c.Blocks })
            .ToList();
    }

    /// <summary>
    /// The same rule by position: for each version (whole articles, oldest first) whose visible
    /// blocks differ from the version before, its index and those blocks. The history endpoint
    /// uses it to keep its other events in between.
    /// </summary>
    public static IReadOnlyList<(int Index, IReadOnlyList<ArticleBlock> Blocks)> Changed(
        Entry entry, IEnumerable<IReadOnlyList<ArticleBlock>> versions, Member viewer)
    {
        var result = new List<(int, IReadOnlyList<ArticleBlock>)>();
        var previous = ArticleEtag.Of([]);
        var index = 0;
        foreach (var blocks in versions)
        {
            var visible = ArticleView.VisibleBlocks(entry, blocks, viewer);
            var etag = ArticleEtag.Of(visible);
            if (etag != previous)
            {
                result.Add((index, visible));
                previous = etag;
            }
            index++;
        }
        return result;
    }
}
