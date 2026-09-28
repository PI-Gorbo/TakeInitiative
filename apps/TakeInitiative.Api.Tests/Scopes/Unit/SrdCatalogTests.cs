using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Utilities;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// The embedded SRD 5.2 data (20a.3, 20a.7): it loads, every monster is whole, and every monster's
/// Stats pass the real dice checker, so a bad transcription fails here and not in a combat.
/// </summary>
public class SrdCatalogTests
{
    private static readonly SrdCatalog Catalog = new();
    private readonly DiceRoller roller = new(new Random(1234));

    [Fact]
    public void TheResource_Loads331Monsters_WithUniqueIdsAndNames()
    {
        Catalog.Monsters.Should().HaveCount(331);
        Catalog.Monsters.Select(m => m.Id).Should().OnlyHaveUniqueItems();
        Catalog.Monsters.Select(m => m.Name.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
        Catalog.Monsters.Should().OnlyContain(m => m.Id.Length > 0 && m.Name.Length > 0);
    }

    [Fact]
    public void EveryMonster_HasAcHpHitDiceAndActions()
    {
        foreach (var block in Catalog.Monsters.Select(m => m.StatBlock))
        {
            block.Ac.Should().BeInRange(1, Stats.AcMax, block.Id);
            block.Hp.Should().BePositive(block.Id);
            block.HitDice.Should().MatchRegex(@"^\d+d\d+([+-]\d+)?$", block.Id);
            block.Cr.Should().MatchRegex(@"^(0|1/8|1/4|1/2|[1-9]|[12]\d|30)$", block.Id);
            block.Pb.Should().BeInRange(2, 9, block.Id);
            block.Senses.Should().Contain("Passive Perception", block.Id);
        }
    }

    [Fact]
    public void EveryMonstersStats_PassTheRealDiceCheck()
    {
        foreach (var monster in Catalog.Monsters)
        {
            var stats = monster.Summary.Stats;
            stats.Should().NotBeNull(monster.Id);
            stats!.Ac.Should().Be(monster.StatBlock.Ac);
            foreach (var expression in new[] { stats.InitiativeRoll!, stats.MaxHp! })
            {
                expression.Length.Should().BeLessThanOrEqualTo(Stats.ExpressionMaxLength, monster.Id);
                var check = roller.Check(expression);
                check.IsSuccess.Should().BeTrue($"{monster.Id}: {expression} {(check.IsFailure ? check.Error : "")}");
            }
        }
    }

    [Fact]
    public void GoblinWarrior_ReadsLikeTheSrd()
    {
        var block = Catalog.Get("goblin-warrior")!.StatBlock;
        block.Name.Should().Be("Goblin Warrior");
        block.Category.Should().Be(StatBlockCategory.Monster);
        (block.Size, block.Type, block.Alignment).Should().Be(("Small", "Fey", "Chaotic Neutral"));
        (block.Ac, block.InitiativeBonus, block.Hp, block.HitDice).Should().Be((15, 2, 10, "3d6"));
        (block.Cr, block.Xp, block.Pb).Should().Be(("1/4", 50, 2));
        block.Skills.Should().Equal(new Dictionary<string, int> { ["stealth"] = 6 });
        block.Actions.Select(a => (a.Kind, a.Name)).Should().Equal(
            (StatBlockActionKind.Action, "Scimitar"),
            (StatBlockActionKind.Action, "Shortbow"),
            (StatBlockActionKind.BonusAction, "Nimble Escape"));
        block.Actions[0].Text.Should().StartWith("Melee Attack Roll: +4, reach 5 ft. Hit: 5 (1d6 + 2) Slashing damage");
    }

    [Fact]
    public void EveryAttackRoll_HasItsHitLabel()
        => Catalog.Monsters
            .SelectMany(m => m.StatBlock.Actions)
            .Where(a => a.Text.Contains("Attack Roll:"))
            .Should().OnlyContain(a => a.Text.Contains(" Hit: "));

    [Fact]
    public void TheSummary_IsACharacterWithADetailLine()
    {
        var summary = Catalog.Get("goblin-warrior")!.Summary;
        summary.Should().Be(new ReferenceSummary(
            "srd52", "goblin-warrior", "Goblin Warrior", ReferenceCategory.Monster, "CR 1/4 · Small Fey",
            null, EntryKind.Character, new Stats { InitiativeRoll = "1d20+2", MaxHp = "3d6", Ac = 15 }));
    }

    [Fact]
    public void SourceJson_HasTheShaAndTheAttribution()
    {
        var source = Catalog.Source;
        source.Document.Should().Be("SRD 5.2");
        source.Sha.Should().MatchRegex("^[0-9a-f]{40}$");
        source.Count.Should().Be(331);
        source.License.Should().Be("CC-BY-4.0");
        source.Attribution.Should().StartWith("This work includes material from the System Reference Document 5.2 (\"SRD 5.2\") by Wizards of the Coast LLC");
        source.Attribution.Should().Contain("https://creativecommons.org/licenses/by/4.0/legalcode");
        Catalog.Attribution.Should().Be(new ReferenceAttribution(
            source.Attribution, "CC-BY-4.0", "https://creativecommons.org/licenses/by/4.0/legalcode", "https://www.dndbeyond.com/srd"));
    }

    [Fact]
    public void TheProvider_SearchesAndGets()
    {
        var provider = new SrdReferenceProvider(Catalog);
        (provider.Key, provider.Label, provider.HasStatBlocks).Should().Be(("srd52", "SRD 5.2", true));

        provider.Search("goblin", 3).Select(m => m.Item.Name).Should().Equal("Goblin Boss", "Goblin Minion", "Goblin Warrior");
        provider.Search("gobln", 5).Select(m => m.Item.Name).Should().Contain("Goblin Warrior");

        var item = provider.Get("owlbear")!;
        item.StatBlock!.Name.Should().Be("Owlbear");
        item.Attribution.Should().Be(Catalog.Attribution);
        provider.Get("tarrasque-jr").Should().BeNull();
        provider.Find("owlbear").Should().Be(item.Summary);
    }

    [Fact]
    public void AddReference_RegistersTheSrdProvider_ThenTheFiveEToolsOne()
    {
        using var services = new ServiceCollection().AddReference().BuildServiceProvider();
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ReferenceCatalog>();
        catalog.Providers.Select(p => p.Key).Should().Equal("srd52", "5etools");
        catalog.Providers[1].Search("goblin", 10).Should().BeEmpty("with no index configured the 5eTools provider is off");
        catalog.GetItem("SRD52", "goblin-warrior")!.Summary.Name.Should().Be("Goblin Warrior");
        catalog.GetItem("5etools", "goblin-warrior").Should().BeNull();
    }
}
