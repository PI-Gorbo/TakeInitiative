using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>
/// Combat cards in the session stream (18e.1): one per combat in its session, built from the
/// caller's own view, a Draft's card for DMs only, the filters, and the card of a combat run in
/// a later session than it was prepared in.
/// </summary>
public class CombatStreamTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public CombatStreamTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    private async Task<SessionStreamSession[]> Stream(Users who, Guid campaignId, SessionStreamFilter? filter = null)
    {
        fixture.LoginAsUser(who);
        var stream = await fixture.GetSessionStream(campaignId, filter);
        stream.Should().Succeed();
        return stream.Value.Sessions;
    }

    [Fact]
    public async Task ADraft_IsACardForDms_AndNothingForPlayers()
    {
        var campaign = await TestCampaign.Create(fixture, "Stream draft", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id, "zanthor prep");

        var dm = (await Stream(Users.DM, campaign.Id)).Single().Combats.Single();
        (dm.Id, dm.Name, dm.Status).Should().Be((combat.Id, "zanthor prep", CombatStatus.Draft));

        fixture.LoginAsUser(Users.Player);
        var raw = await fixture.Call(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/stream");
        raw.Status.Should().Be(200);
        raw.Body.Should().NotContainEquivalentOf("zanthor").And.NotContain(combat.Id.ToString());
    }

    [Fact]
    public async Task ACard_GroupsByEntry_AndLeavesHiddenCombatantsOut_ForAPlayer()
    {
        var campaign = await TestCampaign.Create(fixture, "Stream card", withSecondPlayer: false);
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin");
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "zanthor", Visibility.DM);
        var combat = await fixture.CreateCombat(campaign.Id);
        await fixture.AddAsDm(campaign.Id, combat.Id,
            new { entryId = klarg.Id },
            new { entryId = goblin.Id, count = 4 },
            new { name = "Bandit" });
        fixture.LoginAsUser(Users.DM);
        await fixture.Roll(campaign.Id, combat.Id).Ok();

        var dmCard = (await Stream(Users.DM, campaign.Id)).Single().Combats.Single();
        dmCard.Status.Should().Be(CombatStatus.Active);
        dmCard.Combatants.Select(c => (c.EntryId, c.Count)).Should().BeEquivalentTo(new[]
        {
            ((Guid?)klarg.Id, 1), (goblin.Id, 4), (null, 1),
        });
        dmCard.Combatants.Single(c => c.EntryId == goblin.Id).Name.Should().Be("Goblin");

        var playerCard = (await Stream(Users.Player, campaign.Id)).Single().Combats.Single();
        playerCard.Combatants.Select(c => (c.Name, c.EntryId, c.Count)).Should().BeEquivalentTo(new[]
        {
            ("Goblin", (Guid?)goblin.Id, 4), ("Bandit", null, 1),
        });
        fixture.LoginAsUser(Users.Player);
        (await fixture.Call(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/stream")).Body
            .Should().NotContainEquivalentOf("zanthor").And.NotContain(klarg.Id.ToString());
    }

    [Fact]
    public async Task TheCombatsFilter_IsCardsOnly_AndTheOthersAreNotesOnly()
    {
        var campaign = await TestCampaign.Create(fixture, "Stream filters", withSecondPlayer: false);
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostSessionNote(campaign.Id, "A note", isRecap: true)).Should().Succeed();
        await fixture.CreateCombat(campaign.Id);

        var all = (await Stream(Users.DM, campaign.Id)).Single();
        (all.Notes.Length, all.Combats.Length).Should().Be((1, 1));

        var combats = (await Stream(Users.DM, campaign.Id, SessionStreamFilter.Combats)).Single();
        (combats.Notes.Length, combats.Combats.Length).Should().Be((0, 1));

        foreach (var filter in new[] { SessionStreamFilter.Text, SessionStreamFilter.Images, SessionStreamFilter.Recaps, SessionStreamFilter.Mine })
        {
            (await Stream(Users.DM, campaign.Id, filter)).Single().Combats.Should().BeEmpty(filter.ToString());
        }
    }

    [Fact]
    public async Task ACombatRunInALaterSession_IsACardInThatSession()
    {
        var campaign = await TestCampaign.Create(fixture, "Stream moved", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);
        await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin", initiativeRoll = "10" });
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostStartSession(campaign.Id, 2)).Should().Succeed();
        await fixture.Roll(campaign.Id, combat.Id).Ok();

        var sessions = await Stream(Users.Player, campaign.Id);
        sessions.Select(s => (s.Session.Number, s.Combats.Length)).Should().Equal((1, 0), (2, 1));
    }
}
