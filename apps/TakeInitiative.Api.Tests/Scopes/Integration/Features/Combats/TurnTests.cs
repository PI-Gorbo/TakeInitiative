using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>
/// Turns, reorder and concurrent writes (18b.2–18b.4, 18b.7). <see cref="Users.DM"/> owns each
/// campaign, <see cref="Users.Player"/> has claimed Brynn, <see cref="Users.Outsider"/> is a second
/// player.
/// </summary>
public class TurnTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public TurnTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    /// <summary>A started combat of plain-name combatants with the given constant initiatives.</summary>
    private async Task<(TestCampaign Campaign, CombatResponse Combat)> Running(string name, params (string Name, int Initiative)[] combatants)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        var combat = await fixture.CreateCombat(campaign.Id);
        if (combatants.Length > 0)
        {
            await fixture.AddAsDm(campaign.Id, combat.Id,
                combatants.Select(c => (object)new { name = c.Name, initiativeRoll = $"{c.Initiative}" }).ToArray());
        }
        fixture.LoginAsUser(Users.DM);
        return (campaign, (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat);
    }

    private static string? TurnName(CombatResponse combat)
        => combat.Combatants.SingleOrDefault(c => c.Id == combat.TurnCombatantId)?.Name;

    [Fact]
    public async Task TheLifecycle_FromDraftToTheSecondRound()
    {
        // v1's FullCombatTest, rewritten: Draft, add, roll, a player joins and waits, the turn
        // owner ends their turn, the next combatant has it, and the wrap counts the round.
        var campaign = await TestCampaign.Create(fixture, "Turn lifecycle");
        var brynn = await fixture.PlayerCharacter(campaign);
        var combat = await fixture.CreateCombat(campaign.Id);
        combat.Status.Should().Be(CombatStatus.Draft);
        await fixture.AddAsDm(campaign.Id, combat.Id,
            new { name = "Ogre", initiativeRoll = "40" },
            new { name = "Rat", initiativeRoll = "1" });
        fixture.LoginAsUser(Users.DM);
        var started = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;
        (started.Status, started.Round, TurnName(started)).Should().Be((CombatStatus.Active, 1, "Ogre"));

        fixture.LoginAsUser(Users.Player);
        var joined = (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id }).Ok()).Combat;
        joined.Named("Brynn").Waiting.Should().BeTrue();
        TurnName(joined).Should().Be("Ogre", "a waiting combatant takes no turn");
        var rolled = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;
        rolled.Names().Should().Equal("Ogre", "Brynn", "Rat");

        fixture.LoginAsUser(Users.DM);
        var brynnsTurn = await fixture.EndCurrentTurn(campaign.Id, rolled);
        (TurnName(brynnsTurn), brynnsTurn.Round).Should().Be(("Brynn", 1));

        fixture.LoginAsUser(Users.Outsider);
        (await fixture.EndTurn(campaign.Id, combat.Id, brynnsTurn.TurnCombatantId!.Value, 1)).Status
            .Should().Be(403, "a player cannot end another's turn");

        fixture.LoginAsUser(Users.Player);
        var ratsTurn = await fixture.EndCurrentTurn(campaign.Id, brynnsTurn);
        (TurnName(ratsTurn), ratsTurn.Round).Should().Be(("Rat", 1));

        fixture.LoginAsUser(Users.DM);
        var wrapped = await fixture.EndCurrentTurn(campaign.Id, ratsTurn);
        (TurnName(wrapped), wrapped.Round).Should().Be(("Ogre", 2), "past the last one the turn wraps and the round goes up");

        var ended = (await TestCampaign.EventsOf(fixture, combat.Id)).Select(e => e.Data).OfType<TurnEnded>().ToList();
        ended.Select(e => e.Actor.MemberId).Should().Equal(campaign.DmMemberId, campaign.PlayerMemberId, campaign.DmMemberId);
        ended.Select(e => e.Round).Should().Equal(1, 1, 2);
    }

    [Fact]
    public async Task AStaleEndTurn_IsA409_SoADoubleTapEndsOneTurn()
    {
        var (campaign, combat) = await Running("Turn stale", ("A", 30), ("B", 20), ("C", 10));

        fixture.LoginAsUser(Users.DM);
        var first = await fixture.EndTurn(campaign.Id, combat.Id, combat.TurnCombatantId!.Value, 1);
        var again = await fixture.EndTurn(campaign.Id, combat.Id, combat.TurnCombatantId!.Value, 1);

        first.Status.Should().Be(200);
        again.Status.Should().Be(409);
        again.Body.Should().Contain(PostCombatEndTurn.MovedOnMessage);
        TurnName((await fixture.GetCombat(campaign.Id, combat.Id)).Combat).Should().Be("B", "one turn ended");

        // The right combatant in the wrong round is stale too.
        var b = combat.Named("B").Id;
        (await fixture.EndTurn(campaign.Id, combat.Id, b, 2)).Status.Should().Be(409);
        (await fixture.EndTurn(campaign.Id, combat.Id, Guid.NewGuid(), 1)).Status.Should().Be(409);
    }

    [Fact]
    public async Task TwoDmsEndingTheSameTurnAtOnce_EndOneTurn()
    {
        var (campaign, combat) = await Running("Turn race", ("A", 30), ("B", 20), ("C", 10));

        fixture.LoginAsUser(Users.DM);
        var replies = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => fixture.EndTurn(campaign.Id, combat.Id, combat.TurnCombatantId!.Value, 1)));

        replies.Count(r => r.Status == 200).Should().Be(1, string.Join("\n", replies.Select(r => r.Body)));
        replies.Where(r => r.Status != 200).Should().OnlyContain(r => r.Status == 409);
        TurnName((await fixture.GetCombat(campaign.Id, combat.Id)).Combat).Should().Be("B");
        (await TestCampaign.EventsOf(fixture, combat.Id)).Select(e => e.Data).OfType<TurnEnded>().Should().ContainSingle();
    }

    [Fact]
    public async Task ADm_EndsAnyTurn_IncludingAHiddenCombatants()
    {
        var (campaign, combat) = await Running("Turn DM any", ("A", 30), ("B", 20));
        var hidden = combat.Named("B");
        fixture.LoginAsUser(Users.DM);
        await fixture.PutCombatant(campaign.Id, combat.Id, hidden.Id, Body(hidden, b => b["hidden"] = true)).Ok();

        var onHidden = await fixture.EndCurrentTurn(campaign.Id, combat);
        TurnName(onHidden).Should().Be("B", "hidden combatants take turns like any other");

        // A player cannot end it, and cannot tell it from a stale turn.
        fixture.LoginAsUser(Users.Player);
        var player = await fixture.EndTurn(campaign.Id, combat.Id, hidden.Id, 1);
        player.Status.Should().Be(409);

        fixture.LoginAsUser(Users.DM);
        var back = await fixture.EndCurrentTurn(campaign.Id, onHidden);
        (TurnName(back), back.Round).Should().Be(("A", 2));
    }

    [Fact]
    public async Task APlayer_CannotEndAMonstersTurn()
    {
        var (campaign, combat) = await Running("Turn player monster", ("Goblin", 10));

        fixture.LoginAsUser(Users.Player);
        (await fixture.EndTurn(campaign.Id, combat.Id, combat.TurnCombatantId!.Value, 1)).Status.Should().Be(403);
    }

    [Fact]
    public async Task ALoneCombatant_TakesEveryRound()
    {
        var (campaign, combat) = await Running("Turn lone", ("Solo", 10));

        fixture.LoginAsUser(Users.DM);
        var next = await fixture.EndCurrentTurn(campaign.Id, combat);
        (TurnName(next), next.Round).Should().Be(("Solo", 2));
    }

    [Fact]
    public async Task RemovingTheTurnsCombatant_PassesTheTurnOn()
    {
        var (campaign, combat) = await Running("Turn remove", ("A", 30), ("B", 20), ("C", 10));

        fixture.LoginAsUser(Users.DM);
        var afterA = (await fixture.DeleteCombatant(campaign.Id, combat.Id, combat.Named("A").Id).Ok()).Combat;
        (TurnName(afterA), afterA.Round).Should().Be(("B", 1));

        var onC = await fixture.EndCurrentTurn(campaign.Id, afterA);
        var afterC = (await fixture.DeleteCombatant(campaign.Id, combat.Id, combat.Named("C").Id).Ok()).Combat;
        (TurnName(afterC), afterC.Round).Should().Be(("B", 2), "removing the last one wraps and counts the round");
        onC.TurnCombatantId.Should().Be(combat.Named("C").Id);

        var empty = (await fixture.DeleteCombatant(campaign.Id, combat.Id, combat.Named("B").Id).Ok()).Combat;
        empty.TurnCombatantId.Should().BeNull();
    }

    [Fact]
    public async Task TheEmptyCombat_StartsAndFinishes_AndHasNoTurnToEnd()
    {
        var (campaign, combat) = await Running("Turn empty");
        combat.TurnCombatantId.Should().BeNull();

        fixture.LoginAsUser(Users.DM);
        (await fixture.EndTurn(campaign.Id, combat.Id, Guid.NewGuid(), 1)).Status.Should().Be(409);
        (await fixture.Finish(campaign.Id, combat.Id).Ok()).Combat.Status.Should().Be(CombatStatus.Finished);
    }

    [Fact]
    public async Task EndingATurnInADraft_IsA409()
    {
        var campaign = await TestCampaign.Create(fixture, "Turn draft", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);
        var added = await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "A" });

        fixture.LoginAsUser(Users.DM);
        (await fixture.EndTurn(campaign.Id, combat.Id, added.Named("A").Id, 1)).Status.Should().Be(409);
    }

    // Reorder.

    [Fact]
    public async Task ADm_DragsCombatants_AndTheTurnStays()
    {
        var (campaign, combat) = await Running("Turn reorder", ("A", 30), ("B", 20), ("C", 10));
        var (a, b, c) = (combat.Named("A").Id, combat.Named("B").Id, combat.Named("C").Id);
        fixture.LoginAsUser(Users.DM);

        var cOnTop = (await fixture.Position(campaign.Id, combat.Id, c, null).Ok()).Combat;
        cOnTop.Names().Should().Equal("C", "A", "B");
        cOnTop.Named("C").Initiative.Should().Be(30, "it takes the initiative of the neighbour it lands next to");
        TurnName(cOnTop).Should().Be("A", "the turn stays with whoever has it");

        var aLast = (await fixture.Position(campaign.Id, combat.Id, a, b).Ok()).Combat;
        aLast.Names().Should().Equal("C", "B", "A");
        aLast.Named("A").Initiative.Should().Be(20);

        // Where it already is, or after itself: nothing appended.
        var before = (await TestCampaign.EventsOf(fixture, combat.Id)).Count;
        await fixture.Position(campaign.Id, combat.Id, a, b).Ok();
        await fixture.Position(campaign.Id, combat.Id, a, a).Ok();
        (await TestCampaign.EventsOf(fixture, combat.Id)).Should().HaveCount(before);

        var edits = (await TestCampaign.EventsOf(fixture, combat.Id)).Select(e => e.Data).OfType<CombatantEdited>().ToList();
        edits.Should().HaveCount(2).And.OnlyContain(e => e.Tiebreak != null && e.Actor.MemberId == campaign.DmMemberId);
    }

    [Fact]
    public async Task Reorder_IsForDms_AndOnlyForPlacedCombatants()
    {
        var (campaign, combat) = await Running("Turn reorder rules", ("A", 30), ("B", 20));
        var waiting = (await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "W" })).Named("W").Id;
        var (a, b) = (combat.Named("A").Id, combat.Named("B").Id);

        fixture.LoginAsUser(Users.Player);
        (await fixture.Position(campaign.Id, combat.Id, b, null)).Status.Should().Be(403);

        fixture.LoginAsUser(Users.DM);
        (await fixture.Position(campaign.Id, combat.Id, waiting, null)).Status.Should().Be(400);
        (await fixture.Position(campaign.Id, combat.Id, a, waiting)).Status.Should().Be(400);
        (await fixture.Position(campaign.Id, combat.Id, Guid.NewGuid(), null)).Status.Should().Be(404);
        (await fixture.Position(campaign.Id, combat.Id, a, Guid.NewGuid())).Status.Should().Be(404);
    }

    // Concurrency (18b.7).

    [Fact]
    public async Task EditsAtTheSameTime_AreRetried_AndAllLand()
    {
        var (campaign, combat) = await Running("Turn concurrent edits", ("A", 30), ("B", 20), ("C", 10));

        fixture.LoginAsUser(Users.DM);
        var replies = await Task.WhenAll(combat.Combatants.Select(c =>
            fixture.PutCombatant(campaign.Id, combat.Id, c.Id, Body(c, body => body["conditions"] = new[] { new { label = $"Mark {c.Name}" } }))));

        replies.Should().OnlyContain(r => r.Status == 200, string.Join("\n", replies.Select(r => r.Body)));
        var after = (await fixture.GetCombat(campaign.Id, combat.Id)).Combat;
        after.Combatants.Should().OnlyContain(c => c.Conditions.Single().Label == $"Mark {c.Name}");
    }
}
