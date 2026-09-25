using FakeItEasy;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using TakeInitiative.Utilities;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Entry history (15g.4): every change, oldest first, redacted per viewer, and restoring an old
/// version through <c>PUT article</c> keeps the blocks the restorer cannot see.
/// <see cref="Users.Player"/> creates the entry, <see cref="Users.DM"/> writes the secrets and
/// <see cref="Users.Outsider"/> is the other player.
/// </summary>
public class EntryHistoryTests : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private readonly AuthenticatedWebAppWithDatabaseFixture fixture;

    public EntryHistoryTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    {
        this.fixture = fixture;
        var real = new DiceRoller(Random.Shared);
        A.CallTo(() => fixture.DiceRoller.Check(A<string>._)).ReturnsLazily((string roll) => real.Check(roll));
    }

    private async Task<EntryResponse> Get(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        return (await fixture.GetEntry(campaignId, entryId)).Value;
    }

    private async Task<EntryResponse> Put(Users user, Guid campaignId, Guid entryId, params BlockEdit[] blocks)
    {
        var seen = await Get(user, campaignId, entryId);
        var saved = await fixture.PutEntryArticle(campaignId, entryId, seen.Article.Etag, blocks);
        saved.Should().Succeed();
        return saved.Value;
    }

    private async Task<(EntryHistoryItem[] Items, string Raw)> History(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        var history = await fixture.GetEntryHistory(campaignId, entryId);
        history.Should().Succeed();
        var raw = await fixture.AlbaHost.Scenario(_ => _.Get.Url(EntryUrl(campaignId, entryId, "history")));
        return (history.Value.Items, await raw.ReadAsTextAsync());
    }

    [Fact]
    public async Task History_ListsEveryChange_OldestFirst_WithItsActor()
    {
        var campaign = await TestCampaign.Create(fixture, "History all");
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        (await fixture.PutEntryName(campaign.Id, entry.Id, "Gundren Rockseeker")).Should().Succeed();
        (await fixture.PutEntryAliases(campaign.Id, entry.Id, "Rockseeker")).Should().Succeed();
        await Put(Users.Outsider, campaign.Id, entry.Id, new BlockEdit(null, "Dwarf."));
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryClaim(campaign.Id, entry.Id, campaign.PlayerMemberId)).Should().Succeed();
        (await fixture.PutEntryStats(campaign.Id, entry.Id, null, null, 14)).Should().Succeed();

        var (items, _) = await History(Users.Outsider, campaign.Id, entry.Id);

        items.Select(i => i.Change.Type).Should().Equal(
            EntryChangeType.Created, EntryChangeType.Renamed, EntryChangeType.AliasAdded,
            EntryChangeType.ArticleEdited, EntryChangeType.Claimed, EntryChangeType.StatsChanged);
        items.Select(i => i.ActorMemberId).Should().Equal(
            campaign.PlayerMemberId, campaign.PlayerMemberId, campaign.PlayerMemberId,
            campaign.SecondPlayerMemberId!.Value, campaign.PlayerMemberId, campaign.PlayerMemberId);
        items[1].Change.Name.Should().Be("Gundren Rockseeker");
        items[3].Change.Blocks!.Select(b => b.Text).Should().Equal("Dwarf.");
        items[5].Change.Stats!.Ac.Should().Be(14);
        items.Select(i => i.At).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task AnEditInsideASecret_LeavesNoTrace_ForSomeoneOutsideIt()
    {
        var campaign = await TestCampaign.Create(fixture, "History secret");
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        var v1 = await Put(Users.Player, campaign.Id, entry.Id, new BlockEdit(null, "Dwarf."));
        var dm = await Put(Users.DM, campaign.Id, entry.Id, BlockEdit.Keep(v1.Article.Blocks[0]), new BlockEdit(null, "Captured by Klarg.", Visibility.DM));
        await Put(Users.DM, campaign.Id, entry.Id, BlockEdit.Keep(dm.Article.Blocks[0]), BlockEdit.Change(dm.Article.Blocks[1], "Held at Cragmaw."));

        foreach (var user in new[] { Users.Player, Users.Outsider })
        {
            var (items, raw) = await History(user, campaign.Id, entry.Id);
            items.Count(i => i.Change.Type == EntryChangeType.ArticleEdited).Should().Be(1, "the two DM edits changed nothing they see");
            raw.Should().NotContain("Klarg").And.NotContain("Cragmaw");
        }
        var (dmItems, _) = await History(Users.DM, campaign.Id, entry.Id);
        dmItems.Count(i => i.Change.Type == EntryChangeType.ArticleEdited).Should().Be(3);
    }

    [Fact]
    public async Task AnNpcsStats_AreLeftOutOfAPlayersHistory()
    {
        var campaign = await TestCampaign.Create(fixture, "History stats");
        fixture.LoginAsUser(Users.DM);
        var klarg = (await fixture.PostEntry(campaign.Id, "Klarg")).Value;
        (await fixture.PutEntryStats(campaign.Id, klarg.Id, null, "5d8+10", 12)).Should().Succeed();

        var (items, raw) = await History(Users.Player, campaign.Id, klarg.Id);
        items.Should().NotContain(i => i.Change.Type == EntryChangeType.StatsChanged);
        raw.Should().NotContain("5d8");
        (await History(Users.DM, campaign.Id, klarg.Id)).Items.Should().Contain(i => i.Change.Type == EntryChangeType.StatsChanged);
    }

    [Fact]
    public async Task AMerge_ShowsTheMergedName_AndItsVisibleBlocks()
    {
        var campaign = await TestCampaign.Create(fixture, "History merge");
        fixture.LoginAsUser(Users.Player);
        var into = (await fixture.PostEntry(campaign.Id, "Gundren Rockseeker")).Value;
        var from = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        await Put(Users.DM, campaign.Id, from.Id, new BlockEdit(null, "Hired us."), new BlockEdit(null, "A spy.", Visibility.DM));
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostEntryMerge(campaign.Id, from.Id, into.Id)).Should().Succeed();

        var (items, raw) = await History(Users.Outsider, campaign.Id, into.Id);
        var merge = items.Should().ContainSingle(i => i.Change.Type == EntryChangeType.Merged).Subject;
        merge.Change.Name.Should().Be("Gundren");
        merge.Change.MergedEntryId.Should().Be(from.Id);
        merge.Change.Blocks!.Select(b => b.Text).Should().Equal("Merged from Gundren", "Hired us.");
        raw.Should().NotContain("A spy");
        // The old id's history is the target's.
        (await History(Users.Outsider, campaign.Id, from.Id)).Items.Should().HaveCount(items.Length);
    }

    [Fact]
    public async Task RestoringAnOldVersion_KeepsAHiddenBlock()
    {
        var campaign = await TestCampaign.Create(fixture, "History restore");
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        var v1 = await Put(Users.Player, campaign.Id, entry.Id, new BlockEdit(null, "Dwarf prospector."));
        var dm = await Put(Users.DM, campaign.Id, entry.Id, BlockEdit.Keep(v1.Article.Blocks[0]), new BlockEdit(null, "Captured.", Visibility.DM));
        await Put(Users.Player, campaign.Id, entry.Id, new BlockEdit(v1.Article.Blocks[0].Id, "Vandalised."), new BlockEdit(null, "More."));

        // The player restores their first version: its blocks, with the current etag. A block
        // that no longer exists would go without an id; this one still does.
        var (items, _) = await History(Users.Player, campaign.Id, entry.Id);
        var first = items.First(i => i.Change.Type == EntryChangeType.ArticleEdited).Change.Blocks!;
        var restored = await Put(Users.Player, campaign.Id, entry.Id, first.Select(b => new BlockEdit(b.Id, b.Text, b.Visibility)).ToArray());

        restored.Article.Blocks.Select(b => b.Text).Should().Equal("Dwarf prospector.");
        (await Get(Users.DM, campaign.Id, entry.Id)).Article.Blocks.Select(b => b.Text)
            .Should().Equal("Dwarf prospector.", "Captured.");
        dm.Article.Blocks.Should().HaveCount(2);
    }
}
