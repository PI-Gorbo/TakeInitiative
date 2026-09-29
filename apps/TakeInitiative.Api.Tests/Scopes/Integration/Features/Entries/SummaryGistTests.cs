using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// The wiki home's rows (25g): the summary gist, per viewer and never from a secret block,
/// and the note count with the session of the latest note. <see cref="Users.Player"/>
/// creates, <see cref="Users.DM"/> is the DM and <see cref="Users.Outsider"/> the other player.
/// </summary>
public class SummaryGistTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private async Task<EntryListItemResponse> ListItem(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        return (await fixture.GetEntries(campaignId)).Value.Entries.Single(e => e.Entry.Id == entryId);
    }

    [Fact]
    public async Task TheGist_IsTheFirstLineOfTheVisibleArticle_AndNeverASecret()
    {
        var campaign = await TestCampaign.Create(fixture, "Summary gist");
        fixture.LoginAsUser(Users.Player);
        var gundren = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        var tharden = (await fixture.PostEntry(campaign.Id, "Tharden")).Value;

        // No article: no gist.
        (await ListItem(Users.Player, campaign.Id, gundren.Id)).SummaryGist.Should().BeNull();

        // A DM secret first, then an ordinary block with a mention and some markdown.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryArticle(campaign.Id, gundren.Id, gundren.Article.Etag,
        [
            new BlockEdit(null, "Secretly works for the Black Spider.", Visibility.DM),
            new BlockEdit(null, $"Dwarf **prospector**, brother of @[Tharden](entry:{tharden.Id}).\n\nSecond paragraph."),
        ])).Should().Succeed();

        foreach (var user in new[] { Users.DM, Users.Player, Users.Outsider })
        {
            (await ListItem(user, campaign.Id, gundren.Id)).SummaryGist
                .Should().Be("Dwarf prospector, brother of Tharden.", $"{user} sees the ordinary block, never the secret");
        }
    }

    [Fact]
    public async Task AnArticleOfOnlySecrets_HasNoGist_EvenForItsReaders()
    {
        var campaign = await TestCampaign.Create(fixture, "Only secrets");
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Klarg")).Value;
        var saved = (await fixture.PutEntryArticle(campaign.Id, entry.Id, entry.Article.Etag,
            [new BlockEdit(null, "I owe him gold.", Visibility.Me)])).Value;
        fixture.LoginAsUser(Users.DM);
        var dmView = (await fixture.GetEntry(campaign.Id, entry.Id)).Value;
        (await fixture.PutEntryArticle(campaign.Id, entry.Id, dmView.Article.Etag,
            [new BlockEdit(null, "Bugbear chief.", Visibility.DM)])).Should().Succeed();

        (await ListItem(Users.Player, campaign.Id, entry.Id)).SummaryGist.Should().BeNull();
        (await ListItem(Users.DM, campaign.Id, entry.Id)).SummaryGist.Should().BeNull();
        (await ListItem(Users.Outsider, campaign.Id, entry.Id)).SummaryGist.Should().BeNull();
        saved.Article.Blocks.Should().ContainSingle();
    }

    [Fact]
    public async Task NoteCount_AndTheLatestNotesSession_AreListed_PerViewer()
    {
        var campaign = await TestCampaign.Create(fixture, "Note counts");
        fixture.LoginAsUser(Users.Player);
        var tharden = (await fixture.PostEntry(campaign.Id, "Tharden")).Value;
        var mention = $"@[Tharden](entry:{tharden.Id})";

        var none = await ListItem(Users.Player, campaign.Id, tharden.Id);
        none.NoteCount.Should().Be(0);
        none.LastMentionedSessionNumber.Should().BeNull();

        fixture.LoginAsUser(Users.Player);
        (await fixture.PostSessionNote(campaign.Id, $"Met {mention}.")).Should().Succeed();
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostStartSession(campaign.Id, 2)).Should().Succeed();
        (await fixture.PostSessionNote(campaign.Id, $"{mention} is lying.", Visibility.DM)).Should().Succeed();

        var dm = await ListItem(Users.DM, campaign.Id, tharden.Id);
        dm.NoteCount.Should().Be(2);
        dm.LastMentionedSessionNumber.Should().Be(2);

        var player = await ListItem(Users.Outsider, campaign.Id, tharden.Id);
        player.NoteCount.Should().Be(1, "the DM's note is hidden from players");
        player.LastMentionedSessionNumber.Should().Be(1, "a hidden note's session does not leak");
    }
}
