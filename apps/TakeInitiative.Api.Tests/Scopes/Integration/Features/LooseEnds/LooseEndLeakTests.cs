using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.LooseEnds;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.LooseEnds.LooseEndTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.LooseEnds;

/// <summary>
/// The loose-end leak tests (19b.6, invariants 5 and 7): a player's list and counts hold only
/// what they can see and resolve. <see cref="Users.Player"/> is the player,
/// <see cref="Users.Outsider"/> a second player.
/// </summary>
public class LooseEndLeakTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    /// <summary>The player's raw list and counts, checking the counts never hold what the list omits.</summary>
    private async Task<(LooseEndResponse[] Items, string Body)> PlayerView(Guid campaignId)
    {
        var raw = await fixture.LooseEnds(Users.Player, campaignId).Ok();
        var items = raw.As<LooseEndsResponse>().Items;
        var counts = await fixture.LooseEndCountsOf(Users.Player, campaignId);
        counts.Total.Should().Be(items.Length, "the counts never include what the list omits");
        counts.BySession.Values.Sum().Should().Be(items.Count(i => i.SessionId != null));
        return (items, raw.Body);
    }

    [Fact]
    public async Task APlayer_NeverGetsAnotherMembersNote()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak loose notes");
        var dmNote = await fixture.Note(Users.DM, campaign.Id, "Everyone reads this");
        var dmSecret = await fixture.Note(Users.DM, campaign.Id, "Only DMs", Visibility.DM);
        var theirs = await fixture.Note(Users.Outsider, campaign.Id, "The other player's");
        var theirImage = await fixture.ImageNote(Users.Outsider, campaign.Id);
        var mine = await fixture.Note(Users.Player, campaign.Id, "Mine");

        var (items, body) = await PlayerView(campaign.Id);
        items.Keys().Should().Equal((LooseEndKind.UnlinkedNote, mine.Id));
        foreach (var id in new[] { dmNote.Id, dmSecret.Id, theirs.Id, theirImage.Id })
        {
            body.Should().NotContain(id.ToString());
        }
    }

    [Fact]
    public async Task APlayer_NeverGetsADmEntry_OrOneTheyCannotEdit()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak loose entries");
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "Klarg", Visibility.DM, EntryKind.Other);
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(klarg)} waits.", Visibility.DM);
        var locked = await fixture.Entry(Users.Outsider, campaign.Id, "Outsider's secret plan", kind: EntryKind.Other);
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.PutEntryEditAccess(campaign.Id, locked.Id, EditAccess.OnlyMe)).Should().Succeed();
        var open = await fixture.Entry(Users.Outsider, campaign.Id, "Open plan", kind: EntryKind.Other);

        var (items, body) = await PlayerView(campaign.Id);
        items.Keys().Should().Equal((LooseEndKind.OtherKind, open.Id));
        body.Should().NotContain(klarg.Id.ToString()).And.NotContain("Klarg");
        body.Should().NotContain(locked.Id.ToString(), "Only me: the player cannot resolve it");

        (await fixture.LooseEndsOf(Users.Outsider, campaign.Id)).Keys().Should().Contain((LooseEndKind.OtherKind, locked.Id));
        (await fixture.LooseEndsOf(Users.DM, campaign.Id)).Keys().Should().Contain([
            (LooseEndKind.OtherKind, klarg.Id), (LooseEndKind.EmptyArticle, klarg.Id), (LooseEndKind.OtherKind, locked.Id)]);
    }

    [Fact]
    public async Task APlayer_NeverGetsASuggestionOfADmEntry()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak loose suggestions", withSecondPlayer: false);
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "Klarg", Visibility.DM);
        var sildar = await fixture.Entry(Users.DM, campaign.Id, "Sildar");
        await fixture.Note(Users.Player, campaign.Id, "Klarg and Sildar argued");
        await fixture.Note(Users.DM, campaign.Id, "Klarg and Sildar argued");

        var (items, body) = await PlayerView(campaign.Id);
        items.Single().Suggestions.Select(s => s.Entry.Id).Should().Equal(sildar.Id);
        body.Should().NotContain(klarg.Id.ToString());

        var dm = await fixture.LooseEndsOf(Users.DM, campaign.Id);
        dm.Single().Suggestions.Select(s => s.Entry.Id).Should().Equal(klarg.Id, sildar.Id);
    }

    [Fact]
    public async Task AnEmptyArticle_IsPerViewer()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak loose empty", withSecondPlayer: false);
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");
        await fixture.Article(Users.DM, campaign.Id, tharden.Id, new BlockEdit(null, "Killed by Klarg.", Visibility.DM));
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(tharden)} is missing.");

        var (items, body) = await PlayerView(campaign.Id);
        items.Keys().Should().Equal((LooseEndKind.EmptyArticle, tharden.Id));
        body.Should().NotContain("Killed by Klarg");

        (await fixture.LooseEndsOf(Users.DM, campaign.Id)).Should().BeEmpty("the DM can see the secret, so the article is not empty to them");
    }
}
