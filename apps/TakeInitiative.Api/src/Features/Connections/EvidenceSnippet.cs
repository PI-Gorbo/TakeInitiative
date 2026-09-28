using System.Text.RegularExpressions;

namespace TakeInitiative.Api.Features.Connections;

/// <summary>
/// The words of one piece of evidence around where two entries meet (19a.4), pure. The snippet
/// is the source's own markdown, with its mentions left as <c>@[text](entry:id)</c> so the web
/// draws them as chips (and as plain text for an entry the viewer cannot see, 15c's rule). It
/// is cut from exactly one note or block the viewer can see, so it carries nothing else.
/// <list type="bullet">
/// <item>A text of at most <see cref="Window"/> characters is returned whole.</item>
/// <item>Otherwise the window is about <see cref="Window"/> characters around the first mention
/// of each entry, cut on word boundaries and never inside a mention, with <c>…</c> at a cut.</item>
/// <item>When the two first mentions are too far apart for one window, each gets half a
/// window, joined by <c> … </c>.</item>
/// <item>An entry with no mention in the text (an article's own entry) adds no anchor; with no
/// anchor at all, the window is the start of the text.</item>
/// </list>
/// </summary>
public static partial class EvidenceSnippet
{
    public const int Window = 280;
    public const string Ellipsis = "…";

    [GeneratedRegex(@"@\[(?:[^\[\]]|\[[^\[\]]*\])*\]\(entry:(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)")]
    private static partial Regex MentionPattern();

    private readonly record struct Range(int Start, int End);

    /// <param name="text">The note's or block's markdown.</param>
    /// <param name="a">The ids that mean one entry (<see cref="Entry.MentionIds"/>).</param>
    /// <param name="b">The ids that mean the other.</param>
    public static string Cut(string text, IReadOnlyCollection<Guid> a, IReadOnlyCollection<Guid> b)
    {
        text = text.Trim();
        if (text.Length <= Window)
        {
            return text;
        }

        var mentions = MentionPattern().Matches(text)
            .Select(m => (Range: new Range(m.Index, m.Index + m.Length), Id: Guid.TryParse(m.Groups["id"].Value, out var id) ? id : Guid.Empty))
            .ToList();
        var ranges = mentions.Select(m => m.Range).ToList();
        Range? First(IReadOnlyCollection<Guid> ids) => mentions.Where(m => ids.Contains(m.Id)).Select(m => (Range?)m.Range).FirstOrDefault();

        var anchors = new[] { First(a), First(b) }.OfType<Range>().OrderBy(r => r.Start).ToList();
        if (anchors.Count == 0)
        {
            return Around(text, new Range(0, 0), Window, ranges);
        }

        var span = new Range(anchors[0].Start, anchors[^1].End);
        if (span.End - span.Start <= Window)
        {
            return Around(text, span, Window, ranges);
        }

        var left = Around(text, anchors[0], Window / 2, ranges);
        var right = Around(text, anchors[^1], Window / 2, ranges);
        return left.TrimEnd('…').TrimEnd() + " " + Ellipsis + " " + right.TrimStart('…').TrimStart();
    }

    /// <summary>About <paramref name="size"/> characters centred on <paramref name="anchor"/>, which is always kept whole.</summary>
    private static string Around(string text, Range anchor, int size, IReadOnlyList<Range> mentions)
    {
        var spare = Math.Max(0, size - (anchor.End - anchor.Start));
        var start = Math.Max(0, anchor.Start - spare / 2);
        var end = Math.Min(text.Length, start + Math.Max(size, anchor.End - anchor.Start));
        // Near the end, the room left over goes before the anchor.
        start = Math.Max(0, Math.Min(start, end - size));

        start = WordStart(text, start, anchor.Start);
        end = WordEnd(text, end, anchor.End);
        start = OutsideMentions(start, mentions, towardsStart: true);
        end = OutsideMentions(end, mentions, towardsStart: false);

        var cut = text[start..end].Trim();
        return (start > 0 ? Ellipsis : "") + cut + (end < text.Length ? Ellipsis : "");
    }

    /// <summary>Moves a left cut forward to the start of a word, but never past <paramref name="limit"/>.</summary>
    private static int WordStart(string text, int start, int limit)
    {
        if (start == 0 || char.IsWhiteSpace(text[start - 1]))
        {
            return start;
        }
        for (var i = start; i < limit; i++)
        {
            if (char.IsWhiteSpace(text[i - 1]))
            {
                return i;
            }
        }
        return start;
    }

    /// <summary>Moves a right cut back to the end of a word, but never before <paramref name="limit"/>.</summary>
    private static int WordEnd(string text, int end, int limit)
    {
        if (end == text.Length || char.IsWhiteSpace(text[end]))
        {
            return end;
        }
        for (var i = end; i > limit; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }
        return end;
    }

    /// <summary>A cut inside a mention moves out of it, so a chip is never half drawn.</summary>
    private static int OutsideMentions(int at, IReadOnlyList<Range> mentions, bool towardsStart)
    {
        foreach (var m in mentions)
        {
            if (at > m.Start && at < m.End)
            {
                return towardsStart ? m.Start : m.End;
            }
        }
        return at;
    }
}
