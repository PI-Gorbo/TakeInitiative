using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>
/// Rolling initiative (18b.1): the first roll starts the combat and moves it to the current
/// session, a player rolls only their own, late joiners slot in without re-rolling anyone, ties
/// are never ambiguous, and a typed initiative is kept. Plain-name combatants roll constant
/// expressions ("20") so the order is known.
/// </summary>
public class InitiativeTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public InitiativeTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    private async Task<Combat> Stored(Guid combatId)
    {
        await using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return (await session.LoadAsync<Combat>(combatId))!;
    }

    [Fact]
    public async Task TheFirstRoll_StartsTheCombat_InTheCurrentSession_WithTheTurnOnTop()
    {
        var campaign = await TestCampaign.Create(fixture, "Initiative start", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);
        var sessionOne = combat.SessionId;
        await fixture.AddAsDm(campaign.Id, combat.Id,
            new { name = "Slow", initiativeRoll = "3" },
            new { name = "Fast", initiativeRoll = "19" });

        // The Draft was prepared in Session 1 and is run in Session 2.
        fixture.LoginAsUser(Users.DM);
        var sessionTwo = await fixture.PostStartSession(campaign.Id, 2);
        sessionTwo.Should().Succeed();

        var started = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;

        (started.Status, started.Round, started.SessionId).Should().Be((CombatStatus.Active, 1, sessionTwo.Value.Id));
        started.SessionId.Should().NotBe(sessionOne);
        started.StartedAt.Should().NotBeNull();
        started.Names().Should().Equal("Fast", "Slow");
        (started.Named("Fast").Initiative, started.Named("Slow").Initiative).Should().Be((19, 3));
        started.TurnCombatantId.Should().Be(started.Named("Fast").Id);
        (await fixture.GetCombats(campaign.Id)).As<GetCombatsResponse>().Combats.Single().SessionNumber.Should().Be(2);

        var roll = (await TestCampaign.EventsOf(fixture, combat.Id)).Select(e => e.Data).OfType<InitiativeRolled>().Single();
        (roll.Actor.MemberId, roll.SessionId, roll.Rolls.Length).Should().Be((campaign.DmMemberId, sessionTwo.Value.Id, 2));

        fixture.LoginAsUser(Users.Player);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Status.Should().Be(200, "players see it once it has started");
    }

    [Fact]
    public async Task StartingAnEmptyCombat_Succeeds_WithNoTurn()
    {
        var campaign = await TestCampaign.Create(fixture, "Initiative empty", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);

        fixture.LoginAsUser(Users.DM);
        var started = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;

        (started.Status, started.Round, started.TurnCombatantId).Should().Be((CombatStatus.Active, 1, (Guid?)null));

        // The first combatant to be placed takes the turn.
        var added = await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Wolf", initiativeRoll = "7" });
        added.TurnCombatantId.Should().BeNull("a waiting combatant has no turn");
        var rolled = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;
        rolled.TurnCombatantId.Should().Be(rolled.Named("Wolf").Id);
    }

    [Fact]
    public async Task APlayer_RollsOnlyTheirOwn_AndOnlyOnceTheCombatHasStarted()
    {
        var campaign = await TestCampaign.Create(fixture, "Initiative player", withSecondPlayer: false);
        var brynn = await fixture.PlayerCharacter(campaign);
        var combat = await fixture.CreateCombat(campaign.Id);

        fixture.LoginAsUser(Users.Player);
        (await fixture.Roll(campaign.Id, combat.Id)).Status.Should().Be(404, "a player cannot see a Draft");

        await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Wolf", initiativeRoll = "12" });
        fixture.LoginAsUser(Users.DM);
        await fixture.Roll(campaign.Id, combat.Id).Ok();
        await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Late", initiativeRoll = "5" });

        fixture.LoginAsUser(Users.Player);
        await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id }).Ok();
        var rolled = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;

        rolled.Named("Brynn").Waiting.Should().BeFalse();
        rolled.Named("Brynn").Initiative.Should().BeInRange(4, 23, "1d20+3");
        rolled.Named("Late").Waiting.Should().BeTrue("a player rolls only their own");
        var roll = (await TestCampaign.EventsOf(fixture, combat.Id)).Select(e => e.Data).OfType<InitiativeRolled>().Last();
        (roll.Actor.MemberId, roll.SessionId).Should().Be((campaign.PlayerMemberId, (Guid?)null));
        roll.Rolls.Should().ContainSingle().Which.CombatantId.Should().Be(rolled.Named("Brynn").Id);

        // Nothing of theirs waits: a 200 that appends nothing.
        var before = (await TestCampaign.EventsOf(fixture, combat.Id)).Count;
        fixture.LoginAsUser(Users.Player);
        await fixture.Roll(campaign.Id, combat.Id).Ok();
        (await TestCampaign.EventsOf(fixture, combat.Id)).Should().HaveCount(before);
    }

    [Fact]
    public async Task LateJoiners_SlotIn_WithoutReRollingAnyone_AndTheTurnStays()
    {
        var campaign = await TestCampaign.Create(fixture, "Initiative late", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);
        await fixture.AddAsDm(campaign.Id, combat.Id,
            new { name = "Top", initiativeRoll = "80" },
            new { name = "Bottom", initiativeRoll = "1" });
        fixture.LoginAsUser(Users.DM);
        var started = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;
        var onBottom = await fixture.EndCurrentTurn(campaign.Id, started);
        onBottom.TurnCombatantId.Should().Be(started.Named("Bottom").Id);

        await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Middle", initiativeRoll = "40" });
        fixture.LoginAsUser(Users.DM);
        var after = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;

        after.Names().Should().Equal("Top", "Middle", "Bottom");
        after.Named("Top").Initiative.Should().Be(started.Named("Top").Initiative, "nobody is re-rolled");
        after.Named("Bottom").Initiative.Should().Be(started.Named("Bottom").Initiative);
        (after.TurnCombatantId, after.Round).Should().Be((started.Named("Bottom").Id, 1), "the turn stays where it was");
        (await TestCampaign.EventsOf(fixture, combat.Id)).Select(e => e.Data).OfType<InitiativeRolled>().Last()
            .Rolls.Select(r => r.CombatantId).Should().Equal(after.Named("Middle").Id);

        // Nothing waiting now: a 200 that appends nothing.
        var before = (await TestCampaign.EventsOf(fixture, combat.Id)).Count;
        fixture.LoginAsUser(Users.DM);
        await fixture.Roll(campaign.Id, combat.Id).Ok();
        (await TestCampaign.EventsOf(fixture, combat.Id)).Should().HaveCount(before);
    }

    [Fact]
    public async Task Ties_NeverProduceAnAmbiguousOrder()
    {
        var campaign = await TestCampaign.Create(fixture, "Initiative ties", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);
        await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin", count = 8, initiativeRoll = "10" });
        fixture.LoginAsUser(Users.DM);
        var started = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;

        var stored = await Stored(combat.Id);
        var expected = stored.Combatants.OrderByDescending(c => c.Tiebreak).ThenBy(c => c.Id).Select(c => c.Id).ToList();
        started.Combatants.Select(c => c.Id).Should().Equal(expected, "equal initiatives sort by the hidden tiebreak");
        started.TurnCombatantId.Should().Be(expected[0]);

        fixture.LoginAsUser(Users.Player);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.Combatants.Select(c => c.Id).Should().Equal(expected);
        fixture.LoginAsUser(Users.DM);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.Combatants.Select(c => c.Id).Should().Equal(expected);
    }

    [Fact]
    public async Task ATypedInitiative_IsKeptByTheNextRoll()
    {
        var campaign = await TestCampaign.Create(fixture, "Initiative typed", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);
        var added = await fixture.AddAsDm(campaign.Id, combat.Id,
            new { name = "Typed", initiativeRoll = "1d20" },
            new { name = "Rolled", initiativeRoll = "30" });
        fixture.LoginAsUser(Users.DM);
        await fixture.PutCombatant(campaign.Id, combat.Id, added.Named("Typed").Id,
            Body(added.Named("Typed"), b => b["initiative"] = 31)).Ok();

        var started = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;

        started.Named("Typed").Initiative.Should().Be(31);
        started.Names().Should().Equal("Typed", "Rolled");
        (await TestCampaign.EventsOf(fixture, combat.Id)).Select(e => e.Data).OfType<InitiativeRolled>().Single()
            .Rolls.Select(r => r.CombatantId).Should().Equal(started.Named("Rolled").Id);
    }
}
