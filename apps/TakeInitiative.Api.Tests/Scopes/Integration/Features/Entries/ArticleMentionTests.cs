using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Mentions in articles (15e.6): the mention index counts them and the timeline lists them,
/// per viewer. <see cref="Users.Player"/> writes, <see cref="Users.DM"/> is the DM and
/// <see cref="Users.Outsider"/> the other player.
/// </summary>
public class ArticleMentionTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private async Task<EntryListItemResponse> ListItem(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        return (await fixture.GetEntries(campaignId)).Value.Entries.Single(e => e.Entry.Id == entryId);
    }

    private async Task<EntryTimelineResponse> Timeline(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        return (await fixture.GetEntryTimeline(campaignId, entryId)).Value;
    }

    [Fact]
    public async Task ArticleMentions_AreCountedAndListed_OnlyWhereTheViewerCanSeeTheBlock()
    {
        var campaign = await TestCampaign.Create(fixture, "Article mentions");
        fixture.LoginAsUser(Users.Player);
        var tharden = (await fixture.PostEntry(campaign.Id, "Tharden")).Value;
        var klarg = (await fixture.PostEntry(campaign.Id, "Klarg")).Value;
        var gundren = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        var thardenMention = $"@[Tharden](entry:{tharden.Id})";
        var klargMention = $"@[Klarg](entry:{klarg.Id})";

        // An ordinary block mentions Tharden twice (one count). The DM's secret mentions Klarg,
        // and the article's own mention of Gundren is not counted.
        fixture.LoginAsUser(Users.DM);
        var saved = (await fixture.PutEntryArticle(campaign.Id, gundren.Id, gundren.Article.Etag,
        [
            new BlockEdit(null, $"Brother of {thardenMention}. {thardenMention} again. Self: @[Gundren](entry:{gundren.Id})"),
            new BlockEdit(null, $"Captured by {klargMention}.", Visibility.DM),
        ])).Value;
        fixture.LoginAsUser(Users.Player);
        await fixture.PostSessionNote(campaign.Id, $"Asked {thardenMention} about it.");

        var dmTharden = await ListItem(Users.DM, campaign.Id, tharden.Id);
        dmTharden.MentionCount.Should().Be(2);
        dmTharden.LastMentionedAt.Should().NotBeNull();
        (await ListItem(Users.Outsider, campaign.Id, tharden.Id)).MentionCount.Should().Be(2);

        var dmKlarg = await ListItem(Users.DM, campaign.Id, klarg.Id);
        dmKlarg.MentionCount.Should().Be(1);
        dmKlarg.LastMentionedAt.Should().BeNull("only an article mentions Klarg, and blocks have no time");
        (await ListItem(Users.Player, campaign.Id, klarg.Id)).MentionCount.Should().Be(0, "the secret is the DM's own");
        (await ListItem(Users.Outsider, campaign.Id, klarg.Id)).MentionCount.Should().Be(0);
        (await ListItem(Users.DM, campaign.Id, gundren.Id)).MentionCount.Should().Be(0);

        var secretId = saved.Article.Blocks[1].Id;
        (await Timeline(Users.DM, campaign.Id, klarg.Id)).ArticleMentions.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new EntryArticleMention { EntryId = gundren.Id, BlockIds = [secretId] });
        (await Timeline(Users.Outsider, campaign.Id, klarg.Id)).ArticleMentions.Should().BeEmpty();
        (await Timeline(Users.Outsider, campaign.Id, tharden.Id)).ArticleMentions.Should().ContainSingle()
            .Which.BlockIds.Should().Equal(saved.Article.Blocks[0].Id);
        (await Timeline(Users.DM, campaign.Id, gundren.Id)).ArticleMentions.Should().BeEmpty();
    }

    [Fact]
    public async Task AnArticleOnAnEntryTheViewerCannotSee_MentionsNothingForThem()
    {
        var campaign = await TestCampaign.Create(fixture, "Article mentions hidden entry");
        fixture.LoginAsUser(Users.Player);
        var tharden = (await fixture.PostEntry(campaign.Id, "Tharden")).Value;
        var plan = (await fixture.PostEntry(campaign.Id, "Plan", EntryKind.Other, Visibility.DM)).Value;
        (await fixture.PutEntryArticle(campaign.Id, plan.Id, plan.Article.Etag, [new BlockEdit(null, $"Find @[Tharden](entry:{tharden.Id}).")])).Should().Succeed();

        (await ListItem(Users.Player, campaign.Id, tharden.Id)).MentionCount.Should().Be(1);
        (await ListItem(Users.DM, campaign.Id, tharden.Id)).MentionCount.Should().Be(1);
        (await ListItem(Users.Outsider, campaign.Id, tharden.Id)).MentionCount.Should().Be(0);
        (await Timeline(Users.Outsider, campaign.Id, tharden.Id)).ArticleMentions.Should().BeEmpty();
    }
}
