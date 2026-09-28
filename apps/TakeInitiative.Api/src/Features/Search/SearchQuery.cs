using System.Text;
using System.Text.RegularExpressions;

namespace TakeInitiative.Api.Features.Search;

/// <summary>What a query asks for. A leading <c>@</c> narrows it to entries; <c>&gt;</c> never reaches the server (17c).</summary>
public enum SearchScope
{
    All,
    Entries,
}

/// <summary>
/// One parsed ⌘K query. Pure and unit tested (<c>SearchQueryTests</c>), so the tsquery the
/// providers send to Postgres is decided in one place and never built from string pieces at
/// the call site.
/// </summary>
public sealed partial record SearchQuery
{
    /// <summary>The text after the prefix, trimmed. 1 to <see cref="MaxLength"/> characters.</summary>
    public required string Text { get; init; }
    public required SearchScope Scope { get; init; }
    /// <summary>The query's words, folded to lower case, at most <see cref="MaxTokens"/> of them.</summary>
    public required IReadOnlyList<string> Tokens { get; init; }
    /// <summary>
    /// The <c>to_tsquery('simple', …)</c> argument, <c>'tok1' &amp; 'tok2':*</c>, or null when the
    /// text holds no letters or digits at all. Null means the full-text sections are skipped:
    /// an empty tsquery matches nothing, so there is nothing to ask Postgres for.
    /// </summary>
    public required string? TsQuery { get; init; }
    /// <summary>The session number the text names, if it names one: <c>12</c>, <c>s12</c>, <c>S 12</c>, <c>session 12</c>.</summary>
    public required int? SessionNumber { get; init; }

    /// <summary>
    /// The length rule (17a.4): a single character searches entry names (by prefix) and session
    /// numbers only. Matching one character against every note and article of a campaign would
    /// return most of it, and typing the second character is one keystroke away.
    /// </summary>
    public bool SingleCharacter => Text.Length == 1;

    public const int MaxLength = 100;
    public const int MaxTokens = 8;

    /// <summary>The 400 for an empty or overlong query, under <c>errors.q</c>.</summary>
    public const string LengthError = "Search for 1 to 100 characters.";

    /// <summary>
    /// The query, or null when it is empty or longer than <see cref="MaxLength"/>. The prefix is
    /// taken off before the length is measured, so <c>@</c> alone is as invalid as an empty
    /// string.
    /// </summary>
    public static SearchQuery? Parse(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        var scope = SearchScope.All;
        if (trimmed.StartsWith('@'))
        {
            scope = SearchScope.Entries;
            trimmed = trimmed[1..].Trim();
        }

        if (trimmed.Length is 0 || trimmed.Length > MaxLength)
        {
            return null;
        }

        var tokens = Tokenize(trimmed);
        return new SearchQuery
        {
            Text = trimmed,
            Scope = scope,
            Tokens = tokens,
            TsQuery = BuildTsQuery(tokens),
            SessionNumber = SessionNumberOf(trimmed),
        };
    }

    /// <summary>
    /// The words: everything that is not a letter or a digit separates them, so punctuation,
    /// markdown and the tsquery operators (<c>&amp;</c>, <c>|</c>, <c>!</c>, <c>:</c>, <c>*</c>,
    /// quotes) cannot reach Postgres as syntax. There are no search operators (Notes).
    /// </summary>
    private static IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var word = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(char.ToLowerInvariant(c));
                continue;
            }
            if (word.Length > 0)
            {
                tokens.Add(word.ToString());
                word.Clear();
                if (tokens.Count == MaxTokens) return tokens;
            }
        }
        if (word.Length > 0 && tokens.Count < MaxTokens)
        {
            tokens.Add(word.ToString());
        }
        return tokens;
    }

    /// <summary>
    /// <c>'tok1' &amp; 'tok2':*</c>: every word must match, and the last one is a prefix so
    /// "gund" finds "Gundren" while it is still being typed. The whole string goes to
    /// <c>to_tsquery</c> as a parameter; a token holds only letters and digits, so nothing in it
    /// can end the quoting.
    /// </summary>
    private static string? BuildTsQuery(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0) return null;
        return string.Join(" & ", tokens.Select((t, i) => i == tokens.Count - 1 ? $"'{t}':*" : $"'{t}'"));
    }

    private static int? SessionNumberOf(string text)
    {
        var match = SessionNumberPattern().Match(text);
        return match.Success && int.TryParse(match.Groups[1].ValueSpan, out var number) ? number : null;
    }

    /// <summary>
    /// <c>12</c>, <c>s12</c>, <c>S 12</c> and <c>session 12</c>. Bounded digits, so the pattern
    /// cannot be made to backtrack and the number always fits an <c>int</c>.
    /// </summary>
    [GeneratedRegex(@"^(?:s|session)?\s*([0-9]{1,9})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SessionNumberPattern();
}
