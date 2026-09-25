using System.Security.Cryptography;
using System.Text;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// The article's etag for one viewer: a hash of the blocks they can see (ids, visibility,
/// text and order). It is not the stream version. If it were, an edit inside a DM secret
/// block would change a player's etag and give them an unexplained 409, which would reveal
/// that the secret exists. Two viewers with the same view get the same etag.
/// </summary>
public static class ArticleEtag
{
    public static string For(Entry entry, Member viewer) => Of(ArticleView.VisibleBlocks(entry, viewer));

    public static string For(Entry entry, IEnumerable<ArticleBlock> blocks, Member viewer)
        => Of(ArticleView.VisibleBlocks(entry, blocks, viewer));

    /// <summary>The hash of exactly these blocks, in this order.</summary>
    public static string Of(IEnumerable<ArticleBlock> visibleBlocks)
    {
        var canonical = new StringBuilder();
        foreach (var block in visibleBlocks)
        {
            // Length-prefixed, so no two different lists can produce the same string.
            canonical
                .Append(block.Id.ToString("N")).Append('|')
                .Append(block.Visibility).Append('|')
                .Append(block.Text.Length).Append(':').Append(block.Text).Append('\n');
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexStringLower(hash.AsSpan(0, 16));
    }
}
