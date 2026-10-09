using System.Text.Json;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// Source-book abbreviation to full title, ported from <c>build-5etools-index.mjs</c> lines
/// 227-234.
/// </summary>
/// <remarks>
/// <para>
/// A row's <c>source</c> is an abbreviation — <c>MM</c>, <c>XGE</c>, <c>TCE</c> — which is fine for
/// a chip but not for the line under a name. <c>data/books.json</c> and
/// <c>data/adventures.json</c> are 5eTools' own catalogue of what those abbreviations mean, and a
/// book's title is a bibliographic fact rather than their content, so the index carries it.
/// </para>
/// <para>
/// Books are read before adventures and the first title for an abbreviation wins, because the two
/// files overlap for a handful of sources and the book is the better answer. Either file may be
/// missing, in which case a source shows its own abbreviation as its title.
/// </para>
/// </remarks>
public static class Books
{
    private static readonly (string File, string Key)[] Catalogues =
        [("books.json", "book"), ("adventures.json", "adventure")];

    /// <summary>
    /// Reads the two catalogues out of a 5eTools <c>data/</c> folder.
    /// </summary>
    internal static Dictionary<string, string> ReadTitles(string dataDirectory, JsonReader reader)
    {
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (file, key) in Catalogues)
        {
            var path = Path.Combine(dataDirectory, file);
            foreach (var book in JsonReader.ArrayOrEmpty(reader.ReadOptional(path), key, path))
            {
                // `b.source ?? b.id`: adventures key off `id`, books carry both.
                var abbreviation = Js.AsString(Js.Get(book, "source") is { ValueKind: not JsonValueKind.Null } source
                    ? source
                    : Js.Get(book, "id"));
                var name = Js.AsString(Js.Get(book, "name"));

                if (abbreviation is not null && name is not null) titles.TryAdd(abbreviation, name);
            }
        }

        return titles;
    }
}
