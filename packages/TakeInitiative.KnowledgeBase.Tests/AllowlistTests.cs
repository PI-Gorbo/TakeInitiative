using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// What the index is allowed to carry. Ported from <c>build-5etools-index.test.mjs</c>'s
/// "the allowlist" block.
/// </summary>
/// <remarks>
/// These are the tests that stand in for a licence. 5eTools' data is copyrighted and not licensed
/// for reuse, so the index holds identifiers and a few numbers and nothing else. The last case in
/// here is the blunt one: it serialises the whole fixture and looks for the invented words the
/// fixture's rules text, traits, actions, fluff, tags and token URLs are seeded with. If any of them
/// appears, something in the parser started copying their objects instead of picking fields.
/// </remarks>
public class AllowlistTests
{
    [Fact]
    public void A_fixture_monster_has_exactly_the_allowed_keys()
    {
        var gremlin = Fixture5eTools.Build().Index.Row("monster_test-gremlin_tst");

        Assert.Equal(
            new[] { "id", "name", "category", "source", "page", "url", "imageUrl", "label", "stats" },
            FiveEToolsIndexSerializer.ItemKeysWritten);
        Assert.Equal(new[] { "ac", "hp", "initiativeBonus" }, FiveEToolsIndexSerializer.StatsKeysWritten);

        Assert.Equal(
            new KnowledgeBaseItem
            {
                Id = "monster_test-gremlin_tst",
                Name = "Test Gremlin",
                Category = "Monster",
                Source = "TST",
                Page = 12,
                Url = "https://5e.tools/bestiary.html#test%20gremlin_tst",
                // The fixture names no artwork, which is why 26g's base URL had to be checked
                // against real data instead: see FiveEToolsImages.
                ImageUrl = null,
                Label = "CR 1/2 · Small Fey",
                Stats = new KnowledgeBaseItemStats(15, "3d6+3", 2),
            },
            gremlin);
    }

    /// <summary>
    /// The keys a build writes are the keys the allowlist names, at every level.
    /// </summary>
    /// <remarks>
    /// In the Node script this was a per-row check, because a row was a plain object that anything
    /// could add a key to. Here a row is a record and the serialiser writes it from a table, so the
    /// question becomes whether that table and the allowlist still agree — which is what
    /// <c>FiveEToolsParser.Check</c> asserts on every build, and what this asserts directly. Adding
    /// a field to <see cref="KnowledgeBaseItem" /> and to the serialiser's table, without adding it
    /// to <see cref="FiveEToolsAllowlist" />, fails the build.
    /// </remarks>
    [Fact]
    public void Every_row_and_the_file_have_only_allowed_keys()
    {
        Assert.Equal(FiveEToolsAllowlist.TopKeys, FiveEToolsIndexSerializer.TopKeysWritten);
        Assert.Equal(FiveEToolsAllowlist.ItemKeys, FiveEToolsIndexSerializer.ItemKeysWritten);
        Assert.Equal(FiveEToolsAllowlist.StatsKeys, FiveEToolsIndexSerializer.StatsKeysWritten);

        // And the build ran its own check over all fourteen rows without raising.
        Assert.Equal(14, Fixture5eTools.Build().Index.Items.Count);
    }

    [Theory]
    [InlineData("zorblatt")]
    [InlineData("Traitword")]
    [InlineData("Actionword")]
    [InlineData("Entryword")]
    [InlineData("Fluffword")]
    [InlineData("Modword")]
    [InlineData("tagword")]
    [InlineData("kettlewhistle")]
    [InlineData("quibbleflux")]
    [InlineData("token")]
    [InlineData("Sylvan")]
    [InlineData("darkvision")]
    [InlineData("{@")]
    public void No_rules_text_fluff_tag_or_token_reaches_the_output(string word)
    {
        var text = FiveEToolsIndexSerializer.Serialize(Fixture5eTools.Build().Index);

        Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
    }
}
