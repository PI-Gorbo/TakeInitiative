using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Features.Reference.KnowledgeBase;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using TakeInitiative.Utilities;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Reference;

/// <summary>
/// The knowledge-base reference provider (26d₂), against the synthetic corpus of
/// <see cref="KnowledgeBaseCorpus"/> ingested into <c>knowledge_base_item</c>: the provider's own
/// three members, its rows in ⌘K after the SRD's, the item endpoint's summary with no stat block,
/// + Wiki with the Stats rule, and the leak test from 20b for a 5eTools source.
/// </summary>
/// <remarks>
/// <para>
/// These are the tests that were <c>FiveEToolsCatalogTests</c> and <c>FiveEToolsReferenceTests</c>
/// before the corpus moved out of a file and into Postgres. Every assertion about what a row is worth
/// keeping has been kept, which is why the provider is exercised twice over: directly, in a scope off
/// the host, for the three interface members and the ranking; and through ⌘K and the item endpoint,
/// for the behaviour a user sees. The ones that went with the file — a relative <c>IndexPath</c>
/// resolved from the content root, and the six malformed-file cases — have nothing left to be about:
/// there is no file and no path, and what those cases guarded (an unknown category, a duplicate id, a
/// row with no name) is now the parser's and the primary key's business, asserted in
/// <c>packages/TakeInitiative.KnowledgeBase.Tests</c>.
/// </para>
/// <para>
/// The empty-table case — the old "no path or no file is off without an error" — is
/// <c>ReferenceSearchTests</c>' business, because every fixture but this one leaves the table empty.
/// </para>
/// </remarks>
public class KnowledgeBaseReferenceTests : IClassFixture<KnowledgeBaseFixture>
{
    private readonly KnowledgeBaseFixture fixture;

    public KnowledgeBaseReferenceTests(KnowledgeBaseFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    /// <summary>
    /// The provider itself, in a scope off the host, so a test can call the interface rather than
    /// reach it through ⌘K. It is scoped because it reads the request's Marten session.
    /// </summary>
    private static async Task InProvider(KnowledgeBaseFixture fixture, Func<KnowledgeBaseReferenceProvider, Task> assert)
    {
        using var scope = fixture.AlbaHost.Services.CreateScope();
        await assert(scope.ServiceProvider.GetRequiredService<KnowledgeBaseReferenceProvider>());
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

    /// <summary>
    /// The corpus is in the table, with every category in it — the old
    /// <c>TheIndex_Loads_WithEveryCategory</c>, asked of Postgres instead of of a file. The counts come
    /// from the browse endpoint's facets (26e), which is the only thing in the app that can answer
    /// "what is in the corpus" at all; that it can is the reason the corpus moved.
    /// </summary>
    [Fact]
    public async Task TheCorpus_IsInTheTable_WithEveryCategory()
    {
        var campaign = await TestCampaign.Create(fixture, "5eTools: corpus", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        var page = await fixture.GetKnowledgeBase(campaign.Id, take: GetKnowledgeBase.MaxTake);

        page.Total.Should().Be(KnowledgeBaseCorpus.Count);
        page.Facets.Categories.Should().BeEquivalentTo(new[]
        {
            new KnowledgeBaseCategoryFacetResponse { Category = ReferenceCategory.Monster, Count = 10 },
            new KnowledgeBaseCategoryFacetResponse { Category = ReferenceCategory.Spell, Count = 3 },
            new KnowledgeBaseCategoryFacetResponse { Category = ReferenceCategory.Item, Count = 3 },
        });
        page.Items.Should().OnlyContain(i => i.Provider == "5etools" && i.ProviderLabel == "5eTools");
        page.Items.Should().OnlyContain(i => i.Url.StartsWith(KnowledgeBaseReferenceProvider.BaseUrl));
        KnowledgeBaseReferenceProvider.TheAttribution.Should().Be(new ReferenceAttribution(
            "Found in the 5eTools index. Opens 5etools in a new tab.",
            "",
            "",
            "https://5e.tools/"));
        KnowledgeBaseReferenceProvider.TheAttribution.LicenseName.Should().BeEmpty(
            "5eTools is not a licence the app credits; the row only links out");
    }

    /// <summary>
    /// Every monster's stats survive the round trip through <c>jsonb</c> and still roll — the old
    /// <c>EveryMonstersStats_PassTheDiceChecker</c>, over the rows the provider reads back rather than
    /// over the ones a file held.
    /// </summary>
    [Fact]
    public async Task EveryMonstersStats_PassTheDiceChecker()
    {
        var roller = new DiceRoller(new Random(1234));

        await InProvider(fixture, async provider =>
        {
            var withStats = new List<ReferenceSummary>();
            foreach (var row in KnowledgeBaseCorpus.Rows)
            {
                var summary = await provider.Find(row.Id, CancellationToken.None);
                summary.Should().NotBeNull($"{row.Id} was ingested");
                if (summary!.Stats is not null) withStats.Add(summary);
            }

            withStats.Should().NotBeEmpty();
            withStats.Should().OnlyContain(s => s.Category == ReferenceCategory.Monster);
            withStats.Should().HaveCount(KnowledgeBaseCorpus.Rows.Count(r => r.Stats is not null));
            foreach (var summary in withStats)
            {
                var stats = summary.Stats!;
                foreach (var expression in new[] { stats.InitiativeRoll!, stats.MaxHp! })
                {
                    var check = roller.Check(expression);
                    check.IsSuccess.Should().BeTrue($"{summary.Id}: {expression} {(check.IsFailure ? check.Error : "")}");
                }
                stats.Ac.Should().BeInRange(1, Stats.AcMax);
            }
        });
    }

    /// <summary>
    /// A summary holds only stored columns, and the provider is search-only — the old
    /// <c>ASummary_HoldsOnlyIndexFields</c> and <c>TheProvider_IsSearchOnly</c> in one place, because
    /// they are now the same object.
    /// </summary>
    [Fact]
    public async Task TheProvider_IsSearchOnly_AndItsSummaryHoldsOnlyStoredColumns()
    {
        await InProvider(fixture, async provider =>
        {
            (provider.Key, provider.Label, provider.HasStatBlocks).Should().Be(("5etools", "5eTools", false));

            (await provider.Find(Gremlin, CancellationToken.None)).Should().Be(new ReferenceSummary(
                Provider: "5etools",
                Id: Gremlin,
                Name: "Test Gremlin",
                Category: ReferenceCategory.Monster,
                Detail: "CR 1/2 · Small Fey · TST",
                Url: GremlinUrl,
                SuggestedKind: EntryKind.Character,
                Stats: Stats.Of("1d20+2", "3d6+3", 15),
                Book: "TST p. 12",
                BookTitle: "Test Book of Beasts"));
            (await provider.Find("monster_test-gremlin-zombie_tsta", CancellationToken.None))!.BookTitle
                .Should().Be("Test Adventure in the Lint Caves", "the title comes from the row's source_title");

            (await provider.Find("monster_goblin-tinkerer_tst", CancellationToken.None))!.Stats!.InitiativeRoll
                .Should().Be("1d20-1");
            (await provider.Find("monster_test-mossback_tst", CancellationToken.None))!
                .Should().Match<ReferenceSummary>(s => s.Stats == null && s.Book == "TST");
            (await provider.Find("spell_test-sparkburst_tst", CancellationToken.None))!
                .Should().Match<ReferenceSummary>(s =>
                    s.Category == ReferenceCategory.Spell && s.SuggestedKind == EntryKind.Other && s.Stats == null
                    && s.Detail == "Level 3 Evocation · TST");
            (await provider.Find("item_test-satchel-of-plenty_tst", CancellationToken.None))!
                .Should().Match<ReferenceSummary>(s =>
                    s.Category == ReferenceCategory.Item && s.SuggestedKind == EntryKind.Item && s.Stats == null);
            (await provider.Find("goblin-warrior", CancellationToken.None))
                .Should().BeNull("an SRD id is not a 5eTools one");

            (await provider.Search("gremlin", 10, CancellationToken.None)).Select(m => m.Item.Name)
                .Should().StartWith("Test Gremlin");
            (await provider.Search("sparkbrst", 5, CancellationToken.None)).Select(m => m.Item.Id)
                .Should().Contain("spell_test-sparkburst_tst", "the fuzzy rung");
            (await provider.Search("", 10, CancellationToken.None)).Should().BeEmpty("an empty query matches nothing");
            (await provider.Search("gremlin", 0, CancellationToken.None)).Should().BeEmpty("take of zero is no rows");
            (await provider.Get(Gremlin, CancellationToken.None)).Should().BeNull("the app never shows 5eTools content");
        });
    }

    /// <summary>
    /// The rungs are <c>SearchSql.MatchCategory</c>'s, and an exact match scores 1, which is what lets
    /// the Reference section merge these rows with the SRD's ported-in-C# ones on one ordering.
    /// </summary>
    [Fact]
    public async Task TheRanking_IsTheSameLadderTheSrdIsScoredOn()
    {
        await InProvider(fixture, async provider =>
        {
            var exact = (await provider.Search("test gremlin", 5, CancellationToken.None))[0];
            (exact.Category, exact.Similarity).Should().Be((0, 1.0), "an exact match is rung 0 and similarity 1");

            (await provider.Search("test gremlin c", 5, CancellationToken.None))[0].Category
                .Should().Be(1, "a prefix is rung 1");
            (await provider.Search("gremlin", 5, CancellationToken.None))[0].Category
                .Should().Be(2, "a word prefix inside the name is rung 2");
            (await provider.Search("remlin", 5, CancellationToken.None))[0].Category
                .Should().Be(3, "a substring that starts mid-word is rung 3");
            var fuzzy = (await provider.Search("sparkbrst", 5, CancellationToken.None))[0];
            fuzzy.Category.Should().Be(4, "a near miss is rung 4");
            fuzzy.Similarity.Should().BeGreaterThanOrEqualTo(KnowledgeBaseQueries.MinSimilarity);

            (await provider.Search("gremlin", 10, CancellationToken.None))
                .Should().BeInAscendingOrder(m => m.Category)
                .And.OnlyContain(m => m.Similarity > 0);
            // Within a rung: the more similar first, then the shorter name — ReferenceMatcher's order.
            (await provider.Search("test gremlin", 10, CancellationToken.None))
                .Where(m => m.Category == 1)
                .Select(m => m.Item.Name)
                .Should().Equal("Test Gremlin Chief", "Test Gremlin Zombie", "Test Gremlin, Understudy");
        });
    }

    [Fact]
    public async Task Search_GivesTheSrdFirst_ThenTheKnowledgeBasesRows_ThatLinkOut()
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
            Book = "TST p. 12",
            BookTitle = "Test Book of Beasts",
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
            BookTitle = "Test Book of Beasts",
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
    public async Task APlayer_NeverSeesAnUnclaimedEntrys5eToolsSource()
    {
        var campaign = await TestCampaign.Create(fixture, "5eTools: leak");
        var mark = fixture.Hub.Messages.Count;
        var stranger = await Add(Users.DM, campaign.Id, Gremlin, name: "Mysterious Stranger");
        stranger.Source!.ExternalId.Should().Be(Gremlin, "the DM who made it reads it");
        var pushed = fixture.Hub.Messages.Skip(mark).ToList();

        string[] secrets = ["5etools", "5eTools", "5e.tools", "gremlin", "Gremlin", "TST p.", "Test Book of Beasts"];
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
