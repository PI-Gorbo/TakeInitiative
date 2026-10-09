using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;

namespace TakeInitiative.Api.Tests.Unit;

/// <summary>
/// Turns and reorder, pure (18b.2–18b.4): the next turn and the wrap with hidden and waiting
/// combatants, removing the turn's combatant, and <see cref="CombatOrder.Place"/> with and
/// without room between the neighbours' tiebreaks.
/// </summary>
public class CombatTurnTests
{
    private static readonly Actor Dm = Actor.Member(Guid.NewGuid());

    private static Combatant C(string name, int? initiative, int tiebreak = 1000, bool hidden = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Initiative = initiative,
        Tiebreak = tiebreak,
        Hidden = hidden,
    };

    private static Combat Active(params Combatant[] combatants) => new()
    {
        Id = Guid.NewGuid(),
        Status = CombatStatus.Active,
        Round = 1,
        Combatants = combatants,
        TurnCombatantId = CombatOrder.Ordered(combatants).FirstOrDefault()?.Id,
    };

    private static string[] Names(IEnumerable<Combatant> combatants) => combatants.Select(c => c.Name).ToArray();

    private static string Turn(Combat combat) => combat.Find(combat.TurnCombatantId!.Value)!.Name;

    // Next turn.

    [Fact]
    public void NextTurn_SkipsTheWaiting_IncludesTheHidden_AndWrapsIntoANewRound()
    {
        var combat = Active(C("a", 20), C("waiting", null), C("hidden", 15, hidden: true), C("c", 10));
        var order = CombatOrder.Ordered(combat);
        Names(order).Should().Equal("a", "hidden", "c");

        CombatOrder.NextTurn(order, order[0].Id).Should().Be(((Guid?)order[1].Id, false), "a hidden combatant takes its turn");
        CombatOrder.NextTurn(order, order[1].Id).Should().Be(((Guid?)order[2].Id, false));
        CombatOrder.NextTurn(order, order[2].Id).Should().Be(((Guid?)order[0].Id, true));
    }

    [Fact]
    public void NextTurn_OfALoneCombatant_IsItsOwnNextRound()
    {
        var lone = C("lone", 12);
        CombatOrder.NextTurn([lone], lone.Id).Should().Be(((Guid?)lone.Id, true));
        CombatOrder.NextTurn([lone], Guid.NewGuid()).Should().Be(((Guid?)null, false));
    }

    [Fact]
    public void ApplyingTurnEnded_MovesTheTurnAndSetsTheRound()
    {
        var combat = Active(C("a", 20), C("b", 10));
        var b = combat.Combatants.Single(c => c.Name == "b");

        var after = combat.Apply(new TurnEnded(Dm, combat.TurnCombatantId!.Value, b.Id, 1));
        (Turn(after), after.Round).Should().Be(("b", 1));

        var wrapped = after.Apply(new TurnEnded(Dm, b.Id, combat.TurnCombatantId, 2));
        (Turn(wrapped), wrapped.Round).Should().Be(("a", 2));
    }

    // Removing the turn's combatant.

    [Fact]
    public void RemovingTheTurnsCombatant_PassesTheTurnOn_WrappingIntoANewRound()
    {
        var combat = Active(C("a", 20), C("b", 10), C("c", 5));
        var byName = combat.Combatants.ToDictionary(c => c.Name);

        var middle = combat.Apply(new CombatantRemoved(Dm, byName["a"].Id));
        (Turn(middle), middle.Round).Should().Be(("b", 1));

        var onLast = combat with { TurnCombatantId = byName["c"].Id };
        var wrapped = onLast.Apply(new CombatantRemoved(Dm, byName["c"].Id));
        (Turn(wrapped), wrapped.Round).Should().Be(("a", 2));
    }

    [Fact]
    public void RemovingTheLastCombatant_LeavesNoTurn()
    {
        var combat = Active(C("only", 10));
        var after = combat.Apply(new CombatantRemoved(Dm, combat.Combatants[0].Id));
        after.TurnCombatantId.Should().BeNull();
        after.Combatants.Should().BeEmpty();
    }

    // Reorder.

    /// <summary>The order after applying <paramref name="placements"/>, the way the projection does.</summary>
    private static string[] Reordered(IReadOnlyList<Combatant> combatants, IReadOnlyList<CombatOrder.Placement> placements)
    {
        var byId = placements.ToDictionary(p => p.CombatantId);
        return Names(CombatOrder.Ordered(combatants.Select(c => byId.TryGetValue(c.Id, out var p)
            ? c with { Initiative = p.Initiative, Tiebreak = p.Tiebreak }
            : c)));
    }

    [Fact]
    public void Place_ToTheTop_TakesTheInitiativeOfTheOneBelow()
    {
        var combatants = new[] { C("a", 20), C("b", 15), C("c", 10) };
        var order = CombatOrder.Ordered(combatants);

        var placements = CombatOrder.Place(order, combatants[2].Id, null);

        placements.Should().ContainSingle().Which.Initiative.Should().Be(20);
        Reordered(combatants, placements).Should().Equal("c", "a", "b");
    }

    [Fact]
    public void Place_AfterACombatant_TakesItsInitiative_AndSortsBetweenItsNeighbours()
    {
        var combatants = new[] { C("a", 20), C("b", 15), C("c", 10), C("d", 5) };
        var order = CombatOrder.Ordered(combatants);

        var toMiddle = CombatOrder.Place(order, combatants[0].Id, combatants[1].Id);
        toMiddle.Should().ContainSingle().Which.Initiative.Should().Be(15);
        Reordered(combatants, toMiddle).Should().Equal("b", "a", "c", "d");

        var toBottom = CombatOrder.Place(order, combatants[0].Id, combatants[3].Id);
        toBottom.Should().ContainSingle().Which.Initiative.Should().Be(5);
        Reordered(combatants, toBottom).Should().Equal("b", "c", "d", "a");
    }

    [Fact]
    public void Place_BetweenTies_WithRoom_PicksATiebreakBetweenThem()
    {
        var combatants = new[] { C("a", 12, tiebreak: 900), C("b", 12, tiebreak: 100), C("c", 20) };
        var order = CombatOrder.Ordered(combatants);

        var placements = CombatOrder.Place(order, combatants[2].Id, combatants[0].Id);

        placements.Should().ContainSingle().Which.Should().Be(new CombatOrder.Placement(combatants[2].Id, 12, 500));
        Reordered(combatants, placements).Should().Equal("a", "c", "b");
    }

    [Fact]
    public void Place_BetweenTies_WithNoRoom_RespacesThatInitiativesTies()
    {
        var combatants = new[] { C("a", 12, tiebreak: 6), C("b", 12, tiebreak: 5), C("c", 12, tiebreak: 0), C("d", 20) };
        var order = CombatOrder.Ordered(combatants);

        var between = CombatOrder.Place(order, combatants[3].Id, combatants[0].Id);
        between[0].CombatantId.Should().Be(combatants[3].Id, "the moved one comes first");
        between.Should().OnlyContain(p => p.Initiative == 12);
        between.Select(p => p.Tiebreak).Should().OnlyHaveUniqueItems();
        Reordered(combatants, between).Should().Equal("a", "d", "b", "c");

        var bottom = CombatOrder.Place(order, combatants[3].Id, combatants[2].Id);
        Reordered(combatants, bottom).Should().Equal("a", "b", "c", "d");
        bottom.Should().OnlyContain(p => p.Tiebreak >= 0 && p.Tiebreak < CombatOrder.TiebreakRange);
    }

    [Fact]
    public void Place_AtTheTopOfTheRange_Respaces()
    {
        var top = C("top", 20, tiebreak: CombatOrder.TiebreakRange - 1);
        var combatants = new[] { top, C("low", 3) };

        var placements = CombatOrder.Place(CombatOrder.Ordered(combatants), combatants[1].Id, null);

        Reordered(combatants, placements).Should().Equal("low", "top");
    }

    [Fact]
    public void Place_WhereItAlreadyIs_ChangesNothing()
    {
        var combatants = new[] { C("a", 20), C("b", 15) };
        var order = CombatOrder.Ordered(combatants);

        CombatOrder.Place(order, combatants[0].Id, null).Should().BeEmpty();
        CombatOrder.Place(order, combatants[1].Id, combatants[0].Id).Should().BeEmpty();
        CombatOrder.Place([combatants[0]], combatants[0].Id, null).Should().BeEmpty();
    }
}
