using System.Globalization;
using System.Text;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// Writes an index to the exact bytes the Node script wrote, ported from
/// <c>build-5etools-index.mjs</c> line 376.
/// </summary>
/// <remarks>
/// <para>
/// The layout is the header on one line and then one row per line, which makes a diff between two
/// builds readable and lets a row be hashed on its own. Byte stability is the point: 26c decides
/// what an ingest changed by comparing content hashes, so a key in a different order, a number
/// formatted under a different culture or a <c>\r\n</c> would report every row as updated and make
/// the diff — and the prune threshold that reads it — meaningless.
/// </para>
/// <para>
/// The string escaping is <c>JSON.stringify</c>'s, spelled out here rather than delegated to
/// <c>System.Text.Json</c>. Its default encoder escapes far more than JavaScript does (<c>+</c>,
/// <c>&amp;</c>, every non-ASCII character), which would turn <c>CR 13 · Large Aberration</c> into
/// <c>CR 13 · Large Aberration</c> and a comparison against the committed golden file into a
/// comparison of two spellings of the same thing.
/// </para>
/// <para>
/// The writers are tables rather than straight-line code so that the key list a build actually
/// writes can be checked against <see cref="FiveEToolsAllowlist" />. A field added to a row's record
/// and to this table, but not to the allowlist, fails the build.
/// </para>
/// </remarks>
public static class FiveEToolsIndexSerializer
{
    private static readonly (string Key, Action<StringBuilder, FiveEToolsIndex> Write)[] HeadFields =
    [
        ("format", (json, index) => WriteNumber(json, index.Format)),
        ("fiveEToolsVersion", (json, index) => WriteStringOrNull(json, index.FiveEToolsVersion)),
        ("counts", (json, index) => WriteCounts(json, index.Counts)),
        ("sources", (json, index) => WriteSources(json, index.Sources)),
    ];

    private static readonly (string Key, Action<StringBuilder, KnowledgeBaseItem> Write)[] ItemFields =
    [
        ("id", (json, item) => WriteString(json, item.Id)),
        ("name", (json, item) => WriteString(json, item.Name)),
        ("category", (json, item) => WriteString(json, item.Category)),
        ("source", (json, item) => WriteString(json, item.Source)),
        ("page", (json, item) => WriteNumberOrNull(json, item.Page)),
        ("url", (json, item) => WriteString(json, item.Url)),
        ("label", (json, item) => WriteStringOrNull(json, item.Label)),
        ("stats", (json, item) => WriteStats(json, item.Stats)),
    ];

    private static readonly (string Key, Action<StringBuilder, KnowledgeBaseItemStats> Write)[] StatsFields =
    [
        ("ac", (json, stats) => WriteNumber(json, stats.Ac)),
        ("hp", (json, stats) => WriteString(json, stats.Hp)),
        ("initiativeBonus", (json, stats) => WriteNumber(json, stats.InitiativeBonus)),
    ];

    /// <summary>The index keys <see cref="Serialize" /> writes, in the order it writes them.</summary>
    public static IReadOnlyList<string> TopKeysWritten { get; } = [.. HeadFields.Select(field => field.Key), "items"];

    /// <summary>The row keys <see cref="Serialize" /> writes, in the order it writes them.</summary>
    public static IReadOnlyList<string> ItemKeysWritten { get; } = [.. ItemFields.Select(field => field.Key)];

    /// <summary>The stats keys <see cref="Serialize" /> writes, in the order it writes them.</summary>
    public static IReadOnlyList<string> StatsKeysWritten { get; } = [.. StatsFields.Select(field => field.Key)];

    /// <summary>
    /// The index as a file: the header, then one row per line, then a trailing newline. Always
    /// <c>\n</c>, never the platform's.
    /// </summary>
    public static string Serialize(FiveEToolsIndex index)
    {
        var json = new StringBuilder();

        json.Append('{');
        for (var i = 0; i < HeadFields.Length; i++)
        {
            if (i > 0) json.Append(',');
            WriteKey(json, HeadFields[i].Key);
            HeadFields[i].Write(json, index);
        }

        json.Append(",\"items\":[\n");
        for (var i = 0; i < index.Items.Count; i++)
        {
            if (i > 0) json.Append(",\n");
            WriteItem(json, index.Items[i]);
        }

        json.Append("\n]}\n");
        return json.ToString();
    }

    /// <summary>One row, on one line. 26c hashes exactly this to decide whether a row changed.</summary>
    public static string SerializeItem(KnowledgeBaseItem item)
    {
        var json = new StringBuilder();
        WriteItem(json, item);
        return json.ToString();
    }

    private static void WriteItem(StringBuilder json, KnowledgeBaseItem item)
    {
        json.Append('{');
        for (var i = 0; i < ItemFields.Length; i++)
        {
            if (i > 0) json.Append(',');
            WriteKey(json, ItemFields[i].Key);
            ItemFields[i].Write(json, item);
        }

        json.Append('}');
    }

    private static void WriteStats(StringBuilder json, KnowledgeBaseItemStats? stats)
    {
        if (stats is null)
        {
            json.Append("null");
            return;
        }

        json.Append('{');
        for (var i = 0; i < StatsFields.Length; i++)
        {
            if (i > 0) json.Append(',');
            WriteKey(json, StatsFields[i].Key);
            StatsFields[i].Write(json, stats);
        }

        json.Append('}');
    }

    private static void WriteCounts(StringBuilder json, FiveEToolsCounts counts)
    {
        json.Append("{\"monster\":").Append(counts.Monster.ToString(CultureInfo.InvariantCulture))
            .Append(",\"spell\":").Append(counts.Spell.ToString(CultureInfo.InvariantCulture))
            .Append(",\"item\":").Append(counts.Item.ToString(CultureInfo.InvariantCulture))
            .Append('}');
    }

    /// <summary>
    /// The sources object, keys in ordinal order. The script builds it from a sorted array, and
    /// sorting here rather than trusting the caller's dictionary is what keeps two builds identical.
    /// </summary>
    private static void WriteSources(StringBuilder json, IReadOnlyDictionary<string, string> sources)
    {
        json.Append('{');
        var first = true;
        foreach (var abbreviation in sources.Keys.Order(StringComparer.Ordinal))
        {
            if (!first) json.Append(',');
            first = false;
            WriteKey(json, abbreviation);
            WriteString(json, sources[abbreviation]);
        }

        json.Append('}');
    }

    private static void WriteKey(StringBuilder json, string key)
    {
        WriteString(json, key);
        json.Append(':');
    }

    private static void WriteNumber(StringBuilder json, int value) =>
        json.Append(value.ToString(CultureInfo.InvariantCulture));

    private static void WriteNumberOrNull(StringBuilder json, int? value)
    {
        if (value is { } number) WriteNumber(json, number);
        else json.Append("null");
    }

    private static void WriteStringOrNull(StringBuilder json, string? value)
    {
        if (value is null) json.Append("null");
        else WriteString(json, value);
    }

    /// <summary>
    /// A JSON string escaped the way <c>JSON.stringify</c> escapes one: the quote, the backslash,
    /// the five short forms, the rest of C0 as <c>\u00xx</c> in lower-case hex, and an unpaired
    /// surrogate as <c>\udxxx</c>. Everything else, non-ASCII included, is written as itself.
    /// </summary>
    private static void WriteString(StringBuilder json, string value)
    {
        json.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"': json.Append("\\\""); break;
                case '\\': json.Append("\\\\"); break;
                case '\b': json.Append("\\b"); break;
                case '\f': json.Append("\\f"); break;
                case '\n': json.Append("\\n"); break;
                case '\r': json.Append("\\r"); break;
                case '\t': json.Append("\\t"); break;
                default:
                    if (c < ' ' || (char.IsSurrogate(c) && !IsPaired(value, i))) WriteUnicodeEscape(json, c);
                    else json.Append(c);
                    break;
            }
        }

        json.Append('"');
    }

    private static bool IsPaired(string value, int index) =>
        char.IsHighSurrogate(value[index])
            ? index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])
            : index > 0 && char.IsHighSurrogate(value[index - 1]);

    private static void WriteUnicodeEscape(StringBuilder json, char c) =>
        json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
}
