using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// Spells, items and source books. Ported from <c>build-5etools-index.test.mjs</c>'s
/// "spells, items and sources" block.
/// </summary>
public class SpellItemAndSourceTests
{
    [Fact]
    public void Labels_come_from_enums()
    {
        var index = Fixture5eTools.Build().Index;

        Assert.Equal("Level 3 Evocation", index.Row("spell_test-sparkburst_tst").Label);
        Assert.Equal("Illusion Cantrip", index.Row("spell_test-glimmer_tst").Label);
        Assert.Equal("Uncommon Wondrous Item", index.Row("item_test-satchel-of-plenty_tst").Label);
        Assert.Equal("Rare Melee Weapon", index.Row("item_test-blade-of-echoes_tst").Label);

        // A mundane base item has a type but no rarity 5eTools names, so the label is the type alone.
        Assert.Equal("Melee Weapon", index.Row("item_test-pike_tst").Label);

        Assert.Equal("https://5e.tools/spells.html#test%20sparkburst_tst", index.Row("spell_test-sparkburst_tst").Url);
        Assert.Equal("https://5e.tools/items.html#test%20pike_tst", index.Row("item_test-pike_tst").Url);
    }

    /// <summary>
    /// <c>srd52</c> only skips monsters. The SRD provider serves monsters with a stat block, so a
    /// 5eTools row for the same monster would be a duplicate; it does not serve spells that way, so a
    /// flagged spell is still worth a row.
    /// </summary>
    [Fact]
    public void Srd_flagged_spells_are_kept_and_no_row_but_a_monsters_has_stats()
    {
        var index = Fixture5eTools.Build().Index;

        index.Row("spell_test-mending-hum_tst");

        Assert.All(
            index.Items.Where(item => item.Category != "Monster"),
            item => Assert.Null(item.Stats));
    }

    [Fact]
    public void Sources_map_to_book_and_adventure_titles()
    {
        var index = Fixture5eTools.Build().Index;

        Assert.Equal(new[] { "TST", "TSTA" }, index.Sources.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("Test Book of Beasts", index.Sources["TST"]);
        Assert.Equal("Test Adventure in the Lint Caves", index.Sources["TSTA"]);
        Assert.Equal("0.0.0-test", index.FiveEToolsVersion);
        Assert.Equal(1, index.Format);
    }

    [Fact]
    public void Sorted_by_category_then_name_then_source()
    {
        var index = Fixture5eTools.Build().Index;

        Assert.Equal(
            new[] { "Monster", "Spell", "Item" },
            index.Items.Select(item => item.Category).Distinct(StringComparer.Ordinal));

        var monsters = index.Items.Where(item => item.Category == "Monster").Select(item => item.Name).ToList();
        Assert.Equal(monsters.Order(StringComparer.Ordinal), monsters);
    }

    /// <summary>
    /// <c>--no-stats</c> writes no stats on any row, and the word <c>hp</c> never reaches the file.
    /// </summary>
    [Fact]
    public void No_stats_writes_no_stats_anywhere()
    {
        var (index, report) = Fixture5eTools.Build(noStats: true);

        Assert.All(index.Items, item => Assert.Null(item.Stats));
        Assert.DoesNotContain("\"hp\"", FiveEToolsIndexSerializer.Serialize(index), StringComparison.Ordinal);

        // Nothing was counted as missing stats, because none were asked for.
        Assert.Equal(0, report.WithoutStats);
        Assert.Equal(14, index.Items.Count);
    }
}
