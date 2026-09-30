using System.Text.Json;
using System.Text.RegularExpressions;

namespace TakeInitiative.KnowledgeBase.FiveETools;

/// <summary>
/// Builds a knowledge base index from a local copy of the 5eTools source data. A port of
/// <c>scripts/5etools/build-5etools-index.mjs</c> (roadmap step 21a), which stays the
/// specification: <c>packages/TakeInitiative.KnowledgeBase.Tests/Golden/fixture-index.json</c> is
/// that script's output for the fixture corpus, and the golden test asserts this parser writes the
/// same bytes.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here downloads anything, ever. The operator supplies a folder; a flag that fetched
/// 5eTools' data would put automation for retrieving book content into a public repository.
/// </para>
/// <para>
/// What the parse keeps is fixed by <see cref="FiveEToolsAllowlist" /> and checked on every build.
/// What it leaves out is as deliberate:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     Unearthed Arcana sources, because they are drafts and their rows churn — a link to one would
///     rot.
///     </description>
///   </item>
///   <item>
///     <description>
///     Monsters flagged <c>srd52</c>, because the SRD 5.2 provider already serves those and serves
///     them better: the SRD is licensed, so its rows may show a stat block where a 5eTools row may
///     only link out. Two rows for one monster would be a worse answer, not a fuller one.
///     </description>
///   </item>
///   <item>
///     <description>
///     Rows with no name or no source, which cannot be given a stable id at all.
///     </description>
///   </item>
///   <item>
///     <description>
///     Monsters with no hit points and no <c>_copy</c> to inherit them from. In 5eTools' data that
///     combination means a placeholder rather than a creature.
///     </description>
///   </item>
///   <item>
///     <description>
///     Monsters whose <c>_copy</c> names a base that is not in the folder — see
///     <see cref="Copy" />. Those are reported by name, because they usually mean a partial
///     download.
///     </description>
///   </item>
/// </list>
/// </remarks>
public static partial class FiveEToolsParser
{
    /// <summary>
    /// The index format's version. Bump it when the shape of a row changes, so 26c can tell an
    /// index it understands from one it does not.
    /// </summary>
    public const int Format = 1;

    /// <summary>
    /// <c>^[\w.+-]+$</c> from line 225. A version is written into the index, so it is checked
    /// rather than trusted: <c>\w</c> is ASCII in JavaScript, hence the explicit class.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_.+-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionShape();

    /// <summary>
    /// Reads a 5eTools data folder and builds the index.
    /// </summary>
    /// <exception cref="FiveEToolsBuildException">
    /// The folder is not a 5eTools checkout, a file it holds is not JSON, an enum it holds is one
    /// this parser does not know, two rows want the same id, there are fewer monsters than
    /// <see cref="FiveEToolsParserOptions.MinMonsters" />, or a row would carry a key outside the
    /// allowlist. Every one of those means the output cannot be trusted, so nothing is returned
    /// rather than something partial: 26c writes to the database from this, and a half-built index
    /// would look like a source that had lost thousands of rows.
    /// </exception>
    public static FiveEToolsBuildResult Build(FiveEToolsParserOptions options)
    {
        using var reader = new JsonReader();

        var (data, checkout) = FindDataDirectory(options.From);
        var baseUrl = options.BaseUrl.TrimEnd('/');

        var readMonsters = 0;
        var readSpells = 0;
        var readItems = 0;
        var skippedUa = 0;
        var skippedSrd52 = 0;
        var skippedNoName = 0;
        var missingBase = new List<string>();
        var broken = new List<string>();
        var templates = 0;
        var withoutStats = 0;

        var package = reader.ReadOptional(Path.Combine(checkout, "package.json"));
        var version = Js.AsString(Js.Get(package, "version")) is { } stated && VersionShape().IsMatch(stated)
            ? stated
            : null;

        var titles = Books.ReadTitles(data, reader);
        var rows = new List<KnowledgeBaseItem>();

        // A row worth writing: it has a name and a source, and its source is not Unearthed Arcana.
        (string Name, string Source)? Usable(JsonElement raw)
        {
            if (Js.AsString(Js.Get(raw, "name")) is not { Length: > 0 } name
                || Js.AsString(Js.Get(raw, "source")) is not { Length: > 0 } source)
            {
                skippedNoName++;
                return null;
            }

            if (source.StartsWith("UA", StringComparison.OrdinalIgnoreCase))
            {
                skippedUa++;
                return null;
            }

            return (name, source);
        }

        // --- monsters ---------------------------------------------------------------------------

        var bestiaryDirectory = Path.Combine(data, "bestiary");
        var bestiary = ReadIndexed(reader, bestiaryDirectory, "bestiary-", "monster");

        // Every monster the data holds, whether or not it will be written, because a `_copy` may
        // name a base that is itself skipped — a UA creature, or an SRD 5.2 duplicate.
        var byKey = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var monster in bestiary)
        {
            var name = Js.Get(monster, "name");
            var source = Js.Get(monster, "source");
            if (Js.IsTruthy(name) && Js.IsTruthy(source)) byKey[Copy.KeyOf(name, source)] = monster;
        }

        foreach (var raw in bestiary)
        {
            readMonsters++;
            if (Usable(raw) is not { } row) continue;

            if (Js.IsTruthy(Js.Get(raw, "srd52")))
            {
                skippedSrd52++;
                continue;
            }

            var where = $"monster \"{row.Name}\" ({row.Source})";
            var copy = Js.Get(raw, "_copy");

            if (!Js.IsTruthy(Js.Get(raw, "hp")) && !Js.IsTruthy(copy))
            {
                broken.Add($"{row.Name} ({row.Source})");
                continue;
            }

            if (Js.IsTruthy(Js.Get(copy, "_templates"))) templates++;

            var monster = Copy.Resolve(raw, byKey);
            if (monster is null)
            {
                missingBase.Add($"{row.Name} ({row.Source}) copies " +
                                $"{Js.ToJsString(Js.Get(copy, "name"))} ({Js.ToJsString(Js.Get(copy, "source"))})");
                continue;
            }

            KnowledgeBaseItemStats? stats = null;
            if (!options.NoStats)
            {
                if (Bestiary.AcOf(monster["ac"]) is { } ac
                    && Bestiary.HpDiceOf(monster["hp"]) is { } hp
                    && Bestiary.InitiativeOf(monster) is { } initiativeBonus)
                {
                    stats = new KnowledgeBaseItemStats(ac, hp, initiativeBonus);
                }
                else
                {
                    withoutStats++;
                }
            }

            rows.Add(Pick("Monster", raw, row, Bestiary.MonsterLabel(monster, where), stats, baseUrl));
        }

        // --- spells -----------------------------------------------------------------------------

        foreach (var raw in ReadIndexed(reader, Path.Combine(data, "spells"), "spells-", "spell"))
        {
            readSpells++;
            if (Usable(raw) is not { } row) continue;

            var label = Spells.SpellLabel(raw, $"spell \"{row.Name}\" ({row.Source})");
            rows.Add(Pick("Spell", raw, row, label, null, baseUrl));
        }

        // --- items: magic and mundane items, then base items (weapons, armour, gear) -------------

        var magicFile = Path.Combine(data, "items.json");
        var baseFile = Path.Combine(data, "items-base.json");
        var allItems = JsonReader.ArrayOrEmpty(reader.ReadOptional(magicFile), "item", magicFile)
            .Concat(JsonReader.ArrayOrEmpty(reader.ReadOptional(baseFile), "baseitem", baseFile))
            .ToList();

        var itemsByKey = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in allItems)
        {
            var name = Js.Get(item, "name");
            var source = Js.Get(item, "source");
            if (Js.IsTruthy(name) && Js.IsTruthy(source)) itemsByKey[Copy.KeyOf(name, source)] = item;
        }

        foreach (var raw in allItems)
        {
            readItems++;
            if (Usable(raw) is not { } row) continue;

            // An item's `_copy` is a shallow override rather than the bestiary's chain: a base item
            // is never itself a copy. A base that is missing leaves the item with its own fields.
            JsonElement? baseItem = null;
            var copy = Js.Get(raw, "_copy");
            if (Js.IsTruthy(copy)
                && itemsByKey.TryGetValue(Copy.KeyOf(Js.Get(copy, "name"), Js.Get(copy, "source")), out var found))
            {
                baseItem = found;
            }

            rows.Add(Pick("Item", raw, row, Items.ItemLabel(raw, baseItem), null, baseUrl));
        }

        // --- the guards -------------------------------------------------------------------------

        var monsterCount = rows.Count(item => item.Category == "Monster");
        if (monsterCount < options.MinMonsters)
        {
            throw new FiveEToolsBuildException(
                $"only {monsterCount} monsters; expected at least {options.MinMonsters} (is --from the right folder?)");
        }

        // Category, then name, then source, all ordinal. OrderBy is a stable sort, which is what
        // JavaScript's Array.prototype.sort gives, so two rows alike in all three keep the order the
        // data had them in.
        var sorted = rows
            .OrderBy(CategoryRank)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .ToList();

        // A duplicate id is fatal rather than deduplicated. The id is the table's primary key and
        // what a user's link points at, so two rows claiming one id means a link would resolve to
        // whichever of them was written last.
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in sorted)
        {
            if (!ids.Add(item.Id)) throw new FiveEToolsBuildException($"duplicate id {item.Id}");
            Check(item);
        }

        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sorted.Select(item => item.Source).Distinct(StringComparer.Ordinal))
        {
            sources[source] = titles.TryGetValue(source, out var title) ? title : source;
        }

        var index = new FiveEToolsIndex
        {
            Format = Format,
            FiveEToolsVersion = version,
            Counts = new FiveEToolsCounts(
                monsterCount,
                sorted.Count(item => item.Category == "Spell"),
                sorted.Count(item => item.Category == "Item")),
            Sources = sources,
            Items = sorted,
        };

        if (!FiveEToolsIndexSerializer.TopKeysWritten.SequenceEqual(FiveEToolsAllowlist.TopKeys, StringComparer.Ordinal))
        {
            throw new FiveEToolsBuildException("the index has a key outside the allowlist");
        }

        var report = new FiveEToolsBuildReport(
            new FiveEToolsReadCounts(readMonsters, readSpells, readItems),
            new FiveEToolsSkipped(skippedUa, skippedSrd52, missingBase, broken, skippedNoName),
            templates,
            withoutStats);

        return new FiveEToolsBuildResult(index, report);
    }

    /// <summary>
    /// A 5eTools checkout, or its <c>data/</c> folder. The checkout is worth finding on its own: its
    /// <c>package.json</c> is where the version in the index comes from.
    /// </summary>
    public static (string Data, string Checkout) FindDataDirectory(string from)
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(from));

        if (Path.Exists(Path.Combine(directory, "data", "bestiary")))
        {
            return (Path.Combine(directory, "data"), directory);
        }

        if (Path.Exists(Path.Combine(directory, "bestiary")))
        {
            return (directory, Path.GetDirectoryName(directory) ?? directory);
        }

        throw new FiveEToolsBuildException(
            $"no 5etools data/ folder in {directory} (expected data/bestiary/ or bestiary/)");
    }

    /// <summary>
    /// Reads every file a 5eTools <c>index.json</c> points at. Their folders hold one file per
    /// source plus "fluff" files of artwork and flavour text, and <c>index.json</c> maps a source to
    /// its file. Only files with the right prefix, no "fluff" in the name and a <c>.json</c> suffix
    /// are read, sorted by name so the order does not depend on the index's own.
    /// </summary>
    private static List<JsonElement> ReadIndexed(JsonReader reader, string directory, string prefix, string key)
    {
        var indexFile = Path.Combine(directory, "index.json");
        var index = reader.Read(indexFile);

        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (index.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in index.EnumerateObject())
            {
                if (Js.AsString(entry.Value) is not { } file || !seen.Add(file)) continue;
                if (!file.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (file.Contains("fluff", StringComparison.Ordinal)) continue;
                if (!file.EndsWith(".json", StringComparison.Ordinal)) continue;
                files.Add(file);
            }
        }

        files.Sort(StringComparer.Ordinal);

        var rows = new List<JsonElement>();
        foreach (var file in files)
        {
            var path = Path.Combine(directory, file);
            rows.AddRange(JsonReader.ArrayOrEmpty(reader.Read(path), key, path));
        }

        return rows;
    }

    /// <summary>
    /// Builds one row from named fields. Nothing here spreads or copies a 5eTools object: every
    /// value is either one this repository computed or a field the allowlist names.
    /// </summary>
    private static KnowledgeBaseItem Pick(
        string category,
        JsonElement raw,
        (string Name, string Source) row,
        string? label,
        KnowledgeBaseItemStats? stats,
        string baseUrl)
    {
        if (!FiveEToolsAllowlist.Pages.TryGetValue(category, out var sitePage))
        {
            throw new FiveEToolsBuildException($"unknown category {category}");
        }

        return new KnowledgeBaseItem
        {
            Id = Ids.MakeId(category, row.Name, row.Source),
            Name = row.Name,
            Category = category,
            Source = row.Source,
            Page = Js.AsInteger(Js.Get(raw, "page")),
            Url = $"{baseUrl}/{sitePage}.html#{Ids.EncodeHash(row.Name, row.Source)}",
            Label = label,
            Stats = stats,
        };
    }

    /// <summary>
    /// The last gate before a row is written: it carries exactly the allowlist's keys and nothing
    /// else, and what it carries is the right shape.
    /// </summary>
    /// <remarks>
    /// Some of the script's checks are gone because the type system now makes them unreachable — a
    /// name or a source that is not text, a label that is neither text nor null, an AC or an
    /// initiative bonus that is not a whole number. The ones that remain can still go wrong: the key
    /// lists are checked against the allowlist so a widened row fails rather than ships, stats on
    /// anything but a monster would be a parser bug, and the hit-point dice are re-checked against
    /// the same shape they were read with, because that string is handed to the dice evaluator.
    /// </remarks>
    private static void Check(KnowledgeBaseItem item)
    {
        if (!FiveEToolsIndexSerializer.ItemKeysWritten.SequenceEqual(FiveEToolsAllowlist.ItemKeys, StringComparer.Ordinal))
        {
            throw new FiveEToolsBuildException(
                $"{item.Id}: keys {string.Join(", ", FiveEToolsIndexSerializer.ItemKeysWritten)} are not the allowlist");
        }

        if (item.Stats is not { } stats) return;

        if (item.Category != "Monster")
        {
            throw new FiveEToolsBuildException($"{item.Id}: only monsters have stats");
        }

        if (!FiveEToolsIndexSerializer.StatsKeysWritten.SequenceEqual(FiveEToolsAllowlist.StatsKeys, StringComparer.Ordinal))
        {
            throw new FiveEToolsBuildException($"{item.Id}: stats keys are not the allowlist");
        }

        if (!Bestiary.HpDiceShape().IsMatch(stats.Hp))
        {
            throw new FiveEToolsBuildException($"{item.Id}: HP \"{stats.Hp}\" is not NdM[+-K]");
        }
    }

    private static int CategoryRank(KnowledgeBaseItem item)
    {
        for (var i = 0; i < FiveEToolsAllowlist.Categories.Count; i++)
        {
            if (string.Equals(FiveEToolsAllowlist.Categories[i], item.Category, StringComparison.Ordinal)) return i;
        }

        return -1;
    }
}
