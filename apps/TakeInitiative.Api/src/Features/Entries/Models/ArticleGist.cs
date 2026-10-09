using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>
/// The wiki home's one-line summary gist (25g): the first paragraph of the first block with
/// text, as plain text, capped at <see cref="MaxLength"/> characters. Per viewer: only blocks
/// the viewer can see (<see cref="ArticleView.VisibleBlocks(Entry, Member)"/>) and, of those,
/// only ones meant for everyone, so a secret block (DM or Me) never becomes a gist, not even
/// for the DM who can read it (the list is what a DM shows across the table). Mentions read
/// as their text, emphasis markers and escapes are dropped, and code is kept as its content.
/// </summary>
public static class ArticleGist
{
    public const int MaxLength = 140;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().DisableHtml().Build();

    /// <summary>The gist of <paramref name="entry"/>'s article for <paramref name="viewer"/>, or null when there is none.</summary>
    public static string? For(Entry entry, Member viewer)
        => ArticleView.VisibleBlocks(entry, viewer)
            .Where(b => b.Visibility == Visibility.Everyone)
            .Select(b => Of(b.Text))
            .FirstOrDefault(g => g is not null);

    /// <summary>The first paragraph (or heading) of <paramref name="markdown"/> as plain text, capped; null when it has no text.</summary>
    public static string? Of(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return null;

        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var leaf in document.Descendants<LeafBlock>())
        {
            string text;
            if (leaf.Inline is { } inline)
            {
                var sb = new StringBuilder();
                Append(inline, sb);
                text = sb.ToString();
            }
            else if (leaf is CodeBlock code)
            {
                text = code.Lines.ToString();
            }
            else
            {
                continue;
            }

            text = Collapse(text);
            if (text.Length > 0) return Cap(text);
        }
        return null;
    }

    private static void Append(Inline inline, StringBuilder sb)
    {
        switch (inline)
        {
            case LiteralInline literal:
                sb.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                sb.Append(code.Content);
                break;
            case LineBreakInline:
                sb.Append(' ');
                break;
            case LinkInline { IsImage: true }:
                break;
            case LinkInline link when link.Url?.StartsWith(MentionParser.EntryScheme, StringComparison.Ordinal) == true:
                // A mention reads as its text: drop the "@" that starts it.
                if (sb.Length > 0 && sb[^1] == '@') sb.Length--;
                foreach (var child in link) Append(child, sb);
                break;
            case ContainerInline container:
                foreach (var child in container) Append(child, sb);
                break;
        }
    }

    private static string Collapse(string text)
        => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Cut at a word boundary before <see cref="MaxLength"/>, with an ellipsis.</summary>
    private static string Cap(string text)
    {
        if (text.Length <= MaxLength) return text;
        var cut = text[..(MaxLength - 1)];
        var space = cut.LastIndexOf(' ');
        if (space > MaxLength / 2) cut = cut[..space];
        return cut.TrimEnd(' ', ',', ';', ':', '.', '-') + "…";
    }
}
