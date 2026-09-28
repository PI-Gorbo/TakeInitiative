using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.LooseEnds;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Suggestions.SuggestionTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Suggestions;

/// <summary>
/// <c>POST suggestions/match</c> (23c.2): the model's spans against the entries the caller can see,
/// in one matcher call, with 19b's <c>LinkSpans.Accepts</c>.
/// </summary>
public class SuggestionMatchTests(CountingMatcherFixture fixture) : IClassFixture<CountingMatcherFixture>
{
    [Fact]
    public async Task Matches_ComeBackInOrder_WithNullsForNone_InOneMatcherCall()
    {
        var campaign = await TestCampaign.Create(fixture, "Match order", withSecondPlayer: false);
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        var court = await fixture.Entry(Users.DM, campaign.Id, "Ember Court", kind: EntryKind.Faction);

        fixture.Matcher.Reset();
        var matches = await fixture.MatchesOf(Users.Player, campaign.Id, "Ember Court", "Greyhollow Keep", "Rellan Ashvale");
        fixture.Matcher.Calls.Should().Be(1);

        matches.Should().HaveCount(3);
        matches[0]!.Entry.Id.Should().Be(court.Id);
        matches[1].Should().BeNull();
        matches[2]!.Entry.Id.Should().Be(rellan.Id);
        matches[2]!.Similarity.Should().BeGreaterThanOrEqualTo(0.6);
    }

    [Fact]
    public async Task ALowerCaseSpan_OrAnAlias_MatchesTheEntry_WithItsOwnName()
    {
        var campaign = await TestCampaign.Create(fixture, "Match alias", withSecondPlayer: false);
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryAliases(campaign.Id, rellan.Id, "Rel")).Should().Succeed();

        var matches = await fixture.MatchesOf(Users.Player, campaign.Id, "rellan", "REL", "rellan ashvale");
        matches.Should().AllSatisfy(m => m!.Entry.Id.Should().Be(rellan.Id));
        matches.Should().AllSatisfy(m => m!.Entry.Name.Should().Be("Rellan Ashvale"));
    }

    [Fact]
    public async Task ASubstring_IsNotAMatch()
    {
        var campaign = await TestCampaign.Create(fixture, "Match substring", withSecondPlayer: false);
        await fixture.Entry(Users.DM, campaign.Id, "Brockton", kind: EntryKind.Place);

        (await fixture.MatchesOf(Users.Player, campaign.Id, "rock")).Should().Equal([null]);
    }

    [Fact]
    public async Task TheCaps_Are50Spans_Of1To80Characters()
    {
        var campaign = await TestCampaign.Create(fixture, "Match caps", withSecondPlayer: false);

        (await fixture.MatchesOf(Users.Player, campaign.Id, Enumerable.Repeat("Rellan", 50).ToArray())).Should().HaveCount(50);
        (await fixture.Match(Users.Player, campaign.Id, Enumerable.Repeat("Rellan", 51).ToArray())).Status.Should().Be(400);
        (await fixture.Match(Users.Player, campaign.Id, new string('a', 81))).Status.Should().Be(400);
        (await fixture.Match(Users.Player, campaign.Id, "  ")).Status.Should().Be(400);
        (await fixture.MatchesOf(Users.Player, campaign.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task ANonMember_Gets404()
    {
        var campaign = await TestCampaign.Create(fixture, "Match outsider", withSecondPlayer: false);
        (await fixture.Match(Users.Outsider, campaign.Id, "Rellan")).Status.Should().BeOneOf(403, 404);
    }
}
