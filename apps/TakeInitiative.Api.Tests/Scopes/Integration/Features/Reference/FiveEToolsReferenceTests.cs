using System.Text.Json;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Reference;

/// <summary>
/// The 5eTools provider (21b), against the synthetic <c>Fixtures/5etools-index.json</c>: its rows in
/// ⌘K after the SRD's, the item endpoint's summary with no stat block, + Wiki with the Stats rule,
/// and the leak test from 20b for a 5eTools source. The other Reference tests run with no index,
/// which is step 20's behaviour.
/// </summary>
public class FiveEToolsReferenceTests : IClassFixture<FiveEToolsFixture>
{
    private readonly FiveEToolsFixture fixture;

    public FiveEToolsReferenceTests(FiveEToolsFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    private const string Gremlin = "monster_test-gremlin_tst";
    private const string GremlinUrl = "https://5e.tools/bestiary.html#test%20gremlin_tst";

    private async Task<SearchResponse> Search(Guid campaignId, string q, int? take = null)
    {
        var response = await fixture.GetSearch(campaignId, q, "reference", take);
        response.Should().Succeed();
        return response.Value;
    }

    private async Task<EntryResponse> Add(Users who, Guid campaignId, string itemId, string? name = null, Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(who);
        var reply = await fixture.Call(HttpMethod.Post, EntryFromReferenceTests.FromReferenceUrl(campaignId),
            new { provider = "5etools", itemId, name, visibility = visibility.ToString() }).Ok();
        return reply.As<EntryResponse>();
    }

    [Fact]
    public void TheHost_LoggedHowManyItemsItLoaded()
        => fixture.Logs.Should().Contain(l => l.Category.EndsWith(nameof(FiveEToolsCatalog)) && l.Message.Contains("16 items"));

    [Fact]
    public async Task Search_GivesTheSrdFirst_ThenFiveEToolsRows_ThatLinkOut()
    {
        var campaign = await TestCampaign.Create(fixture, "5eTools: search", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        var hits = (await Search(campaign.Id, "goblin boss")).Section(SearchSectionKey.Reference);
        hits.Take(2).Select(h => (h.Reference!.Provider, h.Reference.Name))
            .Should().Equal(("srd52", "Goblin Boss"), ("5etools", "Goblin Boss"));

        var goblins = (await Search(campaign.Id, "goblin", take: 20)).Section(SearchSectionKey.Reference);
        goblins.Select(h => h.Reference!.Provider).Should().StartWith("srd52", "at the same rung the SRD comes first");
        goblins.Should().Contain(h => h.Reference!.Id == "monster_goblin-tinkerer_tst");

        var gremlin = (await Search(campaign.Id, "test gremlin")).Section(SearchSectionKey.Reference).First().Reference!;
        gremlin.Should().BeEquivalentTo(new SearchReferenceHit
        {
            Provider = "5etools",
            ProviderLabel = "5eTools",
            Id = Gremlin,
            Name = "Test Gremlin",
            Category = ReferenceCategory.Monster,
            Detail = "CR 1/2 · Small Fey · TST",
            Url = GremlinUrl,
            HasStatBlock = false,
            SuggestedKind = EntryKind.Character,
        });

        var spell = (await Search(campaign.Id, "sparkburst")).Section(SearchSectionKey.Reference).Single().Reference!;
        (spell.Category, spell.SuggestedKind, spell.HasStatBlock).Should().Be((ReferenceCategory.Spell, EntryKind.Other, false));
        var item = (await Search(campaign.Id, "satchel")).Section(SearchSectionKey.Reference).Single().Reference!;
        (item.Category, item.SuggestedKind).Should().Be((ReferenceCategory.Item, EntryKind.Item));
    }

    [Fact]
    public async Task TheItem_IsItsSummary_WithNoStatBlock()
    {
        fixture.LoginAsUser(Users.Stranger);
        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url($"/api/reference/5eTools/{Gremlin}");
            _.StatusCodeShouldBe(200);
        });
        result.Context.Response.Headers.CacheControl.ToString().Should().Be(GetReferenceItem.CacheControl);
        var body = await result.ReadAsTextAsync();
        body.Should().Contain("\"statBlock\":null");

        var item = JsonSerializer.Deserialize<ReferenceItemResponse>(body, Web)!;
        item.StatBlock.Should().BeNull();
        item.Summary.Should().BeEquivalentTo(new ReferenceSummaryResponse
        {
            Provider = "5etools",
            ProviderLabel = "5eTools",
            Id = Gremlin,
            Name = "Test Gremlin",
            Category = ReferenceCategory.Monster,
            Detail = "CR 1/2 · Small Fey · TST",
            Url = GremlinUrl,
            HasStatBlock = false,
            SuggestedKind = EntryKind.Character,
            Stats = new StatsResponse { InitiativeRoll = "1d20+2", MaxHp = "3d6+3", Ac = 15 },
        });
        item.Attribution.Should().BeEquivalentTo(new ReferenceAttributionResponse
        {
            Text = "Found in the 5eTools index. Opens 5etools in a new tab.",
            LicenseName = "",
            LicenseUrl = "",
            SourceUrl = "https://5e.tools/",
        });

        (await fixture.GetStatus("/api/reference/5etools/monster_nothing_tst")).Should().Be(404);
        (await fixture.GetStatus("/api/reference/5etools/goblin-warrior")).Should().Be(404, "an SRD id is not a 5eTools one");
    }

    [Fact]
    public async Task PlusWiki_GivesADmTheSourceAndStats_ThatRollInACombat()
    {
        var campaign = await TestCampaign.Create(fixture, "5eTools: DM");

        var eye = await Add(Users.DM, campaign.Id, Gremlin, name: "The Imp", visibility: Visibility.DM);

        eye.Name.Should().Be("The Imp");
        eye.Kind.Should().Be(EntryKind.Character);
        eye.Stats.Should().Be(new StatsResponse { InitiativeRoll = "1d20+2", MaxHp = "3d6+3", Ac = 15 });
        eye.Source.Should().BeEquivalentTo(new EntrySourceResponse
        {
            Provider = "5etools",
            ProviderLabel = "5eTools",
            ExternalId = Gremlin,
            Name = "Test Gremlin",
            Url = GremlinUrl,
            Detail = "TST p. 12",
            HasStatBlock = false,
        });

        var combat = await fixture.CreateCombat(campaign.Id);
        var after = await fixture.AddAsDm(campaign.Id, combat.Id, new { entryId = eye.Id, count = 1 });
        var combatant = after.Combatants.Should().ContainSingle().Subject;
        combatant.Hp.Should().BeInRange(6, 21);
        combatant.MaxHp.Should().Be(combatant.Hp);
        combatant.Ac.Should().Be(15);
        combatant.InitiativeRoll.Should().Be("1d20+2");
    }

    [Fact]
    public async Task PlusWiki_GivesAPlayerTheEntry_WithNeitherSourceNorStats()
    {
        var campaign = await TestCampaign.Create(fixture, "5eTools: player");

        var gremlin = await Add(Users.Player, campaign.Id, Gremlin);

        gremlin.Kind.Should().Be(EntryKind.Character);
        gremlin.Stats.Should().BeNull();
        gremlin.Source.Should().BeNull("an unclaimed Character's source is the DMs'");

        fixture.LoginAsUser(Users.DM);
        var dmView = (await fixture.GetEntry(campaign.Id, gremlin.Id)).Value;
        dmView.Source!.Url.Should().Be(GremlinUrl);
        dmView.Stats.Should().BeNull("+ Wiki does not bypass the Stats rule");
    }

    [Fact]
    public async Task ASpell_IsAnOtherEntry_AndAnItem_AnItemEntry_WithoutStats()
    {
        var campaign = await TestCampaign.Create(fixture, "5eTools: spell and item");

        var spell = await Add(Users.DM, campaign.Id, "spell_test-sparkburst_tst");
        spell.Kind.Should().Be(EntryKind.Other);
        spell.Stats.Should().BeNull();
        spell.Source!.Should().Match<EntrySourceResponse>(s =>
            s.Url == "https://5e.tools/spells.html#test%20sparkburst_tst" && s.Detail == "TST p. 50" && !s.HasStatBlock);

        var item = await Add(Users.Player, campaign.Id, "item_test-satchel-of-plenty_tst");
        item.Kind.Should().Be(EntryKind.Item);
        item.Stats.Should().BeNull();
        item.Source!.Name.Should().Be("Test Satchel of Plenty", "an Item's source is not a secret");
    }

    [Fact]
    public async Task APlayer_NeverSeesAnUnclaimedEntrysFiveEToolsSource()
    {
        var campaign = await TestCampaign.Create(fixture, "5eTools: leak");
        var mark = fixture.Hub.Messages.Count;
        var stranger = await Add(Users.DM, campaign.Id, Gremlin, name: "Mysterious Stranger");
        stranger.Source!.ExternalId.Should().Be(Gremlin, "the DM who made it reads it");
        var pushed = fixture.Hub.Messages.Skip(mark).ToList();

        string[] secrets = ["5etools", "5eTools", "5e.tools", "gremlin", "Gremlin", "TST p."];
        void ShouldNotLeak(string json, string what)
        {
            foreach (var secret in secrets)
            {
                json.Should().NotContain(secret, $"{what} must not name the source");
            }
            using var doc = JsonDocument.Parse(json);
            Keys(doc.RootElement).Should().NotContain("source", $"{what} has no source key at all");
        }

        foreach (var who in new[] { Users.Player, Users.Outsider })
        {
            fixture.LoginAsUser(who);
            ShouldNotLeak((await fixture.Call(HttpMethod.Get, EntryUrl(campaign.Id, stranger.Id)).Ok()).Body, "GET entry");
            ShouldNotLeak((await fixture.Call(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/entries").Ok()).Body, "GET entries");
            ShouldNotLeak((await fixture.Call(HttpMethod.Get, EntryUrl(campaign.Id, stranger.Id, "history")).Ok()).Body, "GET entry history");
            var search = (await fixture.Call(HttpMethod.Get, SearchUrl(campaign.Id, "mysterious stranger", "entries")).Ok()).Body;
            search.Should().Contain(stranger.Id.ToString(), "the control: the entry itself is found");
            ShouldNotLeak(search, "GET search");
        }

        var upserted = pushed.Should().ContainSingle(m => m.Method == CampaignHubMessages.EntryUpserted).Subject;
        ShouldNotLeak(JsonSerializer.Serialize(upserted.Payload, upserted.Payload!.GetType(), Web), "entryUpserted");
    }

    private static IEnumerable<string> Keys(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Keys(p.Value).Prepend(p.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Keys),
        _ => [],
    };
}
