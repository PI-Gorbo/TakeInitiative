using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Reference;

/// <summary>
/// + Wiki, <c>POST entries/from-reference</c> (20b.5): the kind, the source and, for a DM, the
/// Stats, all in one save; a player's entry without Stats; the name rules and the 409 shared with
/// <c>POST entries</c>.
/// </summary>
public class EntryFromReferenceTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public EntryFromReferenceTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    public static string FromReferenceUrl(Guid campaignId) => $"/api/campaigns/{campaignId}/entries/from-reference";

    private async Task<EntryResponse> Add(Users who, Guid campaignId, string itemId, string? name = null, Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(who);
        var reply = await fixture.Call(HttpMethod.Post, FromReferenceUrl(campaignId),
            new { provider = "srd52", itemId, name, visibility = visibility.ToString() }).Ok();
        return reply.As<EntryResponse>();
    }

    private async Task<Reply> Refused(Users who, Guid campaignId, object body)
    {
        fixture.LoginAsUser(who);
        return await fixture.Call(HttpMethod.Post, FromReferenceUrl(campaignId), body);
    }

    [Fact]
    public async Task ADm_GetsACharacter_WithItsSourceAndStats_ThatRollsInACombat()
    {
        var campaign = await TestCampaign.Create(fixture, "From reference: DM");
        var mark = fixture.Hub.Messages.Count;

        var goblin = await Add(Users.DM, campaign.Id, "goblin-warrior", visibility: Visibility.DM);

        goblin.Name.Should().Be("Goblin Warrior");
        goblin.Kind.Should().Be(EntryKind.Character);
        goblin.Visibility.Should().Be(Visibility.DM);
        goblin.EditAccess.Should().Be(EditAccess.Anyone);
        goblin.Article.Blocks.Should().BeEmpty("the article starts empty (§11)");
        goblin.Stats.Should().Be(new StatsResponse { InitiativeRoll = "1d20+2", MaxHp = "3d6", Ac = 15 });
        goblin.Source.Should().BeEquivalentTo(new EntrySourceResponse
        {
            Provider = "srd52",
            ProviderLabel = "SRD 5.2",
            ExternalId = "goblin-warrior",
            Name = "Goblin Warrior",
            Url = "https://www.dndbeyond.com/srd",
            HasStatBlock = true,
        });

        // One save: the creation and the stats, nothing else.
        (await TestCampaign.EventsOf(fixture, goblin.Id)).Select(e => e.Data.GetType())
            .Should().Equal(typeof(EntryCreated), typeof(EntryStatsChanged));
        var pushed = fixture.Hub.Messages.Skip(mark).ToList();
        pushed.Select(m => m.Method).Should().Equal(CampaignHubMessages.EntryUpserted, CampaignHubMessages.EntryStatsChanged);
        pushed[1].Groups.Should().Equal(CampaignGroups.Member(campaign.DmMemberId));

        // @Goblin Warrior ×4: four goblins, each rolling its own HP from the hit dice.
        var combat = await fixture.CreateCombat(campaign.Id);
        var after = await fixture.AddAsDm(campaign.Id, combat.Id, new { entryId = goblin.Id, count = 4 });
        after.Combatants.Should().HaveCount(4).And.AllSatisfy(c =>
        {
            c.Hp.Should().BeInRange(3, 18);
            c.MaxHp.Should().Be(c.Hp);
            c.Ac.Should().Be(15);
            c.InitiativeRoll.Should().Be("1d20+2");
        });
    }

    [Fact]
    public async Task APlayer_GetsTheEntryWithoutStats_AndTheDmFillsThemAfterwards()
    {
        var campaign = await TestCampaign.Create(fixture, "From reference: player");
        var mark = fixture.Hub.Messages.Count;

        var owlbear = await Add(Users.Player, campaign.Id, "owlbear");

        owlbear.Name.Should().Be("Owlbear");
        owlbear.CreatorMemberId.Should().Be(campaign.PlayerMemberId);
        owlbear.Stats.Should().BeNull();
        owlbear.Source.Should().BeNull("an unclaimed Character's source is the DMs' (the Stats rule)");
        (await TestCampaign.EventsOf(fixture, owlbear.Id)).Should().ContainSingle();
        fixture.Hub.Messages.Skip(mark).Select(m => m.Method).Should().Equal(CampaignHubMessages.EntryUpserted);

        // The DM sees the source, and sets the stats the normal way.
        fixture.LoginAsUser(Users.DM);
        var dmView = (await fixture.GetEntry(campaign.Id, owlbear.Id)).Value;
        dmView.Source!.ExternalId.Should().Be("owlbear");
        dmView.Stats.Should().BeNull();
        var saved = await fixture.PutEntryStats(campaign.Id, owlbear.Id, "1d20+1", "7d10+21", 13);
        saved.Should().Succeed();
        saved.Value.Stats!.MaxHp.Should().Be("7d10+21");
        saved.Value.Source!.Name.Should().Be("Owlbear", "every write answers with the source too");
    }

    [Fact]
    public async Task ARenamedEntry_KeepsItsSource()
    {
        var campaign = await TestCampaign.Create(fixture, "From reference: rename");

        var goblin = await Add(Users.DM, campaign.Id, "goblin-warrior", name: "  Goblin  ");
        goblin.Name.Should().Be("Goblin");
        goblin.Source!.Name.Should().Be("Goblin Warrior");

        fixture.LoginAsUser(Users.DM);
        var renamed = await fixture.PutEntryName(campaign.Id, goblin.Id, "Grik");
        renamed.Value.Source!.ExternalId.Should().Be("goblin-warrior");
    }

    [Fact]
    public async Task ADuplicateName_IsA409_WithTheExistingId()
    {
        var campaign = await TestCampaign.Create(fixture, "From reference: duplicate");
        fixture.LoginAsUser(Users.DM);
        var existing = (await fixture.PostEntry(campaign.Id, "Goblin")).Value;

        var reply = await Refused(Users.DM, campaign.Id, new { provider = "srd52", itemId = "goblin-warrior", name = "goblin", visibility = "DM" });

        reply.Status.Should().Be(409);
        using var json = JsonDocument.Parse(reply.Body);
        json.RootElement.GetProperty("errors").GetProperty(PostEntry.ExistingEntryIdKey)[0].GetString()
            .Should().Be(existing.Id.ToString());

        // Without a name, the item's own name is checked.
        (await fixture.PostEntry(campaign.Id, "Owlbear")).Should().Succeed();
        (await Refused(Users.DM, campaign.Id, new { provider = "srd52", itemId = "owlbear", visibility = "DM" })).Status.Should().Be(409);
    }

    [Theory]
    [InlineData("{\"provider\":\"srd52\",\"itemId\":\"goblin-warrior\",\"visibility\":\"Nobody\"}", 400)]
    [InlineData("{\"provider\":\"srd52\",\"itemId\":\"goblin-warrior\",\"name\":\"   \",\"visibility\":\"DM\"}", 400)]
    [InlineData("{\"provider\":\"srd52\",\"itemId\":\"goblin-warrior\",\"name\":\"" + LongName + "\",\"visibility\":\"DM\"}", 400)]
    [InlineData("{\"provider\":\"srd52\",\"itemId\":\"\",\"visibility\":\"DM\"}", 400)]
    [InlineData("{\"provider\":\"srd52\",\"itemId\":\"not-a-monster\",\"visibility\":\"DM\"}", 404)]
    [InlineData("{\"provider\":\"srd51\",\"itemId\":\"goblin-warrior\",\"visibility\":\"DM\"}", 404)]
    public async Task ABadRequest_IsRefused(string body, int status)
    {
        var campaign = await TestCampaign.Create(fixture, $"From reference: bad {status} {body.GetHashCode()}");
        fixture.LoginAsUser(Users.DM);

        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Post.Text(body).ToUrl(FromReferenceUrl(campaign.Id));
            _.WithRequestHeader("Content-Type", "application/json");
            _.IgnoreStatusCode();
        });

        result.Context.Response.StatusCode.Should().Be(status);
        if (status == 404)
        {
            using var json = JsonDocument.Parse(await result.ReadAsTextAsync());
            json.RootElement.GetProperty("errors").TryGetProperty(PostEntryFromReference.ItemIdKey, out _).Should().BeTrue();
        }
        (await fixture.GetEntries(campaign.Id)).Value.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ANonMember_IsA403()
    {
        var campaign = await TestCampaign.Create(fixture, "From reference: stranger");
        (await Refused(Users.Stranger, campaign.Id, new { provider = "srd52", itemId = "owlbear", visibility = "Everyone" }))
            .Status.Should().Be(403);
    }

    private const string LongName =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
}
