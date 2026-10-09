using FluentAssertions;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Reference;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>Stats from a stat block (20a.6).</summary>
public class ReferenceStatsTests
{
    private static readonly SrdCatalog Catalog = new();

    private static StatBlock Block(int initiative, string hitDice, int ac)
        => Catalog.Get("goblin-warrior")!.StatBlock with { InitiativeBonus = initiative, HitDice = hitDice, Ac = ac };

    [Theory]
    [InlineData(2, "1d20+2")]
    [InlineData(-1, "1d20-1")]
    [InlineData(0, "1d20")]
    public void Initiative_IsD20PlusTheBonus(int bonus, string expected)
        => ReferenceStats.From(Block(bonus, "3d6", 15)).InitiativeRoll.Should().Be(expected);

    [Fact]
    public void MaxHp_IsTheHitDice_EvenWithAMinus()
        => ReferenceStats.From(Block(0, "4d8-4", 12)).Should().Be(new Stats { InitiativeRoll = "1d20", MaxHp = "4d8-4", Ac = 12 });

    [Fact]
    public void GoblinWarrior()
        => ReferenceStats.From(Catalog.Get("goblin-warrior")!.StatBlock)
            .Should().Be(new Stats { InitiativeRoll = "1d20+2", MaxHp = "3d6", Ac = 15 });
}
