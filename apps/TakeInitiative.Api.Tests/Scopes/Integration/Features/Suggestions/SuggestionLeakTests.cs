using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Suggestions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Suggestions.SuggestionTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Suggestions;

/// <summary>
/// The suggestion leak tests (23c.8, invariant 5): a player's <c>match</c> never returns an entry
/// they cannot see, by its name or an alias, and <c>models</c> never counts another member's notes.
/// <see cref="Users.Player"/> is the player, <see cref="Users.Outsider"/> a second player.
/// </summary>
public class SuggestionLeakTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    [Fact]
    public async Task APlayersMatch_NeverReturnsADmEntry_OrAnotherMembersOnlyMe_ByNameOrAlias()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak match");
        var court = await fixture.Entry(Users.DM, campaign.Id, "Ember Court", Visibility.DM);
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryAliases(campaign.Id, court.Id, "Ashen Circle")).Should().Succeed();
        var plan = await fixture.Entry(Users.Outsider, campaign.Id, "Velvet Scheme", Visibility.Me);
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.PutEntryAliases(campaign.Id, plan.Id, "Quiet Knife")).Should().Succeed();

        var spans = new[] { "Ember Court", "ashen circle", "Velvet Scheme", "quiet knife" };
        var raw = await fixture.Match(Users.Player, campaign.Id, spans).Ok();
        raw.As<PostSuggestionMatchResponse>().Matches.Should().Equal([null, null, null, null]);
        foreach (var leak in new[] { court.Id.ToString(), plan.Id.ToString(), "Ember Court\"", "Velvet Scheme\"" })
        {
            raw.Body.Should().NotContain(leak);
        }

        (await fixture.MatchesOf(Users.DM, campaign.Id, spans))[0]!.Entry.Id.Should().Be(court.Id, "the DM can see it");
        (await fixture.MatchesOf(Users.Outsider, campaign.Id, spans))[3]!.Entry.Id.Should().Be(plan.Id, "its creator can see it");
    }

    [Fact]
    public async Task AMergedEntry_IsNeverAMatch_ItsNameFindsItsTarget()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak merged", withSecondPlayer: false);
        var merged = await fixture.Entry(Users.Player, campaign.Id, "Emberfall");
        var target = await fixture.Entry(Users.Player, campaign.Id, "Quelline Alderleaf");
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostEntryMerge(campaign.Id, merged.Id, target.Id)).Should().Succeed();

        var match = (await fixture.MatchesOf(Users.Player, campaign.Id, "Emberfall")).Single();
        match!.Entry.Id.Should().Be(target.Id);
    }

    [Fact]
    public async Task Models_NeverCountAnotherMembersNotes()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak models");
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        var dms = await fixture.Note(Users.DM, campaign.Id, "met rellan");
        await fixture.Accepted(Users.DM, campaign.Id, dms, "rellan", rellan.Id);
        var theirs = await fixture.Note(Users.Outsider, campaign.Id, "met rellan too");
        await fixture.Accepted(Users.Outsider, campaign.Id, theirs, "rellan", rellan.Id, version: "outsider-only");

        var raw = await fixture.Models(Users.Player, campaign.Id).Ok();
        raw.As<GetSuggestionModelsResponse>().Models.Should().BeEmpty();
        raw.Body.Should().NotContain("outsider-only");
        (await fixture.Reverted(Users.Player, campaign.Id, "outsider-only")).Notes.Should().Be(0);

        (await fixture.ModelsOf(Users.Outsider, campaign.Id)).Select(m => m.Version).Should().Equal("outsider-only");
        (await fixture.ModelsOf(Users.DM, campaign.Id)).Select(m => (m.Mentions, m.Notes)).Should().Equal((1, 1));
    }
}
