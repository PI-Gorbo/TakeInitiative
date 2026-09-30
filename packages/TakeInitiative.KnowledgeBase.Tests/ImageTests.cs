using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// The artwork slot (26g): a row's picture, read out of 5eTools' fluff files and written as a URL
/// on their own media host.
/// </summary>
/// <remarks>
/// <para>
/// <b>These cases build their own corpus rather than using <c>Fixture/</c>.</b> That folder is a
/// byte copy of the corpus the Node script's golden output was taken from, and it carries no
/// artwork <c>href</c> at all — which is precisely why 26g's plan says the base URL has to be
/// checked against real data, and why the golden file's 26g diff is one key per row and nothing
/// else. Adding images to the fixture would have made that diff unreadable.
/// </para>
/// <para>
/// <b>What the shapes here are, and where they come from.</b> They are 5eTools' real fluff shapes,
/// written out by hand from their published data with invented names: an entry with its own
/// <c>images</c>, an entry that is a bare <c>_copy</c> of another, an entry that has both, an
/// <c>external</c> href, and a file name with a space, a comma and an apostrophe in it. In their
/// own Monster Manual file 369 monsters say they have artwork while only 244 fluff entries carry an
/// <c>images</c> array, so the <c>_copy</c> case below is the majority of the corpus, not an edge.
/// </para>
/// </remarks>
public class ImageTests
{
    private const string Monsters =
        """
        { "name": "Test Gremlin", "source": "TST", "size": ["S"], "type": "fey", "ac": [15], "hp": { "formula": "3d6+3" }, "dex": 14, "cr": "1" },
        { "name": "Test Gremlin Chief", "source": "TST", "size": ["S"], "type": "fey", "ac": [15], "hp": { "formula": "5d6+10" }, "dex": 14, "cr": "2" },
        { "name": "Test Gremlin Zombie", "source": "TST", "size": ["S"], "type": "undead", "ac": [15], "hp": { "formula": "3d6+3" }, "dex": 14, "cr": "1" },
        { "name": "Warden's Stone, Awakened", "source": "TST", "size": ["L"], "type": "construct", "ac": [17], "hp": { "formula": "10d10+30" }, "dex": 14, "cr": "5" }
        """;

    private const string FluffIndex = """{ "TST": "fluff-bestiary-tst.json" }""";

    private const string Fluff =
        """
        {
            "monsterFluff": [
                {
                    "name": "Test Gremlin",
                    "source": "TST",
                    "entries": ["Fluffword kettlewhistle."],
                    "images": [
                        { "type": "image", "href": { "type": "internal", "path": "bestiary/TST/Test Gremlin.webp" }, "width": 652, "height": 1000 }
                    ]
                },
                { "name": "Test Gremlin Chief", "source": "TST", "_copy": { "name": "Test Gremlin", "source": "TST" } },
                {
                    "name": "Test Gremlin Zombie",
                    "source": "TST",
                    "_copy": { "name": "Test Gremlin", "source": "TST" },
                    "images": [
                        { "type": "image", "href": { "type": "internal", "path": "bestiary/TST/Test Gremlin Zombie.webp" } }
                    ]
                },
                {
                    "name": "Warden's Stone, Awakened",
                    "source": "TST",
                    "images": [
                        { "type": "image", "href": { "type": "internal", "path": "bestiary/TST/Warden's Stone, Awakened.webp" } }
                    ]
                }
            ]
        }
        """;

    private static TemporaryDataFolder WithArtwork() =>
        TemporaryDataFolder.WithMonsters(Monsters)
            .Write("data/bestiary/fluff-index.json", FluffIndex)
            .Write("data/bestiary/fluff-bestiary-tst.json", Fluff);

    private static FiveEToolsIndex Build(TemporaryDataFolder folder) =>
        FiveEToolsParser.Build(new FiveEToolsParserOptions { From = folder.Root, MinMonsters = 1 }).Index;

    /// <summary>
    /// The path in the fluff file becomes a URL under the same host the row's link points at, in
    /// the media folder their own renderer resolves an internal href into.
    /// </summary>
    [Fact]
    public void A_fluff_image_becomes_a_url_on_the_sources_media_host()
    {
        using var folder = WithArtwork();

        var row = Build(folder).Row("monster_test-gremlin_tst");

        Assert.Equal("https://5e.tools/img/bestiary/TST/Test%20Gremlin.webp", row.ImageUrl);
    }

    /// <summary>
    /// A mirror configured with <c>--base-url</c> serves its own images, because the media path
    /// hangs off the same base the deep link does rather than off a second hard-coded host.
    /// </summary>
    [Fact]
    public void The_base_url_carries_the_images_too()
    {
        using var folder = WithArtwork();

        var index = FiveEToolsParser.Build(new FiveEToolsParserOptions
        {
            From = folder.Root,
            MinMonsters = 1,
            BaseUrl = "https://mirror.example/",
        }).Index;

        Assert.Equal("https://mirror.example/img/bestiary/TST/Test%20Gremlin.webp", index.Row("monster_test-gremlin_tst").ImageUrl);
    }

    /// <summary>
    /// Half of a real bestiary's fluff entries are a bare <c>_copy</c> — the Abominable Yeti's
    /// picture is the Yeti's — so a parse that did not follow the chain would leave a third of the
    /// corpus without artwork.
    /// </summary>
    [Fact]
    public void A_fluff_copy_takes_its_bases_picture()
    {
        using var folder = WithArtwork();

        Assert.Equal(
            "https://5e.tools/img/bestiary/TST/Test%20Gremlin.webp",
            Build(folder).Row("monster_test-gremlin-chief_tst").ImageUrl);
    }

    /// <summary>An entry with both its own images and a <c>_copy</c> keeps its own.</summary>
    [Fact]
    public void Its_own_picture_wins_over_the_one_it_copies()
    {
        using var folder = WithArtwork();

        Assert.Equal(
            "https://5e.tools/img/bestiary/TST/Test%20Gremlin%20Zombie.webp",
            Build(folder).Row("monster_test-gremlin-zombie_tst").ImageUrl);
    }

    /// <summary>
    /// Their file names hold spaces, commas and apostrophes, and a stored URL has to carry the
    /// encoding their site leaves to the browser. The apostrophe stays as itself because
    /// <c>encodeURIComponent</c> leaves it alone — the same rule <see cref="Ids.EncodeHash" />
    /// already follows for <c>Warden's Stone (Awakened)</c>'s deep link. Both spellings were
    /// requested against <c>https://5e.tools/img/</c> while 26g was written, with real file names
    /// out of their <c>fluff-items.json</c>, and both answered <c>200 image/webp</c>.
    /// </summary>
    [Fact]
    public void A_file_name_with_punctuation_in_it_is_encoded_segment_by_segment()
    {
        using var folder = WithArtwork();

        Assert.Equal(
            "https://5e.tools/img/bestiary/TST/Warden's%20Stone%2C%20Awakened.webp",
            Build(folder).Row("monster_warden-s-stone-awakened_tst").ImageUrl);
    }

    /// <summary>
    /// A checkout with no fluff files is not a failure — it is a corpus without pictures. The two
    /// <c>fluff-index.json</c>s and <c>fluff-items.json</c> are as optional as <c>books.json</c>.
    /// </summary>
    [Fact]
    public void A_checkout_with_no_fluff_files_has_no_artwork_and_still_builds()
    {
        using var folder = TemporaryDataFolder.WithMonsters(Monsters);

        Assert.All(Build(folder).Items, item => Assert.Null(item.ImageUrl));
        Assert.Equal(4, Build(folder).Items.Count);
    }

    /// <summary>
    /// An <c>external</c> href is an arbitrary third-party URL, and the corpus never holds one:
    /// what a row carries is always a path under the host the operator already chose.
    /// </summary>
    [Fact]
    public void An_external_href_is_not_taken()
    {
        using var folder = TemporaryDataFolder.WithMonsters(Monsters)
            .Write("data/bestiary/fluff-index.json", FluffIndex)
            .Write(
                "data/bestiary/fluff-bestiary-tst.json",
                """
                {
                    "monsterFluff": [
                        {
                            "name": "Test Gremlin",
                            "source": "TST",
                            "images": [
                                { "type": "image", "href": { "type": "external", "url": "https://elsewhere.example/gremlin.png" } }
                            ]
                        }
                    ]
                }
                """);

        Assert.Null(Build(folder).Row("monster_test-gremlin_tst").ImageUrl);
    }

    /// <summary>
    /// A path that could point the row out of the media folder is refused. Nothing in their data
    /// looks like this; the check is here because the string is pasted into a URL.
    /// </summary>
    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("../../secret.webp")]
    [InlineData("https://elsewhere.example/gremlin.webp")]
    [InlineData("bestiary\\TST\\Test Gremlin.webp")]
    public void A_path_that_escapes_the_media_folder_is_refused(string path)
    {
        using var folder = TemporaryDataFolder.WithMonsters(Monsters)
            .Write("data/bestiary/fluff-index.json", FluffIndex)
            .Write(
                "data/bestiary/fluff-bestiary-tst.json",
                $$"""
                {
                    "monsterFluff": [
                        {
                            "name": "Test Gremlin",
                            "source": "TST",
                            "images": [
                                { "type": "image", "href": { "type": "internal", "path": {{System.Text.Json.JsonSerializer.Serialize(path)}} } }
                            ]
                        }
                    ]
                }
                """);

        Assert.Null(Build(folder).Row("monster_test-gremlin_tst").ImageUrl);
    }

    /// <summary>
    /// Nothing but the path leaves the fluff file. The fluff entries here carry the invented words
    /// the allowlist tests hunt for, and a build that reached into one for anything else would say
    /// so here.
    /// </summary>
    [Fact]
    public void No_fluff_text_comes_with_the_picture()
    {
        using var folder = WithArtwork();

        var text = FiveEToolsIndexSerializer.Serialize(Build(folder));

        Assert.DoesNotContain("Fluffword", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kettlewhistle", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"width\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The artwork is part of a row's content hash, so a source that moved a picture reports the
    /// row as updated rather than leaving a dead URL in the table for ever.
    /// </summary>
    [Fact]
    public void A_changed_picture_changes_the_rows_hash()
    {
        using var withArt = WithArtwork();
        using var without = TemporaryDataFolder.WithMonsters(Monsters);

        var hashed = Store.KnowledgeBaseRow.HashOf(Build(withArt).Row("monster_test-gremlin_tst"));
        var plain = Store.KnowledgeBaseRow.HashOf(Build(without).Row("monster_test-gremlin_tst"));

        Assert.NotEqual(plain, hashed);
        Assert.Equal(
            "https://5e.tools/img/bestiary/TST/Test%20Gremlin.webp",
            Store.KnowledgeBaseRow.From("5etools", Build(withArt).Row("monster_test-gremlin_tst")).ImageUrl);
    }
}
