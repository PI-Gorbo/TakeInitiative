using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Secret blocks per viewer (15e.2 and 15e.7, invariant 5): reads, pushes, conflicts and
/// history. The entry's creator is <see cref="Users.Player"/>. The viewers are the DM, the
/// creator (a player) and <see cref="Users.Outsider"/> (the other player).
/// </summary>
public class SecretBlockTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    public enum Who { Dm, Creator, OtherPlayer }

    private static readonly Who[] Everybody = [Who.Dm, Who.Creator, Who.OtherPlayer];

    private static Users UserOf(Who who) => who switch
    {
        Who.Dm => Users.DM,
        Who.Creator => Users.Player,
        _ => Users.Outsider,
    };

    private static Guid MemberIdOf(TestCampaign campaign, Who who) => who switch
    {
        Who.Dm => campaign.DmMemberId,
        Who.Creator => campaign.PlayerMemberId,
        _ => campaign.SecondPlayerMemberId!.Value,
    };

    private IDocumentStore Store => fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();

    private async Task Seed(Guid entryId, Guid actorId, params ArticleBlock[] blocks)
    {
        await using var session = Store.LightweightSession();
        session.Events.Append(entryId, new EntryArticleEdited(Actor.Member(actorId), blocks));
        await session.SaveChangesAsync();
    }

    private async Task<EntryResponse?> Get(Who who, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(UserOf(who));
        var entry = await fixture.GetEntry(campaignId, entryId);
        return entry.IsSuccess ? entry.Value : null;
    }

    private async Task<IReadOnlyList<HubMessage>> Pushed(Func<Task> act)
    {
        var mark = fixture.Hub.Messages.Count;
        await act();
        return fixture.Hub.Messages.Skip(mark).ToList();
    }

    public static TheoryData<Visibility, Visibility, Who> Cases()
    {
        var data = new TheoryData<Visibility, Visibility, Who>();
        foreach (var entry in Enum.GetValues<Visibility>())
            foreach (var block in Enum.GetValues<Visibility>())
                foreach (var owner in Everybody)
                    data.Add(entry, block, owner);
        return data;
    }

    /// <summary>The table, written out rather than computed with the rule under test.</summary>
    private static bool Expected(Visibility entry, Visibility block, Who owner, Who viewer)
    {
        var seesEntry = entry switch
        {
            Visibility.Everyone => true,
            Visibility.DM => viewer is Who.Dm or Who.Creator,
            _ => viewer == Who.Creator,
        };
        var inBlock = viewer == owner || block switch
        {
            Visibility.Everyone => true,
            Visibility.DM => viewer == Who.Dm,
            _ => false,
        };
        return seesEntry && inBlock;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task GetEntry_HasTheBlock_ExactlyWhenCanSeeBlockSaysSo(Visibility entryVisibility, Visibility blockVisibility, Who owner)
    {
        var campaign = await TestCampaign.Create(fixture, $"Secret {entryVisibility} {blockVisibility} {owner}");
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Gundren", EntryKind.Character, entryVisibility)).Value;
        var block = new ArticleBlock { Id = Guid.NewGuid(), Text = "The secret", Visibility = blockVisibility, OwnerMemberId = MemberIdOf(campaign, owner) };
        await Seed(entry.Id, campaign.DmMemberId, block);

        using var session = Store.QuerySession();
        var stored = (await session.LoadAsync<Entry>(entry.Id))!;
        var campaignDoc = (await session.LoadAsync<TakeInitiative.Api.Features.Campaigns.Campaign>(campaign.Id))!;

        foreach (var viewer in Everybody)
        {
            var member = campaignDoc.MemberById(MemberIdOf(campaign, viewer))!;
            var expected = Expected(entryVisibility, blockVisibility, owner, viewer);
            EntryVisibility.CanSeeBlock(stored, stored.Article.Blocks.Single(), member).Should().Be(expected, $"the rule, for {viewer}");

            var read = await Get(viewer, campaign.Id, entry.Id);
            var blocks = read?.Article.Blocks ?? [];
            blocks.Any(b => b.Id == block.Id).Should().Be(expected, $"GET for {viewer}");
            if (!expected)
            {
                blocks.Should().BeEmpty("a hidden block leaves no placeholder");
            }
        }
    }

    private async Task<(TestCampaign Campaign, EntryResponse Entry, ArticleBlock Ordinary, ArticleBlock DmSecret, ArticleBlock CreatorSecret)> Gundren(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        var ordinary = new ArticleBlock { Id = Guid.NewGuid(), Text = "Dwarf prospector.", Visibility = Visibility.Everyone, OwnerMemberId = campaign.PlayerMemberId };
        var dmSecret = new ArticleBlock { Id = Guid.NewGuid(), Text = "Glasstaff is Iarno.", Visibility = Visibility.DM, OwnerMemberId = campaign.DmMemberId };
        var creatorSecret = new ArticleBlock { Id = Guid.NewGuid(), Text = "Captured by Klarg.", Visibility = Visibility.DM, OwnerMemberId = campaign.PlayerMemberId };
        await Seed(entry.Id, campaign.DmMemberId, ordinary, dmSecret, creatorSecret);
        return (campaign, entry, ordinary, dmSecret, creatorSecret);
    }

    private static BlockEdit[] Edit(EntryResponse view, Guid blockId, Func<ArticleBlockResponse, BlockEdit> change)
        => view.Article.Blocks.Select(b => b.Id == blockId ? change(b) : BlockEdit.Keep(b)).ToArray();

    private async Task Save(Who who, Guid campaignId, Guid entryId, Func<EntryResponse, BlockEdit[]> edit)
    {
        var view = (await Get(who, campaignId, entryId))!;
        (await fixture.PutEntryArticle(campaignId, entryId, view.Article.Etag, edit(view))).Should().Succeed();
    }

    private static void ShouldReachExactly(IReadOnlyList<HubMessage> pushed, Guid entryId, TestCampaign campaign, params Who[] who)
    {
        var message = pushed.Should().ContainSingle(m => m.Method == CampaignHubMessages.EntryArticleChanged).Subject;
        message.Groups.Should().BeEquivalentTo(who.Select(w => CampaignGroups.Member(MemberIdOf(campaign, w))));
        message.Payload.Should().Be(new EntryArticleChangedMessage(entryId));
        pushed.Should().NotContain(m => m.Method == CampaignHubMessages.EntryUpserted, "the summary did not change");
    }

    [Fact]
    public async Task ADmEditingTheirOwnSecret_PingsOnlyTheDm()
    {
        var (campaign, entry, _, dmSecret, _) = await Gundren("Push dm secret");

        var pushed = await Pushed(() => Save(Who.Dm, campaign.Id, entry.Id, v => Edit(v, dmSecret.Id, b => BlockEdit.Change(b, "Glasstaff is Iarno Albrek."))));

        ShouldReachExactly(pushed, entry.Id, campaign, Who.Dm);
    }

    [Fact]
    public async Task ADmEditingAPlayersDmBlock_PingsTheDmAndThatPlayer()
    {
        var (campaign, entry, _, _, creatorSecret) = await Gundren("Push creator secret");

        var pushed = await Pushed(() => Save(Who.Dm, campaign.Id, entry.Id, v => Edit(v, creatorSecret.Id, b => BlockEdit.Change(b, "Taken to Cragmaw."))));

        ShouldReachExactly(pushed, entry.Id, campaign, Who.Dm, Who.Creator);
    }

    [Fact]
    public async Task AnOrdinaryEdit_PingsEveryone()
    {
        var (campaign, entry, ordinary, _, _) = await Gundren("Push ordinary");

        var pushed = await Pushed(() => Save(Who.OtherPlayer, campaign.Id, entry.Id, v => Edit(v, ordinary.Id, b => BlockEdit.Change(b, "Dwarf."))));

        ShouldReachExactly(pushed, entry.Id, campaign, Who.Dm, Who.Creator, Who.OtherPlayer);
    }

    [Fact]
    public async Task AMeBlock_PingsOnlyItsOwner()
    {
        var (campaign, entry, _, _, _) = await Gundren("Push me block");

        var pushed = await Pushed(() => Save(Who.OtherPlayer, campaign.Id, entry.Id, v => [.. v.Article.Blocks.Select(BlockEdit.Keep), new BlockEdit(null, "I owe him gold.", Visibility.Me)]));

        ShouldReachExactly(pushed, entry.Id, campaign, Who.OtherPlayer);
    }

    [Fact]
    public async Task RevealingASecret_PingsThePlayersWhoNowSeeIt()
    {
        var (campaign, entry, _, dmSecret, _) = await Gundren("Push reveal");

        var pushed = await Pushed(() => Save(Who.Dm, campaign.Id, entry.Id, v => Edit(v, dmSecret.Id, b => new BlockEdit(b.Id, b.Text, Visibility.Everyone))));

        ShouldReachExactly(pushed, entry.Id, campaign, Who.Dm, Who.Creator, Who.OtherPlayer);
        (await Get(Who.OtherPlayer, campaign.Id, entry.Id))!.Article.Blocks.Should().Contain(b => b.Id == dmSecret.Id);
    }

    [Fact]
    public async Task AnArticleEditOnADmEntry_NeverPingsTheOtherPlayer()
    {
        var (campaign, entry, ordinary, _, _) = await Gundren("Push dm entry");
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryVisibility(campaign.Id, entry.Id, Visibility.DM)).Should().Succeed();

        var pushed = await Pushed(() => Save(Who.Creator, campaign.Id, entry.Id, v => Edit(v, ordinary.Id, b => BlockEdit.Change(b, "Dwarf."))));

        ShouldReachExactly(pushed, entry.Id, campaign, Who.Dm, Who.Creator);
    }

    [Fact]
    public async Task EveryViewersResponse_IsTheirOwnView()
    {
        var (campaign, entry, ordinary, dmSecret, creatorSecret) = await Gundren("Responses per viewer");

        fixture.LoginAsUser(Users.Outsider);
        var otherView = (await Get(Who.OtherPlayer, campaign.Id, entry.Id))!;
        var saved = await fixture.PutEntryArticle(campaign.Id, entry.Id, otherView.Article.Etag, Edit(otherView, ordinary.Id, b => BlockEdit.Change(b, "Dwarf.")));
        saved.Value.Article.Blocks.Select(b => b.Id).Should().Equal(ordinary.Id);

        (await Get(Who.Creator, campaign.Id, entry.Id))!.Article.Blocks.Select(b => b.Id).Should().Equal(ordinary.Id, creatorSecret.Id);
        (await Get(Who.Dm, campaign.Id, entry.Id))!.Article.Blocks.Select(b => b.Id).Should().Equal(ordinary.Id, dmSecret.Id, creatorSecret.Id);

        // A rename answers with the caller's view too.
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.PutEntryName(campaign.Id, entry.Id, "Gundren Rockseeker")).Value.Article.Blocks.Select(b => b.Id).Should().Equal(ordinary.Id);
    }

    [Fact]
    public async Task Conflicts_ArePerViewer()
    {
        var (campaign, entry, _, dmSecret, creatorSecret) = await Gundren("Conflicts per viewer");
        var views = new Dictionary<Who, EntryResponse>();
        foreach (var who in Everybody)
        {
            views[who] = (await Get(who, campaign.Id, entry.Id))!;
        }

        // The DM edits their own secret: only the DM's view changed.
        await Save(Who.Dm, campaign.Id, entry.Id, v => Edit(v, dmSecret.Id, b => BlockEdit.Change(b, "Glasstaff is Iarno Albrek.")));

        async Task<int> SaveStale(Who who)
        {
            fixture.LoginAsUser(UserOf(who));
            var view = views[who];
            return (await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, entry.Id),
                ArticleBody(view.Article.Etag, view.Article.Blocks.Select(BlockEdit.Keep)))).Status;
        }

        (await SaveStale(Who.Creator)).Should().Be(200);
        (await SaveStale(Who.OtherPlayer)).Should().Be(200);

        // The creator edits their DM block: the DM's stale view conflicts, the other player's does not.
        views[Who.Dm] = (await Get(Who.Dm, campaign.Id, entry.Id))!;
        views[Who.OtherPlayer] = (await Get(Who.OtherPlayer, campaign.Id, entry.Id))!;
        await Save(Who.Creator, campaign.Id, entry.Id, v => Edit(v, creatorSecret.Id, b => BlockEdit.Change(b, "Taken to Cragmaw.")));

        (await SaveStale(Who.Dm)).Should().Be(409);
        (await SaveStale(Who.OtherPlayer)).Should().Be(200);
    }

    [Fact]
    public async Task History_FromTheEventStream_IsRedactedPerViewer()
    {
        var (campaign, entry, ordinary, dmSecret, _) = await Gundren("History per viewer");
        await Save(Who.Dm, campaign.Id, entry.Id, v => Edit(v, dmSecret.Id, b => BlockEdit.Change(b, "Glasstaff is Iarno Albrek.")));
        await Save(Who.OtherPlayer, campaign.Id, entry.Id, v => Edit(v, ordinary.Id, b => BlockEdit.Change(b, "Dwarf.")));

        // Rebuild every version from the stream, as 15g's history endpoint will.
        var versions = new List<ArticleVersion>();
        IReadOnlyList<ArticleBlock> blocks = [];
        foreach (var e in await TestCampaign.EventsOf(fixture, entry.Id))
        {
            switch (e.Data)
            {
                case EntryArticleEdited edited: blocks = edited.Blocks; break;
                case EntryQuotePromoted promoted: blocks = [.. blocks, promoted.Block]; break;
                default: continue;
            }
            versions.Add(new ArticleVersion(e.Timestamp, ((IActorEvent)e.Data).Actor, blocks));
        }
        versions.Should().HaveCount(3);

        using var session = Store.QuerySession();
        var stored = (await session.LoadAsync<Entry>(entry.Id))!;
        var campaignDoc = (await session.LoadAsync<TakeInitiative.Api.Features.Campaigns.Campaign>(campaign.Id))!;
        IReadOnlyList<ArticleVersion> For(Who who) => ArticleHistory.For(stored, versions, campaignDoc.MemberById(MemberIdOf(campaign, who))!);

        For(Who.Dm).Should().HaveCount(3);
        var other = For(Who.OtherPlayer);
        other.Should().HaveCount(2, "the DM's edit inside their secret changed nothing the other player sees");
        other.SelectMany(v => v.Blocks).Should().NotContain(b => b.Id == dmSecret.Id);
        For(Who.Creator).SelectMany(v => v.Blocks).Should().NotContain(b => b.Id == dmSecret.Id);
    }
}
