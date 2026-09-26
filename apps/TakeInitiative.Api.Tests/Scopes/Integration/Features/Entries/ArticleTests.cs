using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Editing an article (15e.3 and 15e.4): the merge with blocks the editor cannot see, the
/// per-viewer etag and its 409, races, limits and permissions. <see cref="Users.Player"/>
/// creates the entry, <see cref="Users.DM"/> writes the secret blocks, and
/// <see cref="Users.Outsider"/> is the other player.
/// </summary>
public class ArticleTests(InterferingSaveFixture fixture) : IClassFixture<InterferingSaveFixture>
{
    private IDocumentStore Store => fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();

    private async Task<(TestCampaign Campaign, EntryResponse Entry)> NewEntry(string name, Visibility visibility = Visibility.Everyone)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        fixture.LoginAsUser(Users.Player);
        var entry = await fixture.PostEntry(campaign.Id, "Gundren", EntryKind.Character, visibility);
        entry.Should().Succeed();
        return (campaign, entry.Value);
    }

    private async Task<EntryResponse> Get(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.GetEntry(campaignId, entryId);
        entry.Should().Succeed();
        return entry.Value;
    }

    private async Task<EntryResponse> Put(Users user, Guid campaignId, EntryResponse seen, params BlockEdit[] blocks)
    {
        fixture.LoginAsUser(user);
        var saved = await fixture.PutEntryArticle(campaignId, seen.Id, seen.Article.Etag, blocks);
        saved.Should().Succeed();
        return saved.Value;
    }

    private static string[] Texts(EntryResponse entry) => entry.Article.Blocks.Select(b => b.Text).ToArray();

    /// <summary>Gundren with "A", a DM secret "S" by the DM, then "B": the DM's view.</summary>
    private async Task<(TestCampaign Campaign, EntryResponse DmView)> WithDmSecret(string name)
    {
        var (campaign, entry) = await NewEntry(name);
        var dmView = await Put(Users.DM, campaign.Id, entry,
            new BlockEdit(null, "A"), new BlockEdit(null, "S", Visibility.DM), new BlockEdit(null, "B"));
        return (campaign, dmView);
    }

    [Fact]
    public async Task AnEdit_IsReadBack_WithItsOwnerAndTheCallersEtag()
    {
        var (campaign, entry) = await NewEntry("Article read back");
        entry.Article.Blocks.Should().BeEmpty();

        var saved = await Put(Users.Player, campaign.Id, entry,
            new BlockEdit(null, "Dwarf prospector."), new BlockEdit(null, "I owe him gold.", Visibility.Me));

        Texts(saved).Should().Equal("Dwarf prospector.", "I owe him gold.");
        saved.Article.Blocks.Should().AllSatisfy(b =>
        {
            b.OwnerMemberId.Should().Be(campaign.PlayerMemberId);
            b.Quote.Should().BeNull();
        });
        saved.Article.Etag.Should().NotBe(entry.Article.Etag);

        var read = await Get(Users.Player, campaign.Id, entry.Id);
        read.Article.Should().BeEquivalentTo(saved.Article);
        Texts(await Get(Users.DM, campaign.Id, entry.Id)).Should().Equal("Dwarf prospector.");
    }

    [Fact]
    public async Task AStaleEtag_IsA409_AndAppendsNothing()
    {
        var (campaign, entry) = await NewEntry("Article stale etag");
        await Put(Users.Player, campaign.Id, entry, new BlockEdit(null, "First"));
        var events = (await TestCampaign.EventsOf(fixture, entry.Id)).Count;

        fixture.LoginAsUser(Users.Outsider);
        var (status, body) = await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, entry.Id),
            ArticleBody(entry.Article.Etag, [new BlockEdit(null, "Second")]));

        status.Should().Be(409);
        JsonDocument.Parse(body).RootElement.GetProperty("errors").GetProperty(PutEntryArticle.EtagErrorKey)[0].GetString()
            .Should().Be(PutEntryArticle.ConflictMessage);
        (await TestCampaign.EventsOf(fixture, entry.Id)).Should().HaveCount(events);
    }

    [Fact]
    public async Task APlayersSave_KeepsADmSecretBlockTheyNeverSaw_InPlace()
    {
        var (campaign, dmView) = await WithDmSecret("Article keeps secret");

        var playerView = await Get(Users.Player, campaign.Id, dmView.Id);
        Texts(playerView).Should().Equal("A", "B");
        var a = playerView.Article.Blocks[0];
        var b = playerView.Article.Blocks[1];

        var saved = await Put(Users.Outsider, campaign.Id, await Get(Users.Outsider, campaign.Id, dmView.Id),
            BlockEdit.Change(a, "A2"), BlockEdit.Keep(b), new BlockEdit(null, "C"));

        Texts(saved).Should().Equal("A2", "B", "C");
        Texts(await Get(Users.DM, campaign.Id, dmView.Id)).Should().Equal("A2", "S", "B", "C");
    }

    [Fact]
    public async Task ADmsEditInsideASecretBlock_LeavesAPlayersEtag_SoTheirSaveStillGoesThrough()
    {
        var (campaign, dmView) = await WithDmSecret("Article secret edit etag");
        var playerView = await Get(Users.Player, campaign.Id, dmView.Id);

        var secret = dmView.Article.Blocks[1];
        await Put(Users.DM, campaign.Id, dmView,
            BlockEdit.Keep(dmView.Article.Blocks[0]), BlockEdit.Change(secret, "S2"), BlockEdit.Keep(dmView.Article.Blocks[2]));

        (await Get(Users.Player, campaign.Id, dmView.Id)).Article.Etag.Should().Be(playerView.Article.Etag);
        var saved = await Put(Users.Player, campaign.Id, playerView, BlockEdit.Change(playerView.Article.Blocks[0], "A2"), BlockEdit.Keep(playerView.Article.Blocks[1]));

        Texts(saved).Should().Equal("A2", "B");
        Texts(await Get(Users.DM, campaign.Id, dmView.Id)).Should().Equal("A2", "S2", "B");
    }

    [Fact]
    public async Task APlayersConflict_LeaksNoSecretBlock()
    {
        var (campaign, dmView) = await WithDmSecret("Article conflict leaks nothing");
        var stale = await Get(Users.Outsider, campaign.Id, dmView.Id);
        await Put(Users.DM, campaign.Id, dmView, BlockEdit.Change(dmView.Article.Blocks[0], "A2"), BlockEdit.Keep(dmView.Article.Blocks[1]), BlockEdit.Keep(dmView.Article.Blocks[2]));

        fixture.LoginAsUser(Users.Outsider);
        var (status, body) = await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, dmView.Id),
            ArticleBody(stale.Article.Etag, stale.Article.Blocks.Select(BlockEdit.Keep)));

        status.Should().Be(409);
        body.Should().NotContain(dmView.Article.Blocks[1].Id.ToString()).And.NotContain("\"S\"");
    }

    [Fact]
    public async Task AnUnknownIdAndAHiddenId_AreTheSame400()
    {
        var (campaign, dmView) = await WithDmSecret("Article hidden id");
        var playerView = await Get(Users.Player, campaign.Id, dmView.Id);
        var secretId = dmView.Article.Blocks[1].Id;

        async Task<(int, string)> Try(Guid id)
        {
            var (status, body) = await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, dmView.Id),
                ArticleBody(playerView.Article.Etag, [new BlockEdit(id, "I saw it")]));
            return (status, body.Replace(id.ToString(), "<id>"));
        }

        fixture.LoginAsUser(Users.Player);
        var hidden = await Try(secretId);
        var unknown = await Try(Guid.NewGuid());

        hidden.Item1.Should().Be(400);
        hidden.Should().Be(unknown);
    }

    [Fact]
    public async Task OnlyTheOwnerAndTheDms_ChangeABlocksVisibility()
    {
        var (campaign, entry) = await NewEntry("Article block visibility");
        var saved = await Put(Users.Player, campaign.Id, entry, new BlockEdit(null, "Mine"));
        var block = saved.Article.Blocks.Single();

        fixture.LoginAsUser(Users.Outsider);
        var (status, _) = await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, entry.Id),
            ArticleBody(saved.Article.Etag, [new BlockEdit(block.Id, block.Text, Visibility.DM)]));
        status.Should().Be(403);

        var byDm = await Put(Users.DM, campaign.Id, await Get(Users.DM, campaign.Id, entry.Id), new BlockEdit(block.Id, "Mine", Visibility.DM));
        byDm.Article.Blocks.Single().Visibility.Should().Be(Visibility.DM);
        byDm.Article.Blocks.Single().OwnerMemberId.Should().Be(campaign.PlayerMemberId);
        (await Get(Users.Outsider, campaign.Id, entry.Id)).Article.Blocks.Should().BeEmpty();

        var byOwner = await Put(Users.Player, campaign.Id, await Get(Users.Player, campaign.Id, entry.Id), new BlockEdit(block.Id, "Mine", Visibility.Everyone));
        byOwner.Article.Blocks.Single().Visibility.Should().Be(Visibility.Everyone);
    }

    [Fact]
    public async Task TheLimits_CountOnlyWhatTheCallerCanSee()
    {
        var (campaign, entry) = await NewEntry("Article limits");
        fixture.LoginAsUser(Users.Player);
        var url = ArticleUrl(campaign.Id, entry.Id);

        (await fixture.Send(HttpMethod.Put, url, ArticleBody(entry.Article.Etag,
            Enumerable.Range(0, Article.MaxBlocks + 1).Select(i => new BlockEdit(null, $"B{i}"))))).Status.Should().Be(400);
        (await fixture.Send(HttpMethod.Put, url, ArticleBody(entry.Article.Etag,
            [new BlockEdit(null, new string('a', 30_000)), new BlockEdit(null, new string('b', 20_001))]))).Status.Should().Be(400);

        // A DM secret the player cannot see does not count against the player.
        await Put(Users.DM, campaign.Id, entry, new BlockEdit(null, new string('s', 40_000), Visibility.DM));
        var playerView = await Get(Users.Player, campaign.Id, entry.Id);
        var full = await Put(Users.Player, campaign.Id, playerView,
            [.. Enumerable.Range(0, Article.MaxBlocks - 1).Select(i => new BlockEdit(null, $"B{i}")), new BlockEdit(null, new string('p', 20_000))]);

        full.Article.Blocks.Should().HaveCount(Article.MaxBlocks);
        (await Get(Users.DM, campaign.Id, entry.Id)).Article.Blocks.Should().HaveCount(Article.MaxBlocks + 1);
    }

    [Fact]
    public async Task Permission_FollowsEditAccess_AndAHiddenEntryIsA404()
    {
        var (campaign, entry) = await NewEntry("Article permission");
        (await fixture.PutEntryEditAccess(campaign.Id, entry.Id, EditAccess.OnlyMe)).Should().Succeed();

        fixture.LoginAsUser(Users.Outsider);
        (await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, entry.Id), ArticleBody(entry.Article.Etag, [new BlockEdit(null, "X")])))
            .Status.Should().Be(403);
        var byDm = await Put(Users.DM, campaign.Id, entry, new BlockEdit(null, "DMs can always edit"));
        var byCreator = await Put(Users.Player, campaign.Id, byDm, [.. byDm.Article.Blocks.Select(BlockEdit.Keep), new BlockEdit(null, "So can the creator")]);

        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryEditAccess(campaign.Id, entry.Id, EditAccess.Anyone)).Should().Succeed();
        await Put(Users.Outsider, campaign.Id, byCreator, new BlockEdit(null, "Now anyone can"));

        fixture.LoginAsUser(Users.Player);
        var hidden = (await fixture.PostEntry(campaign.Id, "Secret plan", EntryKind.Other, Visibility.Me)).Value;
        fixture.LoginAsUser(Users.DM);
        (await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, hidden.Id), ArticleBody(hidden.Article.Etag, [new BlockEdit(null, "X")])))
            .Status.Should().Be(404);
    }

    [Fact]
    public async Task AnUnchangedView_AppendsNothing_AndPushesNothing()
    {
        var (campaign, dmView) = await WithDmSecret("Article unchanged");
        var events = (await TestCampaign.EventsOf(fixture, dmView.Id)).Count;
        var mark = fixture.Hub.Messages.Count;

        var playerView = await Get(Users.Player, campaign.Id, dmView.Id);
        var saved = await Put(Users.Player, campaign.Id, playerView, [.. playerView.Article.Blocks.Select(b => new BlockEdit(b.Id, $"  {b.Text}\n", b.Visibility))]);

        saved.Article.Etag.Should().Be(playerView.Article.Etag);
        (await TestCampaign.EventsOf(fixture, dmView.Id)).Should().HaveCount(events);
        fixture.Hub.Messages.Skip(mark).Should().BeEmpty();
    }

    [Fact]
    public async Task ArticleEdits_LeaveUpdatedAtAlone_AndCarryAnActorAndACorrelationId()
    {
        var (campaign, dmView) = await WithDmSecret("Article updatedAt");
        var before = await Get(Users.Player, campaign.Id, dmView.Id);

        await Put(Users.DM, campaign.Id, dmView, [.. dmView.Article.Blocks.Select(b => BlockEdit.Change(b, b.Text + "!"))]);

        (await Get(Users.Player, campaign.Id, dmView.Id)).UpdatedAt.Should().Be(before.UpdatedAt,
            "a moving updatedAt would tell players that a secret block changed");
        var edits = (await TestCampaign.EventsOf(fixture, dmView.Id)).Where(e => e.Data is EntryArticleEdited).ToList();
        edits.Should().HaveCount(2).And.AllSatisfy(e =>
        {
            ((EntryArticleEdited)e.Data).Actor.MemberId.Should().Be(campaign.DmMemberId);
            e.CorrelationId.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task NewEntries_AreCreatedWithTheNarrowestVisibility_InTheSameTransaction()
    {
        var (campaign, entry) = await NewEntry("Article new entries");
        var klarg = new NewEntry(Guid.NewGuid(), "Klarg");
        var phandalin = new NewEntry(Guid.NewGuid(), "Phandalin", EntryKind.Place);

        fixture.LoginAsUser(Users.Player);
        var saved = await fixture.PutEntryArticle(campaign.Id, entry.Id, entry.Article.Etag,
            [new BlockEdit(null, $"Hired us to reach {phandalin.Mention}."), new BlockEdit(null, $"Owes {klarg.Mention}.", Visibility.DM)],
            klarg, phandalin);
        saved.Should().Succeed();

        (await fixture.GetEntry(campaign.Id, phandalin.Id)).Value.Visibility.Should().Be(Visibility.Everyone);
        var created = (await fixture.GetEntry(campaign.Id, klarg.Id)).Value;
        created.Visibility.Should().Be(Visibility.DM);
        created.CreatorMemberId.Should().Be(campaign.PlayerMemberId);
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.GetEntry(campaign.Id, klarg.Id)).Should().Fail();

        var edit = (await TestCampaign.EventsOf(fixture, entry.Id)).Last();
        (await TestCampaign.EventsOf(fixture, klarg.Id)).Single().CorrelationId.Should().Be(edit.CorrelationId);
    }

    [Fact]
    public async Task NewEntries_UseTheNoteErrorKeys()
    {
        var (campaign, entry) = await NewEntry("Article new entry errors");
        var url = ArticleUrl(campaign.Id, entry.Id);
        fixture.LoginAsUser(Users.Player);

        static JsonElement Errors(string body) => JsonDocument.Parse(body).RootElement.GetProperty("errors");

        var unmentioned = new NewEntry(Guid.NewGuid(), "Klarg");
        var (s1, b1) = await fixture.Send(HttpMethod.Put, url, ArticleBody(entry.Article.Etag, [new BlockEdit(null, "No mention")], unmentioned));
        s1.Should().Be(400);
        Errors(b1).TryGetProperty(NewEntries.ErrorKey, out _).Should().BeTrue();

        var existing = (await fixture.PostEntry(campaign.Id, "Sildar")).Value;
        var fresh = await Get(Users.Player, campaign.Id, entry.Id);
        var duplicate = new NewEntry(Guid.NewGuid(), "sildar");
        var (s2, b2) = await fixture.Send(HttpMethod.Put, url, ArticleBody(fresh.Article.Etag, [new BlockEdit(null, duplicate.Mention)], duplicate));
        s2.Should().Be(409);
        Errors(b2).GetProperty(PostEntry.ExistingEntryIdKey)[0].GetString().Should().Be(existing.Id.ToString());
        Errors(b2).GetProperty(NewEntries.NewEntryIdKey)[0].GetString().Should().Be(duplicate.Id.ToString());

        var (s3, b3) = await fixture.Send(HttpMethod.Put, url, ArticleBody(fresh.Article.Etag, [new BlockEdit(null, $"@[Sildar](entry:{existing.Id})")],
            new NewEntry(existing.Id, "Sildar Two")));
        s3.Should().Be(409);
        Errors(b3).GetProperty(NewEntries.AlreadyCreatedEntryIdKey)[0].GetString().Should().Be(existing.Id.ToString());
    }

    [Fact]
    public async Task TwoRacingSaves_FromOneView_OneWinsAndTheOtherIsA409()
    {
        var (campaign, entry) = await NewEntry("Article race");
        fixture.LoginAsUser(Users.Player);

        var results = await Task.WhenAll(
            fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, entry.Id), ArticleBody(entry.Article.Etag, [new BlockEdit(null, "One")])),
            fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, entry.Id), ArticleBody(entry.Article.Etag, [new BlockEdit(null, "Two")])));

        results.Select(r => r.Status).Should().BeEquivalentTo([200, 409]);
        var winner = JsonSerializer.Deserialize<EntryResponse>(results.Single(r => r.Status == 200).Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Texts(await Get(Users.Player, campaign.Id, entry.Id)).Should().Equal(winner.Article.Blocks.Single().Text);
    }

    /// <summary>A write that lands between the endpoint's read and its append: the DM edits their own secret block.</summary>
    private Func<IDocumentStore, Task> DmEditsSecret(Guid entryId, Guid dmMemberId, string text) => async store =>
    {
        await using var session = store.LightweightSession();
        var current = (await session.LoadAsync<Entry>(entryId))!;
        var blocks = current.Article.Blocks.Select(b => b.Visibility == Visibility.DM ? b with { Text = text } : b).ToList();
        session.Events.Append(entryId, new EntryArticleEdited(Actor.Member(dmMemberId), blocks));
        await session.SaveChangesAsync();
    };

    [Fact]
    public async Task LosingTheRaceToAWriteTheCallerCannotSee_ReMergesAndSucceeds()
    {
        var (campaign, dmView) = await WithDmSecret("Article race hidden");
        var playerView = await Get(Users.Player, campaign.Id, dmView.Id);

        fixture.Interference.BeforeNextSave(DmEditsSecret(dmView.Id, campaign.DmMemberId, "S raced"));
        var saved = await Put(Users.Player, campaign.Id, playerView, BlockEdit.Change(playerView.Article.Blocks[0], "A2"), BlockEdit.Keep(playerView.Article.Blocks[1]));

        fixture.Interference.Pending.Should().Be(0);
        Texts(saved).Should().Equal("A2", "B");
        Texts(await Get(Users.DM, campaign.Id, dmView.Id)).Should().Equal("A2", "S raced", "B");
    }

    [Fact]
    public async Task LosingTheRaceToAWriteTheCallerCanSee_IsA409()
    {
        var (campaign, dmView) = await WithDmSecret("Article race visible");

        fixture.Interference.BeforeNextSave(DmEditsSecret(dmView.Id, campaign.DmMemberId, "S raced"));
        fixture.LoginAsUser(Users.DM);
        var (status, _) = await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, dmView.Id),
            ArticleBody(dmView.Article.Etag, [BlockEdit.Change(dmView.Article.Blocks[0], "A2"), BlockEdit.Keep(dmView.Article.Blocks[1]), BlockEdit.Keep(dmView.Article.Blocks[2])]));

        status.Should().Be(409);
        Texts(await Get(Users.DM, campaign.Id, dmView.Id)).Should().Equal("A", "S raced", "B");
    }

    [Fact]
    public async Task LosingTheRaceTwice_IsA409()
    {
        var (campaign, dmView) = await WithDmSecret("Article race twice");
        var playerView = await Get(Users.Player, campaign.Id, dmView.Id);

        fixture.Interference.BeforeNextSave(DmEditsSecret(dmView.Id, campaign.DmMemberId, "S1"));
        fixture.Interference.BeforeNextSave(DmEditsSecret(dmView.Id, campaign.DmMemberId, "S2"));
        fixture.LoginAsUser(Users.Player);
        var (status, _) = await fixture.Send(HttpMethod.Put, ArticleUrl(campaign.Id, dmView.Id),
            ArticleBody(playerView.Article.Etag, [BlockEdit.Change(playerView.Article.Blocks[0], "A2"), BlockEdit.Keep(playerView.Article.Blocks[1])]));

        status.Should().Be(409);
        Texts(await Get(Users.DM, campaign.Id, dmView.Id)).Should().Equal("A", "S2", "B");
    }
}
