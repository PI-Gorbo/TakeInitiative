using FluentAssertions;
using TakeInitiative.Api.Features.Combats;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// The initiative order (18a.2): initiative, then Tiebreak, then Id, so the sort is total; the
/// waiting combatants are not in it; and adding a combatant never moves the placed ones.
/// </summary>
public class CombatOrderTests
{
    private static Combatant C(string name, int? initiative, int tiebreak = 0, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        Initiative = initiative,
        Tiebreak = tiebreak,
    };

    [Fact]
    public void Ordered_IsInitiativeDescending_ThenTiebreakDescending_ThenId()
    {
        var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var highId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var combatants = new[]
        {
            C("slow", 3),
            C("tie-low", 15, tiebreak: 10),
            C("tie-high", 15, tiebreak: 99),
            C("same-b", 12, tiebreak: 5, id: highId),
            C("same-a", 12, tiebreak: 5, id: lowId),
            C("fast", 20),
        };

        CombatOrder.Ordered(combatants).Select(c => c.Name).Should()
            .Equal("fast", "tie-high", "tie-low", "same-a", "same-b", "slow");
        CombatOrder.Ordered(combatants.Reverse()).Select(c => c.Name).Should()
            .Equal("fast", "tie-high", "tie-low", "same-a", "same-b", "slow");
    }

    [Fact]
    public void Waiting_IsNotInTheOrder_AndKeepsTheOrderTheyWereAdded()
    {
        var combatants = new[] { C("w1", null), C("placed", 10), C("w2", null), C("w3", null) };

        CombatOrder.Ordered(combatants).Select(c => c.Name).Should().Equal("placed");
        CombatOrder.Waiting(combatants).Select(c => c.Name).Should().Equal("w1", "w2", "w3");
    }

    [Fact]
    public void AddingACombatant_NeverMovesThePlacedOnes()
    {
        var random = new Random(7);
        var placed = Enumerable.Range(0, 20)
            .Select(i => C($"c{i}", i % 4, CombatOrder.NewTiebreak(random)))
            .ToList();
        var before = CombatOrder.Ordered(placed).Select(c => c.Id).ToList();

        var joiner = C("late", 2, CombatOrder.NewTiebreak(random));
        var after = CombatOrder.Ordered([.. placed, joiner]).Select(c => c.Id).Where(id => id != joiner.Id).ToList();

        after.Should().Equal(before);
    }

    [Fact]
    public void After_MovesDown_AndWrapsToTheTop()
    {
        var order = CombatOrder.Ordered([C("a", 3), C("b", 2), C("c", 1)]);

        CombatOrder.After(order, order[0].Id).Should().Be(((Guid?)order[1].Id, false));
        CombatOrder.After(order, order[2].Id).Should().Be(((Guid?)order[0].Id, true));
        CombatOrder.After([order[0]], order[0].Id).Should().Be(((Guid?)null, false), "nobody else to pass to");
    }
}
