using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>
/// An entry's combats (18e.4): through a combatant the caller can see, never through a hidden
/// one or a Draft for a player, and through an entry merged into it.
/// </summary>
public class EntryCombatsTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public EntryCombatsTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    private async Task<Reply> Combats(Users who, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Get, $"/api/campaigns/{campaignId}/entries/{entryId}/combats");
    }

    private async Task<Guid[]> CombatIds(Users who, Guid campaignId, Guid entryId)
        => (await Combats(who, campaignId, entryId).Ok()).As<EntryCombatsResponse>().Combats.Select(c => c.Combat.Id).ToArray();

    [Fact]
    public async Task AVisibleCombatant_ListsItsCombat_WithTheSessionNumber()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry combats visible", withSecondPlayer: false);
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin");
        var combat = await fixture.CreateCombat(campaign.Id);
        await fixture.AddAsDm(campaign.Id, combat.Id, new { entryId = goblin.Id, count = 2 });

        // A Draft: the DM sees it, a player does not.
        (await CombatIds(Users.DM, campaign.Id, goblin.Id)).Should().Equal(combat.Id);
        (await CombatIds(Users.Player, campaign.Id, goblin.Id)).Should().BeEmpty();

        fixture.LoginAsUser(Users.DM);
        await fixture.Roll(campaign.Id, combat.Id).Ok();
        var listed = (await Combats(Users.Player, campaign.Id, goblin.Id).Ok()).As<EntryCombatsResponse>().Combats.Single();
        (listed.Combat.Id, listed.SessionNumber).Should().Be((combat.Id, 1));
        listed.Combat.Combatants.Single().Count.Should().Be(2);
    }

    [Fact]
    public async Task AHiddenCombatant_NeverListsItsCombat_ForAPlayer()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry combats hidden", withSecondPlayer: false);
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin");
        var combat = await fixture.CreateCombat(campaign.Id, "zanthor ambush");
        await fixture.AddAsDm(campaign.Id, combat.Id, new { entryId = goblin.Id, hidden = true });
        fixture.LoginAsUser(Users.DM);
        await fixture.Roll(campaign.Id, combat.Id).Ok();

        (await CombatIds(Users.DM, campaign.Id, goblin.Id)).Should().Equal(combat.Id);
        var raw = await Combats(Users.Player, campaign.Id, goblin.Id).Ok();
        raw.Body.Should().NotContainEquivalentOf("zanthor").And.NotContain(combat.Id.ToString());
    }

    [Fact]
    public async Task AnEntryMergedIn_BringsItsCombats()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry combats merged", withSecondPlayer: false);
        var gob = await fixture.Entry(Users.DM, campaign.Id, "Gob");
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin");
        var combat = await fixture.CreateCombat(campaign.Id);
        await fixture.AddAsDm(campaign.Id, combat.Id, new { entryId = gob.Id });
        fixture.LoginAsUser(Users.DM);
        await fixture.Roll(campaign.Id, combat.Id).Ok();

        fixture.LoginAsUser(Users.DM);
        (await fixture.PostEntryMerge(campaign.Id, gob.Id, goblin.Id)).Should().Succeed();

        var listed = (await Combats(Users.Player, campaign.Id, goblin.Id).Ok()).As<EntryCombatsResponse>().Combats.Single();
        listed.Combat.Id.Should().Be(combat.Id);
        listed.Combat.Combatants.Single().EntryId.Should().Be(goblin.Id, "the card resolves the merged id");
    }

    [Fact]
    public async Task AnEntryTheCallerCannotSee_IsA404()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry combats 404", withSecondPlayer: false);
        var secret = await fixture.Entry(Users.DM, campaign.Id, "Secret", Visibility.DM);
        (await Combats(Users.Player, campaign.Id, secret.Id)).Status.Should().Be(404);
    }
}
