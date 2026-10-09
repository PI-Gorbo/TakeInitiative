namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// One entry that a span matched, and how well.
/// </summary>
/// <param name="EntryId">The entry. Never a merged one: those resolve to their target.</param>
/// <param name="MatchedName">The name or alias that matched, as it is stored (not folded).</param>
/// <param name="IsAlias">Whether <paramref name="MatchedName"/> is an alias rather than the name.</param>
/// <param name="Category">0 exact, 1 prefix, 2 word prefix, 3 substring, 4 fuzzy (17a.8).</param>
/// <param name="Similarity">
/// <c>word_similarity(span, name)</c>, for ordering inside a category. It scores one row alone,
/// so a name the viewer cannot see cannot move one they can.
/// </param>
public record EntryMatch(Guid EntryId, string MatchedName, bool IsAlias, int Category, double Similarity);

/// <summary>
/// How to match. <paramref name="MinSimilarity"/> is the trigram threshold for the fuzzy step,
/// which only applies to spans of three characters or more. <paramref name="FuzzyOnly"/> is for
/// callers that already looked for exact matches themselves.
/// </summary>
public record EntryMatchOptions(int Take = 5, double MinSimilarity = 0.5, bool FuzzyOnly = false)
{
    /// <summary>The defaults, for the callers that only need the thresholds (session titles use the same one).</summary>
    public static readonly EntryMatchOptions Default = new();
}
