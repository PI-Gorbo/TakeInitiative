using System.Text.Json;

using TakeInitiative.KnowledgeBase.FiveETools;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// Monsters: <c>_copy</c> inheritance, the stat readers, and what gets skipped. Ported from
/// <c>build-5etools-index.test.mjs</c>'s "monsters" block.
/// </summary>
public class MonsterTests
{
    /// <summary>
    /// Test Gremlin Chief is a Test Gremlin with more hit points, a higher CR and a proficient
    /// initiative, plus a <c>_mod</c> that appends an action. Its own fields win, the base's fill the
    /// rest, and the <c>_mod</c> changes nothing the index stores.
    /// </summary>
    [Fact]
    public void A_copy_with_mod_takes_the_bases_fields_under_its_own()
    {
        var chief = Fixture5eTools.Build().Index.Row("monster_test-gremlin-chief_tst");

        Assert.Equal("CR 2 · Small Fey", chief.Label);
        Assert.Equal(new KnowledgeBaseItemStats(15, "5d6+10", 4), chief.Stats);
    }

    /// <summary>
    /// A copy with no overrides at all is its base, and a copy in another file with a
    /// <c>_templates</c> inherits the base plainly while the template is counted and not applied —
    /// so the zombie keeps the gremlin's CR and AC and only its own <c>type</c> changes the label.
    /// </summary>
    [Fact]
    public void A_copy_without_mod_and_one_across_files_with_templates()
    {
        var (index, report) = Fixture5eTools.Build();

        Assert.Equal(
            new KnowledgeBaseItemStats(15, "3d6+3", 2),
            index.Row("monster_test-gremlin-understudy_tst").Stats);

        var zombie = index.Row("monster_test-gremlin-zombie_tsta");
        Assert.Equal("CR 1/2 · Small Undead", zombie.Label);
        Assert.Equal(1, report.Templates);
    }

    [Fact]
    public void Skips_a_copy_with_a_missing_base_a_broken_row_Srd_duplicates_and_Ua()
    {
        var (index, report) = Fixture5eTools.Build();
        var names = index.Items.Select(item => item.Name).ToList();

        Assert.DoesNotContain("Test Orphan Copy", names);
        Assert.DoesNotContain("Test Hollow Sentinel", names);
        Assert.DoesNotContain("Test Srd Imp", names);
        Assert.DoesNotContain("Test Draft Horror", names);

        // A monster inside a fluff- file is not read at all: the file is filtered out by name.
        Assert.DoesNotContain("Test Fluff Phantom", names);

        Assert.Single(report.Skipped.MissingBase);
        Assert.Single(report.Skipped.Broken);
        Assert.Equal(1, report.Skipped.Srd52);
        Assert.Equal(1, report.Skipped.Ua);
        Assert.Equal(new FiveEToolsCounts(8, 3, 3), index.Counts);
    }

    /// <summary>
    /// The three ways a monster loses its stats: a <c>{ special }</c> hit-point block, a
    /// <c>{ special }</c> AC, and a CR the parser does not recognise on a monster whose initiative
    /// needs one. The last still gets a label — it just has no CR in it.
    /// </summary>
    [Fact]
    public void Special_hp_special_ac_and_an_unknown_cr_with_proficiency_give_no_stats()
    {
        var index = Fixture5eTools.Build().Index;

        Assert.Null(index.Row("monster_test-swarm-of-motes_tst").Stats);
        Assert.Null(index.Row("monster_test-mossback_tst").Stats);

        var finch = index.Row("monster_test-clockwork-finch_tst");
        Assert.Null(finch.Stats);
        Assert.Equal("Tiny Construct", finch.Label);
    }

    [Fact]
    public void Every_Ac_shape()
    {
        Assert.Equal(15, Bestiary.AcOf(Fixture5eTools.Json("[15]")));
        Assert.Equal(17, Bestiary.AcOf(Fixture5eTools.Json(
            """[{ "ac": 17, "from": ["x"] }, { "ac": 19, "condition": "y" }]""")));
        Assert.Null(Bestiary.AcOf(Fixture5eTools.Json("""[{ "special": "12 + PB" }]""")));
        Assert.Null(Bestiary.AcOf(null));
        Assert.Null(Bestiary.AcOf(Fixture5eTools.Json("[]")));
    }

    [Fact]
    public void Every_Cr_shape()
    {
        Assert.Equal("1/4", Bestiary.CrOf(Fixture5eTools.Json("\"1/4\"")));
        Assert.Equal("5", Bestiary.CrOf(Fixture5eTools.Json("""{ "cr": "5", "lair": "6", "coven": "7" }""")));
        Assert.Null(Bestiary.CrOf(Fixture5eTools.Json("\"Unknown\"")));
        Assert.Null(Bestiary.CrOf(null));

        // A CR written as a number rather than a string is not a CR: the label shows the string.
        Assert.Null(Bestiary.CrOf(Fixture5eTools.Json("5")));
    }

    [Fact]
    public void Hp_dice_are_normalised_and_checked()
    {
        Assert.Equal("10d10+30", Bestiary.HpDiceOf(Fixture5eTools.Json("""{ "average": 85, "formula": "10d10 + 30" }""")));
        Assert.Equal("1d4-1", Bestiary.HpDiceOf(Fixture5eTools.Json("""{ "formula": "1d4 - 1" }""")));
        Assert.Null(Bestiary.HpDiceOf(Fixture5eTools.Json("""{ "special": "half" }""")));
        Assert.Null(Bestiary.HpDiceOf(Fixture5eTools.Json("""{ "formula": "2d8 + 3 + 1" }""")));
        Assert.Null(Bestiary.HpDiceOf(null));
    }

    [Fact]
    public void Initiative_explicit_from_Dex_and_with_proficiency()
    {
        Assert.Equal(7, Bestiary.InitiativeOf(
            Fixture5eTools.Monster("""{ "dex": 8, "initiative": { "initiative": 7 }, "cr": "5" }""")));
        Assert.Equal(-1, Bestiary.InitiativeOf(Fixture5eTools.Monster("""{ "dex": 9, "cr": "1" }""")));
        Assert.Equal(4, Bestiary.InitiativeOf(
            Fixture5eTools.Monster("""{ "dex": 14, "initiative": { "proficiency": 1 }, "cr": "2" }""")));
        Assert.Equal(14, Bestiary.InitiativeOf(
            Fixture5eTools.Monster("""{ "dex": 14, "initiative": { "proficiency": 2 }, "cr": { "cr": "17" } }""")));

        // Proficiency needs a CR to scale by, and "Unknown" is not one.
        Assert.Null(Bestiary.InitiativeOf(
            Fixture5eTools.Monster("""{ "dex": 14, "initiative": { "proficiency": 1 }, "cr": "Unknown" }""")));
        Assert.Null(Bestiary.InitiativeOf(Fixture5eTools.Monster("""{ "cr": "1" }""")));
    }

    /// <summary>
    /// The proficiency bonus a CR implies: +2 for every fraction and up to CR 4, then one more every
    /// four ratings.
    /// </summary>
    [Theory]
    [InlineData("1/8", 2)]
    [InlineData("1/2", 2)]
    [InlineData("0", 2)]
    [InlineData("4", 2)]
    [InlineData("5", 3)]
    [InlineData("8", 3)]
    [InlineData("9", 4)]
    [InlineData("17", 6)]
    [InlineData("30", 9)]
    public void The_proficiency_bonus_follows_the_challenge_rating(string cr, int bonus) =>
        Assert.Equal(bonus, Bestiary.ProficiencyBonus(cr));

    /// <summary>
    /// Warden's Stone (Awakened) is two sizes and carries a free-text creature-type tag. The label
    /// keeps both size words and drops the tag: only the enum is mapped.
    /// </summary>
    [Fact]
    public void Labels_drop_free_text_type_tags()
    {
        var warden = Fixture5eTools.Build().Index.Row("monster_warden-s-stone-awakened_tst");

        Assert.Equal("CR 5 · Large or Huge Construct", warden.Label);
        Assert.Equal(new KnowledgeBaseItemStats(17, "10d10+30", 7), warden.Stats);
    }

    /// <summary>A creature type written as <c>{ choose: [...] }</c> becomes "one or the other".</summary>
    [Fact]
    public void A_chosen_creature_type_lists_the_choices()
    {
        Assert.Equal(
            "CR 4 · Huge Beast or Plant",
            Fixture5eTools.Build().Index.Row("monster_test-mossback_tst").Label);
    }

    /// <summary>
    /// A cycle of copies resolves to nothing rather than overflowing the stack, and the row is
    /// reported as one whose base is missing.
    /// </summary>
    [Fact]
    public void A_copy_that_copies_itself_resolves_to_nothing()
    {
        var snake = Fixture5eTools.Json("""{ "name": "Ouroboros", "source": "TST", "_copy": { "name": "Ouroboros", "source": "TST" } }""");
        var byKey = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [Copy.KeyOf("Ouroboros", "TST")] = snake,
        };

        Assert.Null(Copy.Resolve(snake, byKey));
    }

    /// <summary>
    /// A chain of copies inherits through the middle row: the grandchild gets the grandparent's AC
    /// because the parent is resolved first.
    /// </summary>
    [Fact]
    public void A_copy_of_a_copy_inherits_through_the_chain()
    {
        var grandparent = Fixture5eTools.Json("""{ "name": "A", "source": "TST", "ac": [18], "size": ["M"], "type": "beast", "cr": "3" }""");
        var parent = Fixture5eTools.Json("""{ "name": "B", "source": "TST", "_copy": { "name": "A", "source": "TST" }, "cr": "4" }""");
        var child = Fixture5eTools.Json("""{ "name": "C", "source": "TST", "_copy": { "name": "B", "source": "TST" }, "size": ["L"] }""");
        var byKey = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [Copy.KeyOf("A", "TST")] = grandparent,
            [Copy.KeyOf("B", "TST")] = parent,
            [Copy.KeyOf("C", "TST")] = child,
        };

        var resolved = Copy.Resolve(child, byKey);

        Assert.NotNull(resolved);
        Assert.Equal(18, Bestiary.AcOf(resolved["ac"]));
        Assert.Equal("CR 4 · Large Beast", Bestiary.MonsterLabel(resolved, "test"));
    }
}
