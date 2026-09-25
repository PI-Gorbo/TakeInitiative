namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// One block of an editor's view of the article after their edit (<c>PUT article</c>).
/// <see cref="Id"/> is an existing block the editor can see, or null for a new block.
/// </summary>
public record ArticleBlockEdit(Guid? Id, string Text, Visibility Visibility);

public enum ArticleMergeError
{
    /// <summary>An id that is not a block of this article, or one the editor cannot see. The two are one error, so the reply leaks nothing.</summary>
    UnknownBlock,
    /// <summary>The same id twice.</summary>
    DuplicateBlock,
    /// <summary>A visibility change by someone other than the block's owner or a DM.</summary>
    VisibilityNotAllowed,
}

public record ArticleMergeResult(IReadOnlyList<ArticleBlock> Blocks, ArticleMergeError? Error = null, Guid? BlockId = null)
{
    public bool Failed => Error is not null;

    public static ArticleMergeResult Fail(ArticleMergeError error, Guid blockId) => new([], error, blockId);
}

/// <summary>
/// Editing an article without seeing all of it (15e.3). The editor sends their view after
/// the edit, and this puts it together with the blocks they cannot see:
/// <list type="bullet">
/// <item>blocks with an id are existing blocks the editor can see, with new text and
/// visibility; blocks without one are new, and the editor owns them;</item>
/// <item>a visible block missing from the edit is removed, and so is a block with blank
/// text;</item>
/// <item>every block the editor cannot see is kept, directly after the block it followed
/// before, or first if it was first. If that block is gone, it goes after the nearest
/// earlier block that is still there;</item>
/// <item>only a block's owner and the DMs change its visibility. A quote's source is kept
/// from the stored block.</item>
/// </list>
/// Pure: the endpoint loads, checks the etag and appends.
/// </summary>
public static class ArticleMerge
{
    public static ArticleMergeResult Merge(
        Entry entry, IReadOnlyList<ArticleBlock> stored, IReadOnlyList<ArticleBlockEdit> edits, Member editor, Func<Guid>? newId = null)
    {
        newId ??= Guid.NewGuid;
        var visibleById = stored
            .Where(b => EntryVisibility.CanSeeBlock(entry, b, editor))
            .ToDictionary(b => b.Id);

        var seen = new HashSet<Guid>();
        var edited = new List<ArticleBlock>();
        foreach (var edit in edits)
        {
            var text = edit.Text.Trim();
            if (edit.Id is not { } id)
            {
                if (text.Length > 0)
                {
                    edited.Add(new ArticleBlock { Id = newId(), Text = text, Visibility = edit.Visibility, OwnerMemberId = editor.MemberId });
                }
                continue;
            }

            if (!visibleById.TryGetValue(id, out var block))
            {
                return ArticleMergeResult.Fail(ArticleMergeError.UnknownBlock, id);
            }
            if (!seen.Add(id))
            {
                return ArticleMergeResult.Fail(ArticleMergeError.DuplicateBlock, id);
            }
            if (block.Visibility != edit.Visibility && editor.Role != Role.DM && block.OwnerMemberId != editor.MemberId)
            {
                return ArticleMergeResult.Fail(ArticleMergeError.VisibilityNotAllowed, id);
            }
            if (text.Length > 0)
            {
                edited.Add(block with { Text = text, Visibility = edit.Visibility });
            }
        }

        // Put each hidden block back after its nearest earlier neighbour that survived. The
        // stored order is walked front to back, so a hidden block's hidden predecessor is
        // always placed before it is needed as an anchor.
        var result = new List<ArticleBlock>(edited);
        for (var i = 0; i < stored.Count; i++)
        {
            var hidden = stored[i];
            if (visibleById.ContainsKey(hidden.Id))
            {
                continue;
            }
            var at = 0;
            for (var j = i - 1; j >= 0; j--)
            {
                var anchor = result.FindIndex(b => b.Id == stored[j].Id);
                if (anchor >= 0)
                {
                    at = anchor + 1;
                    break;
                }
            }
            result.Insert(at, hidden);
        }

        return new ArticleMergeResult(result);
    }

    /// <summary>Whether two versions of an article are the same blocks, in the same order.</summary>
    public static bool SameBlocks(IReadOnlyList<ArticleBlock> a, IReadOnlyList<ArticleBlock> b) => a.SequenceEqual(b);
}
