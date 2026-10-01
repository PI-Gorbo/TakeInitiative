using System.Text.Json;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// The artwork slot (26g): a row's picture, as a URL on 5eTools' own media host.
/// </summary>
/// <remarks>
/// <para>
/// <b>This widens the allowlist, and that was a decision rather than a detail.</b> Step 21's script
/// header stated that the index holds "no rules text, descriptions, stat blocks, images or any
/// other 5eTools field", and that any change to the allowlist is the user's call. 26g is that call:
/// <c>imageUrl</c> joins <see cref="FiveEToolsAllowlist.ItemKeys" />, and what it holds is still
/// only an identifier — a path this parser turns into a URL. <b>Nothing is fetched and nothing is
/// stored.</b> The bytes are served by them, to the reader's browser, and never touch our server or
/// our blob store; a reader with images blocked, or a path that has moved, simply gets the row's
/// category glyph.
/// </para>
/// <para>
/// <b>Where the path comes from.</b> 5eTools keeps artwork out of the stat-block files and in
/// "fluff" files beside them, which the parse otherwise skips by name
/// (<c>FiveEToolsParser.ReadIndexed</c>). A fluff entry is <c>{ name, source, images: [ { type:
/// "image", href: { type: "internal", path: "bestiary/MM/Aarakocra.webp" } } ] }</c>, and the three
/// categories are indexed three different ways:
/// </para>
/// <list type="bullet">
///   <item><description><c>bestiary/fluff-index.json</c> → per-source files holding <c>monsterFluff</c>;</description></item>
///   <item><description><c>spells/fluff-index.json</c> → per-source files holding <c>spellFluff</c>;</description></item>
///   <item><description><c>fluff-items.json</c>, one file, holding <c>itemFluff</c>.</description></item>
/// </list>
/// <para>
/// <b>Fluff has its own <c>_copy</c>.</b> Half of a bestiary's fluff entries are a bare
/// <c>{ name, source, _copy: { name, source } }</c> — the Abominable Yeti's picture is the Yeti's.
/// Following it is not an optimisation: in 5eTools' own Monster Manual file 369 monsters say they
/// have artwork while only 244 fluff entries carry an <c>images</c> array of their own. An entry
/// that has both keeps its own.
/// </para>
/// <para>
/// <b>Only <c>internal</c> hrefs.</b> A fluff image may instead carry
/// <c>href: { type: "external", url: … }</c>, which is an arbitrary third-party URL. Those are
/// dropped: what this class writes is always <c>{baseUrl}/img/{path}</c> under the host the
/// operator already chose for the row's link, so the corpus cannot come to hold a URL pointing
/// anywhere else.
/// </para>
/// <para>
/// <b>The base URL was verified against real data</b> before it was written down, because it is the
/// one part of this parse a golden file cannot check — the committed fixtures carry no
/// <c>href</c> at all. Ten paths taken from 5eTools' own <c>fluff-bestiary-mm.json</c>,
/// <c>fluff-items.json</c> and <c>fluff-spells-phb.json</c> were requested under
/// <c>https://5e.tools/img/</c> and every one answered <c>200 image/webp</c>, including the ones
/// whose file names hold spaces, commas and apostrophes. That is why the path is encoded segment by
/// segment rather than pasted in raw.
/// </para>
/// </remarks>
internal sealed class FiveEToolsImages
{
    /// <summary>
    /// The path under the host that serves the artwork. 5eTools' own renderer resolves an internal
    /// media href as <c>{site}/img/{path}</c>, and <see cref="FiveEToolsParserOptions.BaseUrl" /> is
    /// already the site a row links to, so a mirror configured there serves its own images too.
    /// </summary>
    public const string MediaPath = "img";

    /// <summary>A fluff <c>_copy</c> chain longer than this is data this parser will not follow.</summary>
    private const int MaxCopyDepth = 16;

    private readonly Dictionary<string, Dictionary<string, JsonElement>> byCategory;

    private FiveEToolsImages(Dictionary<string, Dictionary<string, JsonElement>> byCategory) =>
        this.byCategory = byCategory;

    /// <summary>
    /// Reads every fluff file the data folder indexes. A category with no fluff index is a category
    /// with no artwork — not a failure: <c>fluff-index.json</c> and <c>fluff-items.json</c> are as
    /// optional as <c>books.json</c> is, and a partial checkout should still produce rows.
    /// </summary>
    public static FiveEToolsImages Read(string data, JsonReader reader)
    {
        return new FiveEToolsImages(new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.Ordinal)
        {
            ["Monster"] = Indexed(reader, Path.Combine(data, "bestiary"), "fluff-bestiary-", "monsterFluff"),
            ["Spell"] = Indexed(reader, Path.Combine(data, "spells"), "fluff-spells-", "spellFluff"),
            ["Item"] = OneFile(reader, Path.Combine(data, "fluff-items.json"), "itemFluff"),
        });
    }

    /// <summary>
    /// The row's artwork URL, or null where the source names none — which is most rows of most
    /// corpora, and always the case for a checkout with no fluff files in it.
    /// </summary>
    public string? UrlFor(string category, string name, string source, string baseUrl)
    {
        if (!byCategory.TryGetValue(category, out var index)) return null;
        if (!index.TryGetValue(Copy.KeyOf(name, source), out var fluff)) return null;

        var path = PathOf(fluff, index, 0);
        return path is null ? null : $"{baseUrl}/{MediaPath}/{Ids.EncodePath(path)}";
    }

    /// <summary>
    /// A fluff entry's own first usable image, else its <c>_copy</c> base's, following the chain.
    /// A base that is not in the folder, or a chain that loops, leaves the row without artwork
    /// rather than failing the build: a missing picture is not a missing row.
    /// </summary>
    private static string? PathOf(JsonElement fluff, Dictionary<string, JsonElement> index, int depth)
    {
        if (FirstImagePath(fluff) is { } own) return own;
        if (depth >= MaxCopyDepth) return null;

        var copy = Js.Get(fluff, "_copy");
        if (!Js.IsTruthy(copy)) return null;

        var key = Copy.KeyOf(Js.Get(copy, "name"), Js.Get(copy, "source"));
        return index.TryGetValue(key, out var basis) ? PathOf(basis, index, depth + 1) : null;
    }

    /// <summary>
    /// The first <c>{ type: "image", href: { type: "internal", path } }</c> of an entry's
    /// <c>images</c>. The first rather than all of them: a row has one slot, and 5eTools puts the
    /// portrait first.
    /// </summary>
    private static string? FirstImagePath(JsonElement fluff)
    {
        if (Js.Get(fluff, "images") is not { } images || !Js.IsArray(images)) return null;

        foreach (var image in images.EnumerateArray())
        {
            if (Js.AsString(Js.Get(image, "type")) != "image") continue;
            if (Js.Get(image, "href") is not { } href) continue;
            if (Js.AsString(Js.Get(href, "type")) != "internal") continue;
            if (Js.AsString(Js.Get(href, "path")) is { Length: > 0 } path && IsSafePath(path)) return path;
        }

        return null;
    }

    /// <summary>
    /// A relative path under the media folder and nothing else. This string is pasted into a URL,
    /// so a leading slash, a scheme, a backslash or a <c>..</c> segment — none of which 5eTools'
    /// data holds — would be a way for the source to point the row somewhere else entirely.
    /// </summary>
    private static bool IsSafePath(string path) =>
        !path.StartsWith('/')
        && !path.Contains('\\', StringComparison.Ordinal)
        && !path.Contains("..", StringComparison.Ordinal)
        && !path.Contains("//", StringComparison.Ordinal)
        && !path.Contains(':', StringComparison.Ordinal);

    /// <summary>
    /// Every fluff file a folder's <c>fluff-index.json</c> points at, keyed by name and source.
    /// Files are read in ordinal name order and a later entry wins, which is the rule the bestiary
    /// lookup already follows, so two builds of one folder index the same way.
    /// </summary>
    private static Dictionary<string, JsonElement> Indexed(
        JsonReader reader, string directory, string prefix, string key)
    {
        var index = reader.ReadOptional(Path.Combine(directory, "fluff-index.json"));
        if (index is not { ValueKind: JsonValueKind.Object } listing) return Empty();

        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in listing.EnumerateObject())
        {
            if (Js.AsString(entry.Value) is not { } file || !seen.Add(file)) continue;
            if (!file.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!file.EndsWith(".json", StringComparison.Ordinal)) continue;
            files.Add(file);
        }

        files.Sort(StringComparer.Ordinal);

        var byKey = Empty();
        foreach (var file in files)
        {
            Add(byKey, reader.ReadOptional(Path.Combine(directory, file)), key, file);
        }

        return byKey;
    }

    private static Dictionary<string, JsonElement> OneFile(JsonReader reader, string file, string key)
    {
        var byKey = Empty();
        Add(byKey, reader.ReadOptional(file), key, file);
        return byKey;
    }

    private static void Add(
        Dictionary<string, JsonElement> byKey, JsonElement? document, string key, string file)
    {
        foreach (var fluff in JsonReader.ArrayOrEmpty(document, key, file))
        {
            var name = Js.Get(fluff, "name");
            var source = Js.Get(fluff, "source");
            if (Js.IsTruthy(name) && Js.IsTruthy(source)) byKey[Copy.KeyOf(name, source)] = fluff;
        }
    }

    private static Dictionary<string, JsonElement> Empty() => new(StringComparer.Ordinal);
}
