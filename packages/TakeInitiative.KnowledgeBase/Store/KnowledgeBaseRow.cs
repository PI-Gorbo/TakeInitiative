using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Store;

/// <summary>
/// One <c>knowledge_base_item</c> row on its way into Postgres: a parsed
/// <see cref="KnowledgeBaseItem" />, the provider it came from, its book's title, and the content
/// hash the diff compares.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="KnowledgeBaseItem" /> is deliberately only the parser's output — the allowlist, and
/// nothing else. The ingest-time columns are here instead, so that adding one is not a change to
/// what the index is allowed to carry.
/// </para>
/// </remarks>
public sealed record KnowledgeBaseRow
{
    /// <summary>Which corpus this came from: <c>5etools</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>The parser's id, and half the primary key.</summary>
    public required string Id { get; init; }

    /// <summary>The name, exactly as the source spells it.</summary>
    public required string Name { get; init; }

    /// <summary><c>Monster</c>, <c>Spell</c> or <c>Item</c>.</summary>
    public required string Category { get; init; }

    /// <summary>The source book's abbreviation, such as <c>MM</c>.</summary>
    public required string SourceBook { get; init; }

    /// <summary>
    /// That book's full title, where the source's own catalogue names one. It comes from the index
    /// header rather than the row, so it is not part of <see cref="ContentHash" /> — see the note
    /// on <see cref="From" />.
    /// </summary>
    public string? SourceTitle { get; init; }

    /// <summary>The page in that book, where the source states one.</summary>
    public int? Page { get; init; }

    /// <summary>The muted line under the name: <c>CR 13 · Large Aberration</c>.</summary>
    public string? Label { get; init; }

    /// <summary>The deep link out.</summary>
    public required string Url { get; init; }

    /// <summary>
    /// The row's artwork, by URL. Always null until 26g, which is the step that adds it to the
    /// allowlist; the column exists now so 26g is a parser change and not a migration.
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// A monster's rollable numbers as JSON, or null. Written to a <c>jsonb</c> column, so Postgres
    /// normalises it; this string is never what a comparison reads.
    /// </summary>
    public string? Stats { get; init; }

    /// <summary>
    /// SHA-256, lower-case hex, of <see cref="FiveEToolsIndexSerializer.SerializeItem" />. See
    /// <see cref="From" /> for why that is the thing being hashed.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Builds the row for one parsed item.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The hash is over the serialiser's bytes, not over this record.</b>
    /// <see cref="FiveEToolsIndexSerializer.SerializeItem" /> is the one place in the codebase with
    /// a byte-stable spelling of a row: fixed key order, invariant number formatting, and
    /// <c>JSON.stringify</c>'s escaping rather than <c>System.Text.Json</c>'s. Hashing anything else
    /// — a record's <c>ToString</c>, a <c>JsonSerializer.Serialize</c> of it — would be a second
    /// spelling to keep stable, and the moment the two disagreed every row would report as updated
    /// and the diff, and the prune threshold that reads it, would be noise. 26b's golden-file and
    /// determinism tests already hold that serialiser still; this reuses that guarantee instead of
    /// making a new one.
    /// </para>
    /// <para>
    /// <b>What that leaves out</b> is <see cref="SourceTitle" />, which is not a field of a row: the
    /// parser reads it once from <c>books.json</c> into the index header and every row from that
    /// book shares it. So a run in which 5eTools renamed a book, and nothing else changed, reports
    /// every row unchanged and leaves the old title in place. That is the right trade — a book title
    /// is a caption, and making the hash cover it would mean either hashing a second string or
    /// rewriting the corpus whenever the header moved — and a re-ingest after any real change to
    /// those rows corrects it. If a title-only refresh is ever wanted, it is a one-line
    /// <c>update … set source_title = …</c>, not a reason to widen the hash.
    /// </para>
    /// </remarks>
    public static KnowledgeBaseRow From(string provider, KnowledgeBaseItem item, string? sourceTitle = null) =>
        new()
        {
            Provider = provider,
            Id = item.Id,
            Name = item.Name,
            Category = item.Category,
            SourceBook = item.Source,
            SourceTitle = sourceTitle,
            Page = item.Page,
            Label = item.Label,
            Url = item.Url,
            ImageUrl = null,
            Stats = StatsJson(item.Stats),
            ContentHash = HashOf(item),
        };

    /// <summary>Every row of a parsed index, with each row's book title filled in from the header.</summary>
    public static IReadOnlyList<KnowledgeBaseRow> From(string provider, FiveEToolsIndex index) =>
    [
        .. index.Items.Select(item => From(
            provider,
            item,
            index.Sources.TryGetValue(item.Source, out var title) ? title : null)),
    ];

    /// <summary>
    /// The content hash on its own, for the tests that prove two parses of the same folder produce
    /// the same hash.
    /// </summary>
    public static string HashOf(KnowledgeBaseItem item)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(FiveEToolsIndexSerializer.SerializeItem(item)));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// A monster's numbers as the <c>stats</c> column holds them, written by hand for the same
    /// reason the index serialiser is: fixed key order and invariant formatting.
    /// </summary>
    private static string? StatsJson(KnowledgeBaseItemStats? stats)
    {
        if (stats is null) return null;

        var hp = new StringBuilder();
        foreach (var character in stats.Hp)
        {
            // The dice string is [0-9dD+- ] by the time the parser has checked it, so there is
            // nothing here that JSON escapes; the switch is a belt on top of that brace.
            if (character is '"' or '\\') hp.Append('\\');
            hp.Append(character);
        }

        return $$"""
                 {"ac":{{stats.Ac.ToString(CultureInfo.InvariantCulture)}},"hp":"{{hp}}","initiativeBonus":{{stats.InitiativeBonus.ToString(CultureInfo.InvariantCulture)}}}
                 """;
    }
}
