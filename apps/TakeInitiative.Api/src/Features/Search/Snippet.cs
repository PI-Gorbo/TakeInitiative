namespace TakeInitiative.Api.Features.Search;

/// <summary>One highlighted run inside a <see cref="Snippet"/>, as UTF-16 offsets into its text.</summary>
public sealed record SnippetHighlight
{
    public required int Start { get; init; }
    public required int Length { get; init; }
}

/// <summary>
/// The words of one matching session note or article block, with the match highlighted
/// (glossary: Snippet). It is cut from exactly one unit that the viewer can see, so it cannot
/// carry text from another audience.
/// <para>
/// The API never sends HTML. <c>ts_headline</c> is asked for two private-use characters as its
/// markers, <see cref="From"/> turns them into <see cref="Highlights"/> over plain
/// <see cref="Text"/>, and the web draws them as text nodes inside <c>&lt;mark&gt;</c>. The
/// markers are also deleted from the source text (<c>SearchSql.PlainText</c>), so a member who
/// types one cannot forge a highlight.
/// </para>
/// </summary>
public sealed record Snippet
{
    public required string Text { get; init; }
    public required SnippetHighlight[] Highlights { get; init; }

    /// <summary>U+E000, a private-use character: <c>ts_headline</c>'s <c>StartSel</c>.</summary>
    public const char StartMarker = '';
    /// <summary>U+E001, a private-use character: <c>ts_headline</c>'s <c>StopSel</c>.</summary>
    public const char StopMarker = '';

    /// <summary>
    /// The <c>ts_headline</c> options, as one SQL literal. <c>MaxFragments=1</c> keeps a snippet
    /// to a single run of words, so no delimiter is needed and nothing is stitched together
    /// across a gap.
    /// </summary>
    public const string HeadlineOptions =
        "StartSel=, StopSel=, MaxWords=24, MinWords=10, ShortWord=2, MaxFragments=1";

    /// <summary>
    /// The snippet for one headline, or null when there is nothing to show. Markdown markers are
    /// dropped as the markers are read, so the offsets are right for the text the reader sees:
    /// emphasis and code markers anywhere, and a heading's <c>#</c> or a quote's <c>&gt;</c> at
    /// the start of a line.
    /// </summary>
    public static Snippet? From(string? headline)
    {
        if (string.IsNullOrEmpty(headline))
        {
            return null;
        }

        var text = new System.Text.StringBuilder(headline.Length);
        var highlights = new List<SnippetHighlight>();
        int? openedAt = null;
        // Markdown's block markers only count at the start of a line, and only the spaces that
        // follow them belong to the marker: "a > b" keeps its ">".
        var atLineStart = true;
        var sawBlockMarker = false;

        foreach (var c in headline)
        {
            if (c == StartMarker)
            {
                openedAt = text.Length;
                continue;
            }
            if (c == StopMarker)
            {
                if (openedAt is { } start && text.Length > start)
                {
                    highlights.Add(new SnippetHighlight { Start = start, Length = text.Length - start });
                }
                openedAt = null;
                continue;
            }
            if (c is '*' or '_' or '`')
            {
                continue;
            }
            if (atLineStart)
            {
                if (c is '#' or '>')
                {
                    sawBlockMarker = true;
                    continue;
                }
                if (sawBlockMarker && c is ' ' or '\t')
                {
                    continue;
                }
                atLineStart = false;
                sawBlockMarker = false;
            }

            text.Append(c);
            if (c is '\n')
            {
                atLineStart = true;
                sawBlockMarker = false;
            }
        }

        return text.Length == 0
            ? null
            : new Snippet { Text = text.ToString(), Highlights = [.. highlights] };
    }

    /// <summary>
    /// <see cref="From"/>, but always a snippet: a headline with nothing to show becomes an empty
    /// one rather than nothing at all.
    /// <para>
    /// A headline with no highlight in it is <b>not</b> drift and must not cost the viewer the hit.
    /// A row is matched on an indexed expression and its headline is cut from what a reader sees, and
    /// the two can differ (17a.5, and <c>SearchSql.EntryArticleText</c>): a session title matched
    /// down the trigram ladder has no <c>tsquery</c> to mark at all. The hit is real either way, so
    /// it is shown with the words and no highlights.
    /// </para>
    /// </summary>
    public static Snippet OrEmpty(string? headline)
        => From(headline) ?? new Snippet { Text = string.Empty, Highlights = [] };
}
