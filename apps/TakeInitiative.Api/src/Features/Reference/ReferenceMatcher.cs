using System.Globalization;
using System.Text;

namespace TakeInitiative.Api.Features.Reference;

/// <summary>
/// Matches a query against reference item names, in memory (20a.5). Reference items are not in
/// Postgres, so this ports the ladder of <c>SearchSql.MatchCategory</c> to keep the Reference
/// section ranked the way the Entries section is: 0 exact, 1 prefix, 2 word prefix, 3 substring,
/// 4 fuzzy (a query of three characters or more whose <see cref="WordSimilarity"/> reaches
/// <c>EntryMatchOptions.Default.MinSimilarity</c>). Within a rung: the more similar first, then the
/// shorter name, then the name. Pure.
/// </summary>
public static class ReferenceMatcher
{
    /// <summary>Below this many characters the fuzzy rung is skipped: trigrams match nearly everything.</summary>
    public const int FuzzyMinLength = 3;

    /// <summary>One candidate's name, folded once up front by the provider.</summary>
    public record Candidate<T>(T Item, string Name, string Folded);

    /// <summary>
    /// Lower case with accents stripped (<see cref="NormalizationForm.FormD"/>), the C# twin of the
    /// SQL's <c>lower(unaccent(…))</c>: "Gündren" folds to "gundren".
    /// </summary>
    public static string Fold(string text)
    {
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
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

    /// <summary>The best <paramref name="take"/> matches for <paramref name="text"/>, ranked.</summary>
    public static IReadOnlyList<(T Item, int Category, double Similarity)> Search<T>(
        string text, IEnumerable<Candidate<T>> candidates, int take, double? minSimilarity = null)
    {
        var query = Fold(text);
        if (query.Length == 0 || take <= 0)
        {
            return [];
        }
        var threshold = minSimilarity ?? EntryMatchOptions.Default.MinSimilarity;
        var queryTrigrams = Trigrams(query).ToHashSet();

        var matches = new List<(Candidate<T> Candidate, int Category, double Similarity)>();
        foreach (var candidate in candidates)
        {
            var category = Category(candidate.Folded, query);
            if (category is not null)
            {
                var similarity = category == 0 ? 1.0 : WordSimilarity(queryTrigrams, candidate.Folded);
                matches.Add((candidate, category.Value, similarity));
            }
            else if (query.Length >= FuzzyMinLength)
            {
                var similarity = WordSimilarity(queryTrigrams, candidate.Folded);
                if (similarity >= threshold)
                {
                    matches.Add((candidate, 4, similarity));
                }
            }
        }

        return matches
            .OrderBy(m => m.Category)
            .ThenByDescending(m => m.Similarity)
            .ThenBy(m => m.Candidate.Name.Length)
            .ThenBy(m => m.Candidate.Name, StringComparer.Ordinal)
            .Take(take)
            .Select(m => (m.Candidate.Item, m.Category, m.Similarity))
            .ToList();
    }

    /// <summary>
    /// The rungs 0–3 of the ladder, or null. Like SQL's <c>strpos</c>, only the first occurrence
    /// counts, and a word starts after whitespace, a hyphen, a quote or an opening parenthesis.
    /// Both sides are already folded.
    /// </summary>
    public static int? Category(string candidate, string query)
    {
        if (candidate == query)
        {
            return 0;
        }
        var index = candidate.IndexOf(query, StringComparison.Ordinal);
        return index switch
        {
            < 0 => null,
            0 => 1,
            _ when IsWordStart(candidate[index - 1]) => 2,
            _ => 3,
        };
    }

    private static bool IsWordStart(char previous)
        => char.IsWhiteSpace(previous) || previous is '-' or '\'' or '"' or '(';

    /// <summary>pg_trgm's <c>word_similarity(query, candidate)</c>, both already folded.</summary>
    public static double WordSimilarity(string query, string candidate)
        => WordSimilarity(Trigrams(query).ToHashSet(), candidate);

    /// <summary>
    /// pg_trgm's <c>word_similarity</c>, ported: the greatest similarity between the query's
    /// trigrams and any continuous run of the candidate's trigrams in order, where similarity is
    /// shared / (query's + run's − shared), over unique trigrams. Postgres narrows the runs
    /// greedily; this tries every run, so it can score a little higher on odd names but never
    /// lower. "word" in "two words" is 0.8, as in the pg_trgm docs.
    /// </summary>
    public static double WordSimilarity(IReadOnlySet<string> queryTrigrams, string candidate)
    {
        if (queryTrigrams.Count == 0)
        {
            return 0;
        }
        var sequence = Trigrams(candidate);
        var best = 0.0;
        var run = new HashSet<string>();
        for (var start = 0; start < sequence.Count; start++)
        {
            if (!queryTrigrams.Contains(sequence[start]))
            {
                continue; // a run that starts on a trigram the query lacks only scores lower
            }
            run.Clear();
            var shared = 0;
            for (var end = start; end < sequence.Count; end++)
            {
                if (run.Add(sequence[end]) && queryTrigrams.Contains(sequence[end]))
                {
                    shared++;
                }
                var similarity = (double)shared / (queryTrigrams.Count + run.Count - shared);
                if (similarity > best)
                {
                    best = similarity;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// pg_trgm's trigrams, in order: the text is split into words of letters and digits, and each
    /// word is padded with two spaces in front and one behind ("cat" → "  c", " ca", "cat", "at ").
    /// </summary>
    public static List<string> Trigrams(string text)
    {
        var trigrams = new List<string>();
        var word = new StringBuilder();
        void Flush()
        {
            if (word.Length == 0)
            {
                return;
            }
            var padded = "  " + word + " ";
            for (var i = 0; i + 3 <= padded.Length; i++)
            {
                trigrams.Add(padded.Substring(i, 3));
            }
            word.Clear();
        }
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(c);
            }
            else
            {
                Flush();
            }
        }
        Flush();
        return trigrams;
    }
}
