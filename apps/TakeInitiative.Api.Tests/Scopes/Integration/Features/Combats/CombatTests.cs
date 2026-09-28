using FluentAssertions;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Combats;

/// <summary>
/// Creating, listing and reading combats (18a.6). <see cref="Users.DM"/> owns each campaign;
/// <see cref="Users.Player"/> and <see cref="Users.Outsider"/> are its players. Finishing (18b.5)
/// and the Finished combat's read-only rule are here too.
/// </summary>
public class CombatTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public CombatTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    [Fact]
    public async Task ADm_CreatesADraft_InTheCurrentSession()
    {
        var campaign = await TestCampaign.Create(fixture, "Combat create");
        fixture.LoginAsUser(Users.DM);
        var session2 = await fixture.PostStartSession(campaign.Id, 2);

        var combat = await fixture.CreateCombat(campaign.Id, "  Goblin Ambush  ");

        combat.Name.Should().Be("Goblin Ambush");
        combat.Status.Should().Be(CombatStatus.Draft);
        combat.Round.Should().Be(0);
        combat.SessionId.Should().Be(session2.Value.Id);
        combat.TurnCombatantId.Should().BeNull();
        combat.StartedAt.Should().BeNull();
        combat.Combatants.Should().BeEmpty();

        var events = await TestCampaign.EventsOf(fixture, combat.Id);
        events.Should().ContainSingle().Which.Data.Should().BeOfType<CombatCreated>()
            .Which.Actor.MemberId.Should().Be(campaign.DmMemberId);
    }

    [Fact]
    public async Task Creating_WithNoSession_IsA409()
    {
        fixture.LoginAsUser(Users.DM);
        var created = await fixture.PostCreateCampaign(new() { Name = "Combat no session" });

        var reply = await fixture.Call(HttpMethod.Post, CombatsUrl(created.Value.Id), new { name = "Too soon" });

        reply.Status.Should().Be(409);
        reply.Body.Should().Contain("Start Session 1 first.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ANameIsRequired(string name)
    {
        var campaign = await TestCampaign.Create(fixture, $"Combat name '{name}'", withSecondPlayer: false);
        fixture.LoginAsUser(Users.DM);
        (await fixture.Call(HttpMethod.Post, CombatsUrl(campaign.Id), new { name })).Status.Should().Be(400);
        (await fixture.Call(HttpMethod.Post, CombatsUrl(campaign.Id), new { name = new string('x', 101) })).Status.Should().Be(400);
    }

    [Fact]
    public async Task APlayer_CannotCreate()
    {
        var campaign = await TestCampaign.Create(fixture, "Combat player create", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        (await fixture.Call(HttpMethod.Post, CombatsUrl(campaign.Id), new { name = "Mine" })).Status.Should().Be(403);
    }

    [Fact]
    public async Task ADraft_IsA404ForAPlayer_AndMissingFromTheirList()
    {
        var campaign = await TestCampaign.Create(fixture, "Combat draft 404", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);

        fixture.LoginAsUser(Users.Player);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Status.Should().Be(404);
        (await fixture.GetCombats(campaign.Id)).As<GetCombatsResponse>().Combats.Should().BeEmpty();

        fixture.LoginAsUser(Users.DM);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.Id.Should().Be(combat.Id);
    }

    [Fact]
    public async Task TheList_IsNewestFirst_FiltersByStatus_AndCarriesTheSessionNumber()
    {
        var campaign = await TestCampaign.Create(fixture, "Combat list", withSecondPlayer: false);
        var first = await fixture.CreateCombat(campaign.Id, "First");
        var second = await fixture.CreateCombat(campaign.Id, "Second");
        await fixture.AddAsDm(campaign.Id, second.Id, new { name = "Bandit", count = 3 });
        await fixture.Start(second.Id, new Dictionary<Guid, int>());

        fixture.LoginAsUser(Users.DM);
        var all = (await fixture.GetCombats(campaign.Id)).As<GetCombatsResponse>().Combats;
        all.Select(c => c.Name).Should().Equal("Second", "First");
        var active = all[0];
        (active.Status, active.Round, active.SessionNumber, active.CombatantCount).Should().Be((CombatStatus.Active, 1, 1, 3));

        (await fixture.GetCombats(campaign.Id, "Draft")).As<GetCombatsResponse>().Combats.Select(c => c.Id).Should().Equal(first.Id);
        (await fixture.GetCombats(campaign.Id, "Active,Finished")).As<GetCombatsResponse>().Combats.Select(c => c.Id).Should().Equal(second.Id);
        (await fixture.GetCombats(campaign.Id, "Paused")).Status.Should().Be(400);

        fixture.LoginAsUser(Users.Player);
        (await fixture.GetCombats(campaign.Id)).As<GetCombatsResponse>().Combats.Select(c => c.Id).Should().Equal(second.Id);
        (await fixture.GetCombat(campaign.Id, second.Id)).Status.Should().Be(200);
    }

    [Fact]
    public async Task AnotherCampaignsCombat_IsA404()
    {
        var campaign = await TestCampaign.Create(fixture, "Combat mine", withSecondPlayer: false);
        var other = await TestCampaign.Create(fixture, "Combat theirs", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(other.Id);

        fixture.LoginAsUser(Users.DM);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Status.Should().Be(404, "the route's campaign is not the combat's");
        (await fixture.GetCombat(campaign.Id, Guid.NewGuid())).Status.Should().Be(404);
    }

    // Finish (18b.5).

    [Fact]
    public async Task AFinishedCombat_IsReadOnly_WithNoTurn()
    {
        var campaign = await TestCampaign.Create(fixture, "Combat finished", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id);
        await fixture.AddAsDm(campaign.Id, combat.Id, new { name = "Goblin", initiativeRoll = "12" });
        fixture.LoginAsUser(Users.DM);
        var started = (await fixture.Roll(campaign.Id, combat.Id).Ok()).Combat;

        fixture.LoginAsUser(Users.Player);
        (await fixture.Finish(campaign.Id, combat.Id)).Status.Should().Be(403, "only a DM finishes");

        fixture.LoginAsUser(Users.DM);
        var finished = (await fixture.Finish(campaign.Id, combat.Id).Ok()).Combat;
        (finished.Status, finished.TurnCombatantId, finished.Round).Should().Be((CombatStatus.Finished, (Guid?)null, 1));
        finished.FinishedAt.Should().NotBeNull();

        var goblin = started.Named("Goblin");
        var writes = new[]
        {
            await fixture.Finish(campaign.Id, combat.Id),
            await fixture.Roll(campaign.Id, combat.Id),
            await fixture.EndTurn(campaign.Id, combat.Id, goblin.Id, 1),
            await fixture.AddCombatants(campaign.Id, combat.Id, new { name = "Late" }),
            await fixture.PutCombatant(campaign.Id, combat.Id, goblin.Id, Body(goblin, b => b["hp"] = 1)),
            await fixture.Position(campaign.Id, combat.Id, goblin.Id, null),
            await fixture.DeleteCombatant(campaign.Id, combat.Id, goblin.Id),
        };
        writes.Should().OnlyContain(w => w.Status == 409 && w.Body.Contains(CombatAccess.FinishedMessage));

        fixture.LoginAsUser(Users.Player);
        (await fixture.GetCombat(campaign.Id, combat.Id)).Combat.Status.Should().Be(CombatStatus.Finished, "a finished fight stays readable");
    }

    [Fact]
    public async Task FinishingADraft_DiscardsIt_AndPlayersNeverSeeIt()
    {
        var campaign = await TestCampaign.Create(fixture, "Combat discard", withSecondPlayer: false);
        var combat = await fixture.CreateCombat(campaign.Id, "Scrapped");

        fixture.LoginAsUser(Users.DM);
        var finished = (await fixture.Finish(campaign.Id, combat.Id).Ok()).Combat;
        (finished.Status, finished.StartedAt).Should().Be((CombatStatus.Finished, (DateTimeOffset?)null));
        (await fixture.GetCombats(campaign.Id, "Finished")).As<GetCombatsResponse>().Combats.Select(c => c.Id).Should().Equal(combat.Id);

        fixture.LoginAsUser(Users.Player);
        (await fixture.GetCombats(campaign.Id)).As<GetCombatsResponse>().Combats.Should().BeEmpty();
        (await fixture.GetCombat(campaign.Id, combat.Id)).Status.Should().Be(404);
    }
}
