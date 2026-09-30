using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// Row ids and deep links, ported from <c>build-5etools-index.mjs</c> lines 75-93.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MakeId" /> is the single most load-bearing function in the package. Its output is
/// the knowledge base table's primary key (26c) and what a user's entry link points at (step 27),
/// so a row's id has to survive a re-ingest and has to be character for character what the Node
/// script produced. Everything here is therefore written out rather than delegated to a .NET
/// convenience that is "close enough": <see cref="EncodeHash" /> reimplements
/// <c>encodeURIComponent</c> instead of using <see cref="Uri.EscapeDataString" />, which escapes a
/// different set, and <see cref="Slug" /> collapses runs by hand instead of leaning on a regex
/// whose character classes are Unicode-aware in .NET and ASCII-only in JavaScript.
/// </para>
/// </remarks>
public static partial class Ids
{
    /// <summary>
    /// <c>^[a-z]+_[a-z0-9-]+_[a-z0-9-]+$</c> from line 91. <c>\z</c> rather than <c>$</c>:
    /// .NET's <c>$</c> also matches before a trailing newline, JavaScript's does not.
    /// </summary>
    [GeneratedRegex(@"^[a-z]+_[a-z0-9-]+_[a-z0-9-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdShape();

    /// <summary>
    /// The characters <c>encodeURIComponent</c> leaves alone. Note the apostrophe and the
    /// parentheses: <c>Warden's Stone (Awakened)</c> keeps all three in its 5etools link.
    /// </summary>
    private const string Unreserved =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.!~*'()";

    /// <summary>
    /// A name reduced to a lower-case slug: NFKD, combining marks dropped, then every run of
    /// anything but <c>[a-z0-9]</c> to a single <c>-</c> with the ends trimmed.
    /// </summary>
    public static string Slug(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormKD);

        // /[\u0300-\u036f]/g: the combining diacritical marks NFKD has just split off.
        var stripped = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (c is < (char)0x0300 or > (char)0x036f) stripped.Append(c);
        }

        return NonAlphanumericToDash(stripped.ToString().ToLowerInvariant(), trimDashes: true);
    }

    /// <summary>
    /// 5etools' <c>UrlUtil.encodeForHash</c>: each part lower-cased and URI-encoded, joined
    /// with <c>_</c>.
    /// </summary>
    public static string EncodeHash(params string[] parts) =>
        string.Join("_", parts.Select(part => EncodeUriComponent(part.ToLowerInvariant())));

    /// <summary>
    /// <c>{category}_{slug(name)}_{source}</c>, lower-cased, non-alphanumerics to <c>-</c>.
    /// The source part is not decomposed and its dashes are not trimmed — that is what line 90
    /// does, and the ids already in the wild were formed this way.
    /// </summary>
    /// <exception cref="FiveEToolsBuildException">
    /// The id does not have the shape line 91 demands — an empty slug, or an underscore where
    /// there should not be one. A row with no usable id is a row no link could ever point at,
    /// so the whole build fails rather than the row being dropped quietly.
    /// </exception>
    public static string MakeId(string category, string name, string source)
    {
        var id = $"{category.ToLowerInvariant()}_{Slug(name)}_" +
                 NonAlphanumericToDash(source.ToLowerInvariant(), trimDashes: false);

        if (!IdShape().IsMatch(id))
        {
            throw new FiveEToolsBuildException($"cannot make an id for {category} \"{name}\" ({source})");
        }

        return id;
    }

    /// <summary>
    /// A media path (26g) encoded for a URL: each segment as <c>encodeURIComponent</c> would write
    /// it, joined back with <c>/</c>. 5eTools' file names hold spaces, commas and apostrophes —
    /// <c>items/MOT/Akmon, Hammer of Purphoros.webp</c> — and their site leaves the encoding to the
    /// browser, which a stored URL cannot.
    /// </summary>
    public static string EncodePath(string path) =>
        string.Join("/", path.Split('/').Select(EncodeUriComponent));

    /// <summary>
    /// <c>replace(/[^a-z0-9]+/g, "-")</c>, optionally followed by
    /// <c>replace(/^-+|-+$/g, "")</c>. Written as one pass because a dash is only ever emitted
    /// before the next kept character, which collapses runs and trims the ends at once.
    /// </summary>
    private static string NonAlphanumericToDash(string value, bool trimDashes)
    {
        var result = new StringBuilder(value.Length);
        var pending = false;

        foreach (var c in value)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pending && (trimDashes is false || result.Length > 0)) result.Append('-');
                pending = false;
                result.Append(c);
            }
            else
            {
                pending = true;
            }
        }

        // A trailing run is a trailing dash, which only the untrimmed form keeps.
        if (pending && trimDashes is false) result.Append('-');

        return result.ToString();
    }

    private static string EncodeUriComponent(string value)
    {
        var encoded = new StringBuilder(value.Length);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if (b < 0x80 && Unreserved.Contains((char)b, StringComparison.Ordinal)) encoded.Append((char)b);
            else encoded.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        return encoded.ToString();
    }
}
