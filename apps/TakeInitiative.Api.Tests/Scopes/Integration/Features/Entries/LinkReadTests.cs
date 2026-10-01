using FluentAssertions;

using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Reference;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// The links read path (27b): who may read an entry's links, what a knowledge-base link resolves to,
/// what a row that has gone resolves to, and what history shows. There is no endpoint that writes a
/// link in 27b, so every link here is appended straight to the stream
/// (<see cref="TestCampaign.Append"/>); 27c's <c>LinkApiTests</c> covers the endpoints.
/// </summary>
/// <remarks>
/// The read rule is <see cref="EntrySources.CanRead"/>, reused rather than reinvented, so the four
/// entry shapes below are the same four the source is tested against — and an unclaimed Character
/// hiding its links from a player is the case the whole arrangement exists for (invariant 8).
/// </remarks>
public class LinkReadTests(KnowledgeBaseFixture fixture) : IClassFixture<KnowledgeBaseFixture>
{
    private const string Gremlin = "monster_test-gremlin_tst";
    private const string Zombie = "monster_test-gremlin-zombie_tsta";
    private const string Provider = KnowledgeBaseCorpus.Provider;

    private static EntryLink KbLink(string itemId, Guid memberId, int minutes = 0, string provider = Provider) => new(
        Id: Guid.NewGuid(),
        Kind: EntryLinkKind.KnowledgeBase,
        Provider: provider,
        ItemId: itemId,
        Url: null,
        Label: null,
        AddedAt: Microseconds.Truncate(DateTimeOffset.UtcNow.AddMinutes(minutes)),
        AddedByMemberId: memberId);

    private static EntryLink UrlLink(string url, string label, Guid memberId, int minutes = 0) => new(
        Id: Guid.NewGuid(),
        Kind: EntryLinkKind.External,
        Provider: null,
        ItemId: null,
        Url: url,
        Label: label,
        AddedAt: Microseconds.Truncate(DateTimeOffset.UtcNow.AddMinutes(minutes)),
        AddedByMemberId: memberId);

    private async Task<EntryResponse> Entry(Users user, Guid campaignId, string name, EntryKind kind)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.PostEntry(campaignId, name, kind);
        entry.Should().Succeed();
        return entry.Value;
    }

    private async Task Add(Guid entryId, Guid actorMemberId, params EntryLink[] added)
        => await TestCampaign.Append(
            fixture,
            entryId,
            [.. added.Select(link => new EntryLinkAdded(Actor.Member(actorMemberId), link))]);

    private async Task<EntryLinkResponse[]> Read(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        return (await fixture.GetEntry(campaignId, entryId)).Value.Links;
    }

    [Fact]
    public async Task AnUnclaimedCharactersLinks_AreTheDmsOnly()
    {
        var campaign = await TestCampaign.Create(fixture, "Links: NPC");
        var eye = await Entry(Users.DM, campaign.Id, "The Eye", EntryKind.Character);
        await Add(eye.Id, campaign.DmMemberId, KbLink(Gremlin, campaign.DmMemberId));

        var dm = await Read(Users.DM, campaign.Id, eye.Id);
        dm.Should().ContainSingle().Which.Name.Should().Be("Test Gremlin");

        (await Read(Users.Player, campaign.Id, eye.Id)).Should().BeEmpty("an NPC's link to a monster is its stat block");
        (await Read(Users.Outsider, campaign.Id, eye.Id)).Should().BeEmpty();

        // And not in the bytes either: "none" and "not for you" have to look the same.
        fixture.LoginAsUser(Users.Player);
        var raw = await fixture.AlbaHost.Scenario(_ => _.Get.Url(EntryUrl(campaign.Id, eye.Id)));
        var body = await raw.ReadAsTextAsync();
        body.Should().NotContain("Test Gremlin").And.NotContain(Gremlin);
    }

    [Fact]
    public async Task AClaimedCharactersLinks_AreReadByEveryoneWhoSeesIt_AndGoAgainOnUnclaim()
    {
        var campaign = await TestCampaign.Create(fixture, "Links: PC");
        var thorin = await Entry(Users.DM, campaign.Id, "Thorin", EntryKind.Character);
        await Add(
            thorin.Id,
            campaign.DmMemberId,
            UrlLink("https://www.dndbeyond.com/characters/12345", "D&D Beyond sheet", campaign.PlayerMemberId));

        (await Read(Users.Player, campaign.Id, thorin.Id)).Should().BeEmpty("it is unclaimed until someone claims it");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryClaim(campaign.Id, thorin.Id, campaign.PlayerMemberId)).Should().Succeed();

        foreach (var who in new[] { Users.DM, Users.Player, Users.Outsider })
        {
            var links = await Read(who, campaign.Id, thorin.Id);
            var link = links.Should().ContainSingle().Subject;
            link.Kind.Should().Be(EntryLinkKind.External);
            link.Label.Should().Be("D&D Beyond sheet");
            link.Url.Should().Be("https://www.dndbeyond.com/characters/12345");
            link.Stale.Should().BeFalse("an external link is never stale");
            link.Provider.Should().BeNull();
        }

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryClaim(campaign.Id, thorin.Id, null)).Should().Succeed();
        (await Read(Users.Player, campaign.Id, thorin.Id)).Should().BeEmpty("unclaiming takes them away again");
    }

    [Theory]
    [InlineData(EntryKind.Place)]
    [InlineData(EntryKind.Faction)]
    [InlineData(EntryKind.Item)]
    [InlineData(EntryKind.Event)]
    public async Task EveryOtherKindsLinks_AreReadByEveryoneWhoSeesTheEntry(EntryKind kind)
    {
        var campaign = await TestCampaign.Create(fixture, $"Links: {kind}");
        var entry = await Entry(Users.DM, campaign.Id, $"Somewhere {kind}", kind);
        await Add(entry.Id, campaign.DmMemberId, KbLink(Gremlin, campaign.DmMemberId));

        foreach (var who in new[] { Users.DM, Users.Player, Users.Outsider })
        {
            (await Read(who, campaign.Id, entry.Id)).Should().ContainSingle("nothing is being hidden on a " + kind);
        }
    }

    [Fact]
    public async Task AKnowledgeBaseLink_ResolvesFromTheCorpus_AndIsOrderedByWhenItWasAdded()
    {
        var campaign = await TestCampaign.Create(fixture, "Links: resolved");
        var caves = await Entry(Users.DM, campaign.Id, "The Lint Caves", EntryKind.Place);
        // Appended newest first, so the ordering cannot come from the append order by accident.
        await Add(
            caves.Id,
            campaign.DmMemberId,
            KbLink(Zombie, campaign.DmMemberId, minutes: 5),
            KbLink(Gremlin, campaign.PlayerMemberId, minutes: -5));

        var links = await Read(Users.Player, campaign.Id, caves.Id);

        links.Select(l => l.ItemId).Should().Equal([Gremlin, Zombie], "oldest first");
        var gremlin = links[0];
        gremlin.Kind.Should().Be(EntryLinkKind.KnowledgeBase);
        gremlin.Provider.Should().Be(Provider);
        gremlin.ProviderLabel.Should().Be("5eTools");
        gremlin.Name.Should().Be("Test Gremlin");
        gremlin.Detail.Should().Be("CR 1/2 · Small Fey · TST");
        gremlin.BookTitle.Should().Be("Test Book of Beasts");
        gremlin.Url.Should().Be("https://5e.tools/bestiary.html#test%20gremlin_tst");
        gremlin.Label.Should().BeNull("a knowledge-base link has no label of its own");
        gremlin.HasStatBlock.Should().BeFalse("5eTools rows link out; the app draws no stat block for them");
        gremlin.Stale.Should().BeFalse();
        gremlin.AddedByMemberId.Should().Be(campaign.PlayerMemberId);
    }

    [Fact]
    public async Task ALinkToARowThatIsNotThere_ResolvesStale_WithNowhereToGo()
    {
        var campaign = await TestCampaign.Create(fixture, "Links: stale");
        var caves = await Entry(Users.DM, campaign.Id, "The Deep Caves", EntryKind.Place);
        await Add(caves.Id, campaign.DmMemberId, KbLink("monster_never-ingested_tst", campaign.DmMemberId));

        var link = (await Read(Users.Player, campaign.Id, caves.Id)).Should().ContainSingle().Subject;

        link.Stale.Should().BeTrue();
        link.Name.Should().BeNull();
        link.Detail.Should().BeNull();
        link.Url.Should().BeNull("there is nowhere to send the reader");
        link.ItemId.Should().Be("monster_never-ingested_tst", "it is still removable, so it still names itself");
        link.ProviderLabel.Should().Be("5eTools", "the provider is still registered even though its row is gone");
    }

    [Fact]
    public async Task ALinkThatSpellsItsProviderDifferently_StillResolves()
    {
        var campaign = await TestCampaign.Create(fixture, "Links: provider case");
        var caves = await Entry(Users.DM, campaign.Id, "The Shouty Caves", EntryKind.Place);
        // No endpoint writes this — POST stores the provider's own spelling — but the table's primary
        // key is case-sensitive, so the resolver has to fold the provider before it asks.
        await Add(caves.Id, campaign.DmMemberId, KbLink(Gremlin, campaign.DmMemberId, provider: "5eTOOLS"));

        var link = (await Read(Users.Player, campaign.Id, caves.Id)).Should().ContainSingle().Subject;

        link.Stale.Should().BeFalse();
        link.Name.Should().Be("Test Gremlin");
    }

    [Fact]
    public async Task ALinkToAProviderThatIsNotTableBacked_ResolvesFromThatProvider()
    {
        var campaign = await TestCampaign.Create(fixture, "Links: srd");
        var caves = await Entry(Users.DM, campaign.Id, "The Goblin Camp", EntryKind.Place);
        await Add(caves.Id, campaign.DmMemberId, KbLink("goblin-warrior", campaign.DmMemberId, provider: "srd52"));

        var link = (await Read(Users.Player, campaign.Id, caves.Id)).Should().ContainSingle().Subject;

        link.Stale.Should().BeFalse("the SRD's catalogue is in the assembly, not in knowledge_base_item");
        link.Name.Should().Be("Goblin Warrior");
        link.ProviderLabel.Should().Be("SRD 5.2");
        link.HasStatBlock.Should().BeTrue("the app draws the SRD's own stat blocks");
    }

    [Fact]
    public async Task History_ShowsTheAddAndTheRemove_AndOnlyToWhoMayReadTheLinks()
    {
        var campaign = await TestCampaign.Create(fixture, "Links: history");
        var eye = await Entry(Users.DM, campaign.Id, "The Watcher", EntryKind.Character);
        var link = KbLink(Gremlin, campaign.DmMemberId);
        await Add(eye.Id, campaign.DmMemberId, link);
        await TestCampaign.Append(fixture, eye.Id, new EntryLinkRemoved(Actor.Member(campaign.DmMemberId), link.Id));

        fixture.LoginAsUser(Users.DM);
        var dm = (await fixture.GetEntryHistory(campaign.Id, eye.Id)).Value.Items;
        var added = dm.Should().ContainSingle(i => i.Change.Type == EntryChangeType.LinkAdded).Subject;
        added.ActorMemberId.Should().Be(campaign.DmMemberId);
        added.Change.Link!.Name.Should().Be("Test Gremlin", "a removed link is still named in history");
        var removed = dm.Should().ContainSingle(i => i.Change.Type == EntryChangeType.LinkRemoved).Subject;
        removed.Change.Link!.Id.Should().Be(link.Id);

        fixture.LoginAsUser(Users.Player);
        var player = (await fixture.GetEntryHistory(campaign.Id, eye.Id)).Value.Items;
        player.Should().NotContain(
            i => i.Change.Type == EntryChangeType.LinkAdded || i.Change.Type == EntryChangeType.LinkRemoved,
            "a link a caller may not read is absent from their history, not greyed out");

        // Claimed, the same history reads both rows: the rule is the entry's shape now, as it is for
        // the stats and the source.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryClaim(campaign.Id, eye.Id, campaign.PlayerMemberId)).Should().Succeed();
        fixture.LoginAsUser(Users.Player);
        (await fixture.GetEntryHistory(campaign.Id, eye.Id)).Value.Items
            .Count(i => i.Change.Type is EntryChangeType.LinkAdded or EntryChangeType.LinkRemoved)
            .Should().Be(2);
    }

    [Fact]
    public async Task ALinkLeavesUpdatedAtAlone_AndARemovedLinkIsGoneFromTheEntry()
    {
        var campaign = await TestCampaign.Create(fixture, "Links: updatedAt");
        var caves = await Entry(Users.DM, campaign.Id, "Quiet Hollow", EntryKind.Place);
        var link = KbLink(Gremlin, campaign.DmMemberId);
        await Add(caves.Id, campaign.DmMemberId, link);

        fixture.LoginAsUser(Users.DM);
        var after = (await fixture.GetEntry(campaign.Id, caves.Id)).Value;
        after.UpdatedAt.Should().Be(caves.UpdatedAt, "the summary every viewer gets does not move");

        await TestCampaign.Append(fixture, caves.Id, new EntryLinkRemoved(Actor.Member(campaign.DmMemberId), link.Id));
        (await Read(Users.DM, campaign.Id, caves.Id)).Should().BeEmpty();
    }
}
