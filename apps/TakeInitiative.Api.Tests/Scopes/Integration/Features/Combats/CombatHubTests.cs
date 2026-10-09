using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>
/// <c>combatChanged</c> (18a.7): the DM view to the DM group, each player's own view to their
/// member group once the combat has started, and a Draft to the DMs only.
/// </summary>
public class CombatHubTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public CombatHubTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    private async Task<IReadOnlyList<HubMessage>> Pushed(Func<Task> act)
    {
        var mark = fixture.Hub.Messages.Count;
        await act();
        return fixture.Hub.Messages.Skip(mark).Where(m => m.Method == CampaignHubMessages.CombatChanged).ToList();
    }

    private static CombatChangedMessage To(IReadOnlyList<HubMessage> pushed, string group)
        => pushed.Should().ContainSingle(m => m.Groups.Contains(group)).Subject.Payload.Should().BeOfType<CombatChangedMessage>().Subject;

    [Fact]
    public async Task ADraft_ReachesNoPlayer()
    {
        var campaign = await TestCampaign.Create(fixture, "Hub combat draft");
        CombatResponse combat = null!;
        var created = await Pushed(async () => combat = await fixture.CreateCombat(campaign.Id));
        var added = await Pushed(() => fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin" }));

        foreach (var pushed in new[] { created, added })
        {
            var message = pushed.Should().ContainSingle().Subject;
            message.Groups.Should().Equal(CampaignGroups.Dms(campaign.Id));
            ((CombatChangedMessage)message.Payload!).Combat.Id.Should().Be(combat.Id);
        }
        ((CombatChangedMessage)added.Single().Payload!).Summary.CombatantCount.Should().Be(1);
    }

    [Fact]
    public async Task ADmGetsTheFullView_AndEachPlayerTheirOwn()
    {
        var campaign = await TestCampaign.Create(fixture, "Hub combat views");
        var brynn = await fixture.PlayerCharacter(campaign);
        var combat = await fixture.CreateCombat(campaign.Id);
        var added = await fixture.AddAsDm(campaign.Id, combat.Id,
            new { name = "Goblin", maxHp = "10" }, new { name = "Lurker", hidden = true });
        await fixture.Start(combat.Id, added.Combatants.ToDictionary(c => c.Id, _ => 10));

        fixture.LoginAsUser(Users.Player);
        var pushed = await Pushed(async () =>
            (await fixture.AddCombatants(campaign.Id, combat.Id, new { entryId = brynn.Id })).Status.Should().Be(200));

        pushed.Should().HaveCount(3, "the DM group and each of the two players")
            .And.OnlyContain(m => m.Groups.Count == 1);

        var dm = To(pushed, CampaignGroups.Dms(campaign.Id)).Combat;
        dm.Combatants.Select(c => c.Name).Should().BeEquivalentTo("Goblin", "Lurker", "Brynn");
        dm.Combatants.Single(c => c.Name == "Goblin").Hp.Should().Be(10);

        var player = To(pushed, CampaignGroups.Member(campaign.PlayerMemberId)).Combat;
        player.Combatants.Select(c => c.Name).Should().BeEquivalentTo("Goblin", "Brynn");
        player.Combatants.Single(c => c.Name == "Goblin").Hp.Should().BeNull();
        player.Combatants.Single(c => c.Name == "Brynn").InitiativeRoll.Should().Be("1d20+3", "their own, exactly");

        var other = To(pushed, CampaignGroups.Member(campaign.SecondPlayerMemberId!.Value));
        other.Combat.Combatants.Single(c => c.Name == "Brynn").InitiativeRoll.Should().BeNull("not their own");
        other.Combat.Combatants.Single(c => c.Name == "Brynn").Hp.Should().Be(24, "a player character is Exact");
        other.Summary.CombatantCount.Should().Be(2);
        pushed.Should().NotContain(m => m.Groups.Contains(CampaignGroups.Campaign(campaign.Id)), "no view goes to everyone at once");
        pushed.Should().NotContain(m => m.Groups.Contains(CampaignGroups.Member(campaign.DmMemberId)));
    }

    [Fact]
    public async Task ADemotedDm_StopsGettingTheFullView()
    {
        var campaign = await TestCampaign.Create(fixture, "Hub combat demote");
        await campaign.PromoteToDm(fixture, campaign.PlayerMemberId);
        var combat = await fixture.CreateCombat(campaign.Id);
        var added = await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Lurker", hidden = true }, new { name = "Goblin" });
        await fixture.Start(combat.Id, new Dictionary<Guid, int>());

        var asDm = await Pushed(() => fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Wolf" }));
        asDm.Should().NotContain(m => m.Groups.Contains(CampaignGroups.Member(campaign.PlayerMemberId)),
            "while a DM, they are reached through the DM group");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutMemberRole(campaign.Id, campaign.PlayerMemberId, Role.Player)).Should().Succeed();
        var asPlayer = await Pushed(() => fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Bat" }));

        var view = To(asPlayer, CampaignGroups.Member(campaign.PlayerMemberId)).Combat;
        view.Combatants.Select(c => c.Name).Should().NotContain("Lurker");
        added.Combatants.Should().Contain(c => c.Name == "Lurker");
    }
}
