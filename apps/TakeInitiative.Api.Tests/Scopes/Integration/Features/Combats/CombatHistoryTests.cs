using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>A combat's history (18b.6): one row per event, the actor on each, and DMs only.</summary>
public class CombatHistoryTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public CombatHistoryTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    [Fact]
    public async Task EveryEvent_IsARow_WithItsActorAndASentence()
    {
        var campaign = await TestCampaign.Create(fixture, "History rows", withSecondPlayer: false);
        var brynn = await fixture.PlayerCharacter(campaign);
        var combat = await fixture.CreateCombat(campaign.Id, "Goblin Ambush");
        var added = await fixture.AddAsDm(campaign.Id, combat.Id,
            new { name = "Goblin", count = 2, initiativeRoll = "17", maxHp = "7" });
        fixture.LoginAsUser(Users.DM);
        var started = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;
        fixture.LoginAsUser(Users.Player);
        await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id }).Ok();
        fixture.LoginAsUser(Users.DM);
        var goblin = started.Named("Goblin 1");
        await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, Body(goblin, b => b["hp"] = 2)).Ok();
        await fixture.EndCurrentTurn(campaign.Id, started);
        await fixture.DeleteCombatant(campaign.Id, combat.Id, started.Named("Goblin 2").Id).Ok();
        await fixture.Finish(campaign.Id, combat.Id).Ok();

        var reply = await fixture.History(campaign.Id, combat.Id);
        reply.Status.Should().Be(200, reply.Body);
        var items = reply.As<CombatHistoryResponse>().Items;

        items.Select(i => i.Kind).Should().Equal(
            CombatHistoryKind.Created,
            CombatHistoryKind.CombatantsAdded,
            CombatHistoryKind.InitiativeRolled,
            CombatHistoryKind.CombatantsAdded,
            CombatHistoryKind.CombatantEdited,
            CombatHistoryKind.TurnEnded,
            CombatHistoryKind.CombatantRemoved,
            CombatHistoryKind.Finished);
        items.Select(i => i.Version).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        items.Select(i => i.ActorMemberId).Should().Equal(
            campaign.DmMemberId, campaign.DmMemberId, campaign.DmMemberId, campaign.PlayerMemberId,
            campaign.DmMemberId, campaign.DmMemberId, campaign.DmMemberId, campaign.DmMemberId);
        items.Should().OnlyContain(i => i.Text.Length > 0 && i.Timestamp != default);

        items[0].Text.Should().EndWith("created Goblin Ambush.");
        items[1].Text.Should().EndWith("added Goblin 1 and Goblin 2.");
        items[2].Text.Should().EndWith("started the combat: Goblin 1 rolled 17 and Goblin 2 rolled 17.")
            .And.NotContain("rolled 17, Goblin", "a list reads naturally");
        items[4].Text.Should().EndWith("changed Goblin 1: HP 7 → 2.");
        items[5].Text.Should().Contain("'s turn.").And.Contain("Next: ");
        items[6].Text.Should().EndWith("removed Goblin 2.");
        items[7].Text.Should().EndWith("finished the combat.");
    }

    [Fact]
    public async Task Players_CannotReadIt()
    {
        var campaign = await TestCampaign.Create(fixture, "History players", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);

        fixture.LoginAsUser(Users.Player);
        (await fixture.History(campaign.Id, combat.Id)).Status.Should().Be(404, "a Draft does not exist for a player");

        fixture.LoginAsUser(Users.DM);
        await fixture.Roll(campaign.Id, combat.Id).Ok();
        fixture.LoginAsUser(Users.Player);
        (await fixture.History(campaign.Id, combat.Id)).Status.Should().Be(403);
    }
}
