using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TakeInitiative.Api.Features.LooseEnds;

/// <summary>A stretch of a note's text that might name an entry, at its character offset (UTF-16, as the web indexes strings).</summary>
public record LinkSpan(int Start, int Length, string Text);

/// <summary>A span the entry matcher matched: the entry and how well.</summary>
public record LinkMatch(LinkSpan Span, Guid EntryId, double Similarity);

/// <summary>
/// Link suggestions' pure half (19b.2, glossary: Link suggestion). <see cref="From"/> proposes the
/// spans of an unlinked note that could name an entry, <see cref="Accepts"/> decides whether the
/// entry matcher's answer for a span is good enough to offer, and <see cref="Pick"/> chooses a
/// note's suggestions from the accepted matches. The matcher call between them is
/// <see cref="LooseEnds.SuggestLinks"/>.
/// </summary>
public static partial class LinkSpans
{
    /// <summary>At most this many spans from one note.</summary>
    public const int MaxPerNote = 40;
    /// <summary>A span is at most this many words.</summary>
    public const int MaxWords = 3;
    /// <summary>At most this many suggestions on one note.</summary>
    public const int MaxSuggestions = 3;

    /// <summary>
    /// Common words that are never a name, even capitalised at the start of a sentence ("The",
    /// "We", "Then"). Compared folded, and applied to every word, because a lower-case word of four
    /// letters or more is a span too and "then" or "with" never names an entry.
    /// </summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "and", "or", "but", "so", "if", "of", "to", "in", "on", "at", "by", "for", "as",
        "i", "we", "he", "she", "it", "they", "you", "me", "us", "him", "her", "them", "my", "our", "his",
        "its", "their", "your", "this", "that", "these", "those", "there", "here", "then", "than", "when",
        "where", "what", "who", "whom", "which", "while", "after", "before", "later", "next", "now", "also",
        "with", "from", "into", "onto", "over", "under", "about", "again", "back", "just", "only", "very",
        "much", "more", "most", "some", "each", "every", "been", "being", "have", "were", "was", "will",
        "would", "could", "should", "might", "must", "shall", "does", "did", "done", "still", "even",
        "yes", "no", "not", "all", "any", "both", "other", "such", "too", "well", "meanwhile", "finally",
        "suddenly", "today", "tonight", "yesterday", "tomorrow", "session", "note", "notes",
    };

    // A mention, @[text](entry:id), or any other markdown link target, inline code, or a bare URL:
    // none of it is prose, so none of it is a span, and each one breaks a run of words.
    [GeneratedRegex(@"@\[(?:[^\[\]]|\[[^\[\]]*\])*\]\([^)\s]*\)|\]\([^)\s]*\)|`[^`\n]*`|https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex NotProse();

    // A word: a letter, then letters, marks, digits, apostrophes or hyphens.
    [GeneratedRegex(@"\p{L}[\p{L}\p{M}\p{Nd}'’\-]*")]
    private static partial Regex Word();

    /// <summary>
    /// The spans of <paramref name="text"/> worth asking the entry matcher about: runs of one to
    /// <see cref="MaxWords"/> neighbouring words that each start with a capital letter or have at
    /// least four letters, skipping mentions, link targets, code, URLs and the stop list. Two words
    /// are neighbours when only spaces separate them, so punctuation and markdown break a run. In
    /// text order, at most <see cref="MaxPerNote"/>.
    /// </summary>
    public static IReadOnlyList<LinkSpan> From(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var masked = new bool[text.Length];
        foreach (Match m in NotProse().Matches(text))
        {
            Array.Fill(masked, true, m.Index, m.Length);
        }

        var words = new List<(int Start, int End)>();
        foreach (Match m in Word().Matches(text))
        {
            var end = WordEnd(text, m.Index, m.Index + m.Length);
            if (Enumerable.Range(m.Index, end - m.Index).Any(i => masked[i]))
            {
                continue;
            }
            var word = text[m.Index..end];
            if (IsCandidate(word))
            {
                words.Add((m.Index, end));
            }
        }

        var spans = new List<LinkSpan>();
        for (var i = 0; i < words.Count && spans.Count < MaxPerNote; i++)
        {
            for (var j = i; j < words.Count && j < i + MaxWords && spans.Count < MaxPerNote; j++)
            {
                if (j > i && !OnlySpaces(text, words[j - 1].End, words[j].Start))
                {
                    break;
                }
                var start = words[i].Start;
                spans.Add(new LinkSpan(start, words[j].End - start, text[start..words[j].End]));
            }
        }
        return spans;
    }

    /// <summary>Where a word really ends: without a trailing apostrophe or hyphen, or a possessive <c>'s</c> ("Halia's map" asks about "Halia").</summary>
    private static int WordEnd(string text, int start, int end)
    {
        if (end - start > 2 && text[end - 1] is 's' or 'S' && text[end - 2] is '\'' or '’')
        {
            end -= 2;
        }
        while (end > start && text[end - 1] is '\'' or '’' or '-')
        {
            end--;
        }
        return end;
    }

    private static bool IsCandidate(string word)
    {
        if (StopWords.Contains(Fold(word)))
        {
            return false;
        }
        var letters = word.Count(char.IsLetter);
        return (char.IsUpper(word[0]) && letters >= 2) || letters >= 4;
    }

    private static bool OnlySpaces(string text, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            if (text[i] is not (' ' or '\t'))
            {
                return false;
            }
        }
        return to > from;
    }

    /// <summary>
    /// Whether the matcher's answer for a span is worth offering. An exact match (category 0) and a
    /// fuzzy one (4, already over the matcher's threshold) are; a prefix or word-prefix match (1, 2)
    /// is only when the span is whole words of the name ("gundren" in "Gundren Rockseeker", not
    /// "gund"); a substring (3) never is ("rock" inside "Brockton").
    /// </summary>
    public static bool Accepts(string span, EntryMatch match) => match.Category switch
    {
        0 or 4 => true,
        1 or 2 => IsWholeWordsOf(span, match.MatchedName),
        _ => false,
    };

    private static bool IsWholeWordsOf(string span, string name)
    {
        var want = Words(span);
        var have = Words(name);
        if (want.Length == 0)
        {
            return false;
        }
        for (var i = 0; i + want.Length <= have.Length; i++)
        {
            if (want.AsSpan().SequenceEqual(have.AsSpan(i, want.Length)))
            {
                return true;
            }
        }
        return false;
    }

    private static string[] Words(string text)
        => Word().Matches(text).Select(m => Fold(text[m.Index..WordEnd(text, m.Index, m.Index + m.Length)])).ToArray();

    /// <summary>Lower case with accents removed, the C# twin of the matcher's <c>lower(unaccent(…))</c>, near enough for comparing words.</summary>
    private static string Fold(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// A note's suggestions from its accepted matches: overlapping spans keep the longest, then the
    /// more similar; an entry is suggested once; at most <see cref="MaxSuggestions"/>. In text order.
    /// </summary>
    public static IReadOnlyList<LinkMatch> Pick(IEnumerable<LinkMatch> matches)
    {
        var chosen = new List<LinkMatch>();
        foreach (var match in matches
            .OrderByDescending(m => m.Span.Length)
            .ThenByDescending(m => m.Similarity)
            .ThenBy(m => m.Span.Start))
        {
            if (chosen.Count == MaxSuggestions)
            {
                break;
            }
            var overlaps = chosen.Any(c =>
                match.Span.Start < c.Span.Start + c.Span.Length && c.Span.Start < match.Span.Start + match.Span.Length);
            if (!overlaps && chosen.All(c => c.EntryId != match.EntryId))
            {
                chosen.Add(match);
            }
        }
        return chosen.OrderBy(m => m.Span.Start).ToList();
    }
}
