using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// The guards that stop a bad parse becoming a bad ingest. Ported from
/// <c>build-5etools-index.test.mjs</c>'s "the command line" block, minus the parts that are about
/// argument parsing — those belong to the CLI in 26c.
/// </summary>
/// <remarks>
/// Every case in here ends in nothing being returned. That is the point: 26c writes to Postgres from
/// whatever <see cref="FiveEToolsParser.Build" /> hands back, and a half-built index would not look
/// like a failure, it would look like a source that had lost thousands of rows — which is exactly
/// the situation the prune threshold is there to catch and exactly the wrong place to first notice.
/// </remarks>
public class BuildGuardTests
{
    /// <summary>
    /// A folder with no <c>bestiary/</c> under it and none under a <c>data/</c> is not a 5eTools
    /// checkout, and is named in the message so an operator can see what they pointed at.
    /// </summary>
    [Fact]
    public void A_folder_that_is_not_a_5etools_checkout_fails()
    {
        using var folder = TemporaryDataFolder.Empty();

        var error = Assert.Throws<FiveEToolsBuildException>(() => FiveEToolsParser.Build(
            new FiveEToolsParserOptions { From = folder.Root, MinMonsters = 1 }));

        Assert.Contains("no 5etools data/ folder", error.Message, StringComparison.Ordinal);
        Assert.Contains(folder.Root, error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The real fixture holds eight monsters, and the default floor is a thousand. A count that low
    /// means the operator pointed at a partial download, one file, or an unrelated folder; writing it
    /// would look like 5eTools had shrunk.
    /// </summary>
    [Fact]
    public void Too_few_monsters_fails_with_the_count_and_the_floor()
    {
        var error = Assert.Throws<FiveEToolsBuildException>(() => FiveEToolsParser.Build(
            new FiveEToolsParserOptions { From = Fixture5eTools.Checkout }));

        Assert.Equal(
            "only 8 monsters; expected at least 1000 (is --from the right folder?)",
            error.Message);
    }

    /// <summary>
    /// Two different names that slug to one id are fatal. The id is the primary key and what a link
    /// points at, so the alternative — last one wins — would silently move a user's link onto a
    /// different monster.
    /// </summary>
    [Fact]
    public void Two_rows_wanting_the_same_id_fail()
    {
        using var folder = TemporaryDataFolder.WithMonsters(
            """
            { "name": "Gremlin Chief", "source": "TST", "size": ["S"], "type": "fey", "ac": [15], "hp": { "formula": "3d6+3" }, "dex": 14, "cr": "1" },
            { "name": "Gremlin  Chief!", "source": "TST", "size": ["S"], "type": "fey", "ac": [15], "hp": { "formula": "3d6+3" }, "dex": 14, "cr": "1" }
            """);

        var error = Assert.Throws<FiveEToolsBuildException>(() => FiveEToolsParser.Build(
            new FiveEToolsParserOptions { From = folder.Root, MinMonsters = 1 }));

        Assert.Equal("duplicate id monster_gremlin-chief_tst", error.Message);
    }

    /// <summary>A size letter that is not in the enum means their format moved; look, do not guess.</summary>
    [Fact]
    public void An_unknown_size_fails()
    {
        using var folder = TemporaryDataFolder.WithMonsters(
            """{ "name": "Wrong Size", "source": "TST", "size": ["Z"], "type": "fey", "ac": [15], "hp": { "formula": "3d6+3" }, "dex": 14, "cr": "1" }""");

        var error = Assert.Throws<FiveEToolsBuildException>(() => FiveEToolsParser.Build(
            new FiveEToolsParserOptions { From = folder.Root, MinMonsters = 1 }));

        Assert.Equal("monster \"Wrong Size\" (TST): unknown size \"Z\"", error.Message);
    }

    /// <summary>
    /// A folder with a bestiary but no <c>package.json</c> has no version, and that is a provenance
    /// note rather than a failure.
    /// </summary>
    [Fact]
    public void A_folder_with_no_package_json_has_no_version()
    {
        using var folder = TemporaryDataFolder.WithMonsters(
            """{ "name": "Lonely Beast", "source": "TST", "size": ["S"], "type": "beast", "ac": [12], "hp": { "formula": "2d6" }, "dex": 10, "cr": "1/4" }""");

        var (index, report) = FiveEToolsParser.Build(
            new FiveEToolsParserOptions { From = folder.Root, MinMonsters = 1 });

        Assert.Null(index.FiveEToolsVersion);
        Assert.Equal(new FiveEToolsCounts(1, 0, 0), index.Counts);

        // No spells folder and no items files: absent is not an error, it is an empty category.
        Assert.Equal(0, report.Read.Spell);
        Assert.Equal(0, report.Read.Item);

        // A source with no entry in books.json or adventures.json shows its own abbreviation.
        Assert.Equal("TST", index.Sources["TST"]);
    }

    /// <summary>
    /// A bestiary file that is not JSON fails with the file named, rather than throwing something
    /// the CLI would have to translate.
    /// </summary>
    [Fact]
    public void A_file_that_is_not_json_fails_with_its_path()
    {
        using var folder = TemporaryDataFolder.WithMonsters("");
        var broken = Path.Combine(folder.Bestiary, "bestiary-tst.json");
        File.WriteAllText(broken, "{ not json");

        var error = Assert.Throws<FiveEToolsBuildException>(() => FiveEToolsParser.Build(
            new FiveEToolsParserOptions { From = folder.Root, MinMonsters = 0 }));

        Assert.Contains(broken, error.Message, StringComparison.Ordinal);
        Assert.StartsWith("cannot read ", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A bestiary folder with no <c>index.json</c> is a build error, not an empty category: the folder
    /// is there, so something is wrong with it.
    /// </summary>
    [Fact]
    public void A_bestiary_folder_with_no_index_fails()
    {
        using var folder = TemporaryDataFolder.Empty();
        Directory.CreateDirectory(Path.Combine(folder.Root, "data", "bestiary"));

        var error = Assert.Throws<FiveEToolsBuildException>(() => FiveEToolsParser.Build(
            new FiveEToolsParserOptions { From = folder.Root, MinMonsters = 0 }));

        Assert.Contains("index.json", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// So is a missing spells index, which is worth pinning because it is asymmetric: the bestiary's
    /// and the spells' <c>index.json</c> are required, while <c>items.json</c>,
    /// <c>items-base.json</c>, <c>books.json</c> and <c>adventures.json</c> are optional and a
    /// missing one simply means an empty category. The Node script reads them that way too, and this
    /// port reproduces it rather than softening it — a 5eTools folder with no spells is not a 5eTools
    /// folder.
    /// </summary>
    [Fact]
    public void A_data_folder_with_no_spells_index_fails()
    {
        using var folder = TemporaryDataFolder.WithMonsters("");
        Directory.Delete(Path.Combine(folder.Root, "data", "spells"), recursive: true);

        var error = Assert.Throws<FiveEToolsBuildException>(() => FiveEToolsParser.Build(
            new FiveEToolsParserOptions { From = folder.Root, MinMonsters = 0 }));

        Assert.Contains(Path.Combine("spells", "index.json"), error.Message, StringComparison.Ordinal);
    }
}
