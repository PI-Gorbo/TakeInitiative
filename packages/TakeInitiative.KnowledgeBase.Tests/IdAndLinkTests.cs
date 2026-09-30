using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// Ids and links. Ported from <c>build-5etools-index.test.mjs</c>'s "ids and links" block.
/// </summary>
/// <remarks>
/// An id is a database primary key that a user's entry link points at (step 27), so these cases are
/// the ones that would break existing links if the port drifted. They are written as literals rather
/// than derived, deliberately: the right answer is "what the Node script produced", and a shared
/// helper would be able to be wrong in both places at once.
/// </remarks>
public class IdAndLinkTests
{
    /// <summary>
    /// The link encoder keeps what <c>encodeURIComponent</c> keeps — the apostrophe and the
    /// parentheses included — and percent-encodes the rest in upper-case hex.
    /// </summary>
    [Theory]
    [InlineData("Goblin Boss", "XMM", "goblin%20boss_xmm")]
    [InlineData("Warden's Stone (Awakened)", "TST", "warden's%20stone%20(awakened)_tst")]
    [InlineData("Test Gremlin, Understudy", "TST", "test%20gremlin%2C%20understudy_tst")]
    [InlineData("Bag of Holding", "XDMG", "bag%20of%20holding_xdmg")]
    public void The_link_encoder_handles_spaces_apostrophes_commas_and_parentheses(
        string name,
        string source,
        string hash) =>
        Assert.Equal(hash, Ids.EncodeHash(name, source));

    [Theory]
    [InlineData("Monster", "Goblin Boss", "XMM", "monster_goblin-boss_xmm")]
    [InlineData("Monster", "Warden's Stone (Awakened)", "TST", "monster_warden-s-stone-awakened_tst")]
    [InlineData("Item", "Bag of Holding", "XDMG", "item_bag-of-holding_xdmg")]
    public void Ids_are_slugs(string category, string name, string source, string id) =>
        Assert.Equal(id, Ids.MakeId(category, name, source));

    /// <summary>
    /// An accented name decomposes and loses its marks, so the id stays ASCII and a re-ingest from a
    /// file saved under a different normalisation still produces the same key.
    /// </summary>
    [Theory]
    [InlineData("Déjà Vu", "deja-vu")]
    [InlineData("  Spaced  Out  ", "spaced-out")]
    [InlineData("Mummy Lord (Variant)", "mummy-lord-variant")]
    public void Slugs_normalise_and_trim(string name, string slug) => Assert.Equal(slug, Ids.Slug(name));

    /// <summary>
    /// A name with nothing alphanumeric in it cannot be given an id, and that fails the build rather
    /// than emitting a row nothing could ever link to.
    /// </summary>
    [Fact]
    public void A_name_that_slugs_to_nothing_fails_the_build()
    {
        var error = Assert.Throws<FiveEToolsBuildException>(() => Ids.MakeId("Monster", "———", "TST"));

        Assert.Contains("cannot make an id for Monster", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An empty source fails the same way. A source of punctuation does not: line 90 maps its
    /// non-alphanumerics to dashes without trimming them, and a lone dash satisfies line 91's shape,
    /// so <c>monster_beholder_-</c> is an id the script would have written and this one writes too.
    /// </summary>
    [Fact]
    public void An_empty_source_fails_the_build_and_punctuation_does_not()
    {
        Assert.Throws<FiveEToolsBuildException>(() => Ids.MakeId("Monster", "Beholder", ""));
        Assert.Equal("monster_beholder_-", Ids.MakeId("Monster", "Beholder", "!!"));
    }

    [Fact]
    public void A_base_url_overrides_the_host()
    {
        var index = Fixture5eTools.Build(baseUrl: "https://example.test/").Index;

        Assert.Equal(
            "https://example.test/bestiary.html#test%20gremlin_tst",
            index.Row("monster_test-gremlin_tst").Url);
    }
}
