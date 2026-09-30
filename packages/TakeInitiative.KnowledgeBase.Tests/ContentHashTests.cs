using TakeInitiative.KnowledgeBase.Store;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// The content hash the diff compares rows by (26c).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this matters more than it looks.</b> <c>updated</c> only means something if a row that did
/// not change hashes the same twice. If it does not, every run reports every row as updated,
/// <c>unchanged</c> is always zero, and the prune threshold — which is read off the same diff — stops
/// being something an operator can believe. The safety rails in this step are only as good as this
/// number.
/// </para>
/// <para>
/// So the hash is taken over <c>FiveEToolsIndexSerializer.SerializeItem</c>, which is the one
/// byte-stable spelling of a row in the codebase and the one 26b's golden-file test already holds
/// still. These cases prove the property end to end rather than by inspection: parse the fixture
/// twice, in two processes' worth of independent object graphs, and compare.
/// </para>
/// </remarks>
public class ContentHashTests
{
    /// <summary>
    /// Two parses of one folder produce the same hash for every row. This is the determinism the
    /// whole diff rests on.
    /// </summary>
    [Fact]
    public void Two_parses_of_one_folder_hash_every_row_the_same()
    {
        var first = KnowledgeBaseRow.From("5etools", Fixture5eTools.Build().Index);
        var second = KnowledgeBaseRow.From("5etools", Fixture5eTools.Build().Index);

        Assert.NotEmpty(first);
        Assert.Equal(first.Select(row => row.Id), second.Select(row => row.Id));
        Assert.Equal(first.Select(row => row.ContentHash), second.Select(row => row.ContentHash));
    }

    /// <summary>
    /// And no two rows of the fixture share a hash, so "the hashes matched" cannot be an accident of
    /// every row hashing to the same thing.
    /// </summary>
    [Fact]
    public void Every_row_of_the_fixture_hashes_to_something_different()
    {
        var rows = KnowledgeBaseRow.From("5etools", Fixture5eTools.Build().Index);

        Assert.Equal(rows.Count, rows.Select(row => row.ContentHash).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>A hash is 64 lower-case hex characters, because the column is compared as text.</summary>
    [Fact]
    public void A_hash_is_lower_case_hex()
    {
        var hash = KnowledgeBaseRow.HashOf(Item());

        Assert.Equal(64, hash.Length);
        Assert.All(hash, character => Assert.Contains(character, "0123456789abcdef"));
    }

    /// <summary>
    /// Every field the serialiser writes moves the hash. These are the changes an ingest has to
    /// notice; a row whose page moved and whose hash did not would report as unchanged for good and
    /// never be corrected.
    /// </summary>
    [Fact]
    public void Every_field_of_a_row_changes_the_hash()
    {
        var baseline = KnowledgeBaseRow.HashOf(Item());

        (string What, KnowledgeBaseItem Changed)[] changes =
        [
            ("id", Item() with { Id = "monster_beholder_mpmm" }),
            ("name", Item() with { Name = "Beholder Zombie" }),
            ("category", Item() with { Category = "Item" }),
            ("source", Item() with { Source = "MPMM" }),
            ("page", Item() with { Page = 29 }),
            ("a missing page", Item() with { Page = null }),
            ("url", Item() with { Url = "https://5e.tools/bestiary.html#beholder_mpmm" }),
            ("label", Item() with { Label = "CR 14 · Large Aberration" }),
            ("a missing label", Item() with { Label = null }),
            ("armour class", Item() with { Stats = new KnowledgeBaseItemStats(19, "18d10+36", 2) }),
            ("hit points", Item() with { Stats = new KnowledgeBaseItemStats(18, "18d10+37", 2) }),
            ("initiative", Item() with { Stats = new KnowledgeBaseItemStats(18, "18d10+36", 3) }),
            ("no stats at all", Item() with { Stats = null }),
        ];

        Assert.All(changes, change => Assert.True(
            KnowledgeBaseRow.HashOf(change.Changed) != baseline,
            $"a changed {change.What} has to change the content hash"));
    }

    private static KnowledgeBaseItem Item() => new()
    {
        Id = "monster_beholder_mm",
        Name = "Beholder",
        Category = "Monster",
        Source = "MM",
        Page = 28,
        Url = "https://5e.tools/bestiary.html#beholder_mm",
        Label = "CR 13 · Large Aberration",
        Stats = new KnowledgeBaseItemStats(18, "18d10+36", 2),
    };
}
