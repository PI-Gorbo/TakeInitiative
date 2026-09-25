using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TakeInitiative.Api.Features.Entries;

/// <summary>One mention in a text: the entry it links to and the text it shows.</summary>
public record Mention(Guid EntryId, string Text);

/// <summary>
/// Finds the mentions in a session note's (or, from 15e, an article block's) markdown.
/// A mention is an unescaped <c>@</c> directly followed by an inline link whose destination is
/// <c>entry:</c> plus a GUID in its 36-character form (<c>@[text](entry:&lt;guid&gt;)</c>).
/// <para>
/// The text is parsed as CommonMark with raw HTML off, the dialect of the web's markdown-it
/// renderer, so what counts as a link, as code, or as an escape is the same on both sides:
/// links inside code spans or code blocks are not links, nested balanced brackets belong
/// to the link text, and <c>\@</c> is a literal "@" that starts nothing. The case list in
/// <c>Fixtures/mentions.json</c> is run by both this parser's tests and the web's, so the
/// two cannot drift apart.
/// </para>
/// <para>
/// <see cref="Mention.Text"/> is the link text as plain text: emphasis markers dropped,
/// escapes resolved, code spans kept as their content, line breaks as spaces.
/// </para>
/// </summary>
public static class MentionParser
{
    public const string EntryScheme = "entry:";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().DisableHtml().Build();

    /// <summary>Every mention in <paramref name="text"/>, in order, repeats included.</summary>
    public static IReadOnlyList<Mention> Parse(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains(EntryScheme, StringComparison.Ordinal))
        {
            return [];
        }

        var document = Markdown.Parse(text, Pipeline);
        var mentions = new List<Mention>();
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.IsImage || link.IsAutoLink) continue;
            if (!TryEntryId(link.Url, out var entryId)) continue;
            if (!IsPrecededByAt(link)) continue;
            mentions.Add(new Mention(entryId, PlainText(link)));
        }
        return mentions;
    }

    /// <summary>The distinct entry ids <paramref name="text"/> mentions, in order of first mention.</summary>
    public static Guid[] EntryIds(string text) => Parse(text).Select(m => m.EntryId).Distinct().ToArray();

    private static bool TryEntryId(string? url, out Guid entryId)
    {
        entryId = default;
        return url is not null
            && url.StartsWith(EntryScheme, StringComparison.Ordinal)
            && Guid.TryParseExact(url.AsSpan(EntryScheme.Length), "D", out entryId);
    }

    private static bool IsPrecededByAt(LinkInline link)
        => link.PreviousSibling is LiteralInline literal
            && !(literal.IsFirstCharacterEscaped && literal.Content.Length == 1)
            && literal.Content.Length > 0
            && literal.Content.Text[literal.Content.End] == '@';

    private static string PlainText(ContainerInline container)
    {
        var sb = new StringBuilder();
        Append(container, sb);
        return sb.ToString().Trim();
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
            case ContainerInline container:
                foreach (var child in container)
                {
                    Append(child, sb);
                }
                break;
        }
    }
}
