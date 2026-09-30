using Alba;

using FluentAssertions;

using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Reference;

/// <summary>
/// The knowledge base's browse endpoint (26e), against the seeded corpus: paging, each filter, the
/// facet counts, the caps, and who may ask.
/// </summary>
/// <remarks>
/// <para>
/// The empty-table case is in <c>ReferenceSearchTests</c>, because the fixture that seeds nothing is
/// the one every other Reference test already uses.
/// </para>
/// <para>
/// <b>Nothing here asserts a full alphabetical order.</b> <c>order by name</c> is the database's
/// collation, and whether "Test Gremlin, Understudy" sorts before or after "Test Gremlin Chief"
/// depends on whether punctuation counts — which is a property of the container's locale and not of
/// this endpoint. Paging is asserted by taking the pages apart and putting them back together, which
/// is what paging actually promises. A <c>q</c> ordering <i>is</i> asserted, because the match ladder
/// decides that one and not the collation.
/// </para>
/// </remarks>
public class GetKnowledgeBaseTests(KnowledgeBaseFixture fixture) : IClassFixture<KnowledgeBaseFixture>
{
    private const string Gremlin = "monster_test-gremlin_tst";
    private const string Zombie = "monster_test-gremlin-zombie_tsta";

    /// <summary>A campaign the caller is a member of, since membership is all the route is for.</summary>
    private async Task<Guid> Campaign(string name, Users who = Users.Player)
    {
        var campaign = await TestCampaign.Create(fixture, name, withSecondPlayer: false);
        fixture.LoginAsUser(who);
        return campaign.Id;
    }

    [Fact]
    public async Task Paging_AnswersOnePageAtATime_WithTheTotalOnEveryPage()
    {
        var campaign = await Campaign("KB: paging");

        var all = await fixture.GetKnowledgeBase(campaign, take: GetKnowledgeBase.MaxTake);
        all.Total.Should().Be(KnowledgeBaseCorpus.Count);
        all.Items.Should().HaveCount(KnowledgeBaseCorpus.Count);

        var first = await fixture.GetKnowledgeBase(campaign, take: 5);
        var second = await fixture.GetKnowledgeBase(campaign, skip: 5, take: 5);
        var rest = await fixture.GetKnowledgeBase(campaign, skip: 10, take: 5);
        var last = await fixture.GetKnowledgeBase(campaign, skip: 15, take: 5);

        first.Items.Should().HaveCount(5);
        second.Items.Should().HaveCount(5);
        rest.Items.Should().HaveCount(5);
        last.Items.Should().HaveCount(1, "sixteen rows in pages of five is three full pages and one row");
        new[] { first, second, rest, last }.Should().AllSatisfy(page =>
            page.Total.Should().Be(KnowledgeBaseCorpus.Count, "the total is of the filter, not of the page"));

        var paged = first.Items.Concat(second.Items).Concat(rest.Items).Concat(last.Items).ToList();
        paged.Select(i => i.Id).Should().OnlyHaveUniqueItems("no row is on two pages");
        paged.Select(i => i.Id).Should().Equal(all.Items.Select(i => i.Id), "the pages are the list, in order");
    }

    [Fact]
    public async Task Skip_PastTheEnd_IsAnEmptyPage_ThatStillKnowsTheTotal()
    {
        var campaign = await Campaign("KB: past the end");

        var page = await fixture.GetKnowledgeBase(campaign, skip: 500);

        page.Items.Should().BeEmpty();
        page.Total.Should().Be(KnowledgeBaseCorpus.Count, "a page past the end still counts the corpus");
        page.Facets.Categories.Should().NotBeEmpty("the facets are of the filter, not of the page");
    }

    [Fact]
    public async Task Take_IsCapped_AndSkipIsBounded()
    {
        var campaign = await Campaign("KB: caps");

        (await fixture.GetStatus(KnowledgeBaseUrl(campaign, take: GetKnowledgeBase.MaxTake)))
            .Should().Be(200, "fifty is the cap and not past it");
        (await fixture.GetStatus(KnowledgeBaseUrl(campaign, take: GetKnowledgeBase.MaxTake + 1)))
            .Should().Be(400, $"take is capped at {GetKnowledgeBase.MaxTake}");
        (await fixture.GetStatus(KnowledgeBaseUrl(campaign, take: 0))).Should().Be(400);
        (await fixture.GetStatus(KnowledgeBaseUrl(campaign, skip: -1))).Should().Be(400);
        (await fixture.GetStatus(KnowledgeBaseUrl(campaign, skip: GetKnowledgeBase.MaxSkip + 1)))
            .Should().Be(400, "an unbounded offset is a scan the caller chooses the length of");
        (await fixture.GetStatus(KnowledgeBaseUrl(campaign, q: new string('x', SearchQuery.MaxLength + 1))))
            .Should().Be(400);
        (await fixture.GetStatus(KnowledgeBaseUrl(campaign, category: null, book: new string('x', 41))))
            .Should().Be(400);

        // The default take, with no take at all: thirty, which is under the corpus of sixteen, so the
        // page is the whole corpus and the default is asserted by the cap test above rather than here.
        (await fixture.GetKnowledgeBase(campaign)).Items.Should().HaveCount(KnowledgeBaseCorpus.Count);
        GetKnowledgeBase.DefaultTake.Should().BeLessThanOrEqualTo(GetKnowledgeBase.MaxTake);
    }

    [Fact]
    public async Task EachFilter_NarrowsThePage()
    {
        var campaign = await Campaign("KB: filters");

        var spells = await fixture.GetKnowledgeBase(campaign, category: ReferenceCategory.Spell);
        spells.Total.Should().Be(3);
        spells.Items.Should().OnlyContain(i => i.Category == ReferenceCategory.Spell);

        var items = await fixture.GetKnowledgeBase(campaign, category: ReferenceCategory.Item);
        items.Total.Should().Be(3);

        var adventure = await fixture.GetKnowledgeBase(campaign, book: "TSTA");
        adventure.Total.Should().Be(1);
        adventure.Items.Single().Should().BeEquivalentTo(new KnowledgeBaseItemResponse
        {
            Provider = "5etools",
            ProviderLabel = "5eTools",
            Id = Zombie,
            Name = "Test Gremlin Zombie",
            Category = ReferenceCategory.Monster,
            Label = "CR 1/2 · Small Undead",
            Book = "TSTA",
            BookTitle = "Test Adventure in the Lint Caves",
            Page = 5,
            Url = "https://5e.tools/bestiary.html#test%20gremlin%20zombie_tsta",
            ImageUrl = null,
        });

        (await fixture.GetKnowledgeBase(campaign, provider: "5etools")).Total.Should().Be(KnowledgeBaseCorpus.Count);
        (await fixture.GetKnowledgeBase(campaign, provider: "srd52")).Total
            .Should().Be(0, "the SRD is bundled with the app, not ingested");

        var combined = await fixture.GetKnowledgeBase(campaign, category: ReferenceCategory.Monster, book: "TSTA");
        combined.Total.Should().Be(1);
        (await fixture.GetKnowledgeBase(campaign, category: ReferenceCategory.Spell, book: "TSTA")).Total
            .Should().Be(0, "the filters are an AND");

        (await fixture.GetStatus(KnowledgeBaseUrl(campaign) + "?category=Feat"))
            .Should().Be(400, "there are three categories and they are named, never numbered");
    }

    /// <summary>
    /// A facet drops its own filter and keeps the other, so its number is what choosing that value
    /// would show. Counting the filtered page instead would put the total beside whatever is chosen
    /// and a zero beside everything else, which is the one thing a filter count must not do.
    /// </summary>
    [Fact]
    public async Task TheFacets_CountWhatChoosingEachValueWouldShow()
    {
        var campaign = await Campaign("KB: facets");

        var all = await fixture.GetKnowledgeBase(campaign);
        all.Facets.Categories.Should().BeEquivalentTo(new[]
        {
            new KnowledgeBaseCategoryFacetResponse { Category = ReferenceCategory.Monster, Count = 10 },
            new KnowledgeBaseCategoryFacetResponse { Category = ReferenceCategory.Spell, Count = 3 },
            new KnowledgeBaseCategoryFacetResponse { Category = ReferenceCategory.Item, Count = 3 },
        });
        all.Facets.Books.Should().BeEquivalentTo(new[]
        {
            new KnowledgeBaseBookFacetResponse { Book = "TST", BookTitle = "Test Book of Beasts", Count = 15 },
            new KnowledgeBaseBookFacetResponse { Book = "TSTA", BookTitle = "Test Adventure in the Lint Caves", Count = 1 },
        });
        all.Facets.Categories.Sum(f => f.Count).Should().Be(all.Total);
        all.Facets.Books.Sum(f => f.Count).Should().Be(all.Total);

        var spells = await fixture.GetKnowledgeBase(campaign, category: ReferenceCategory.Spell);
        spells.Facets.Categories.Should().BeEquivalentTo(all.Facets.Categories,
            "the category counts drop the category filter, so the other chips still say what they hold");
        spells.Facets.Books.Should().BeEquivalentTo(new[]
        {
            new KnowledgeBaseBookFacetResponse { Book = "TST", BookTitle = "Test Book of Beasts", Count = 3 },
        }, "the book counts keep the category filter: TSTA has no spells, so it is not offered");

        var adventure = await fixture.GetKnowledgeBase(campaign, book: "TSTA");
        adventure.Facets.Categories.Should().BeEquivalentTo(new[]
        {
            new KnowledgeBaseCategoryFacetResponse { Category = ReferenceCategory.Monster, Count = 1 },
        }, "the category counts keep the book filter");
        adventure.Facets.Books.Should().BeEquivalentTo(all.Facets.Books, "the book counts drop the book filter");
    }

    /// <summary>
    /// <c>q</c> uses the same matcher as ⌘K: the rung first, then the similarity, then the shorter
    /// name. The four gremlins all tie at similarity 1, so the order is by length, and that is a
    /// property of the ladder rather than of the collation.
    /// </summary>
    [Fact]
    public async Task Q_MatchesOnTheSameLadderAsCommandK_AndTheFacetsFollowIt()
    {
        var campaign = await Campaign("KB: search");

        var gremlins = await fixture.GetKnowledgeBase(campaign, q: "gremlin");

        gremlins.Total.Should().Be(4);
        gremlins.Items.Select(i => i.Name).Should().Equal(
            "Test Gremlin", "Test Gremlin Chief", "Test Gremlin Zombie", "Test Gremlin, Understudy");
        gremlins.Facets.Categories.Should().BeEquivalentTo(new[]
        {
            new KnowledgeBaseCategoryFacetResponse { Category = ReferenceCategory.Monster, Count = 4 },
        }, "a facet counts what the search found, not the whole corpus");
        gremlins.Facets.Books.Should().BeEquivalentTo(new[]
        {
            new KnowledgeBaseBookFacetResponse { Book = "TST", BookTitle = "Test Book of Beasts", Count = 3 },
            new KnowledgeBaseBookFacetResponse { Book = "TSTA", BookTitle = "Test Adventure in the Lint Caves", Count = 1 },
        });

        (await fixture.GetKnowledgeBase(campaign, q: "test gremlin")).Items[0].Id
            .Should().Be(Gremlin, "an exact match is rung 0");
        (await fixture.GetKnowledgeBase(campaign, q: "sparkbrst")).Items.Select(i => i.Id)
            .Should().Contain("spell_test-sparkburst_tst", "the fuzzy rung");
        (await fixture.GetKnowledgeBase(campaign, q: "no such thing at all")).Total.Should().Be(0);
        (await fixture.GetKnowledgeBase(campaign, q: "   ")).Total
            .Should().Be(KnowledgeBaseCorpus.Count, "whitespace is no query, and no query is a plain list");

        var searchedAndFiltered = await fixture.GetKnowledgeBase(
            campaign, q: "gremlin", category: ReferenceCategory.Monster, book: "TSTA");
        searchedAndFiltered.Items.Single().Id.Should().Be(Zombie);

        // Paging a search pages the ranking, not a re-sort of the page.
        var page = await fixture.GetKnowledgeBase(campaign, q: "gremlin", skip: 1, take: 2);
        page.Items.Select(i => i.Name).Should().Equal("Test Gremlin Chief", "Test Gremlin Zombie");
        page.Total.Should().Be(4);
    }

    /// <summary>
    /// The campaign in the route is membership and nothing else, so a non-member is a 403 and an
    /// unknown campaign is a 404 — exactly as every other campaign-scoped endpoint answers. The rows
    /// themselves are the same for everyone, which is why a member of *any* campaign sees the same page.
    /// </summary>
    [Fact]
    public async Task ANonMember_Gets403_AndAnUnknownCampaign_Gets404()
    {
        var campaign = await Campaign("KB: membership");

        fixture.LoginAsUser(Users.Stranger);
        (await fixture.GetStatus(KnowledgeBaseUrl(campaign)))
            .Should().Be(403, "the content is global, but the page behind it is a campaign's");

        fixture.LoginAsUser(Users.DM);
        (await fixture.GetStatus(KnowledgeBaseUrl(Guid.NewGuid()))).Should().Be(404);

        foreach (var who in new[] { Users.DM, Users.Player })
        {
            fixture.LoginAsUser(who);
            var page = await fixture.GetKnowledgeBase(campaign);
            page.Total.Should().Be(KnowledgeBaseCorpus.Count, "reference content does not vary by viewer");
        }
    }

    [Fact]
    public async Task ThePage_IsCacheable_BecauseOnlyAnIngestChangesIt()
    {
        var campaign = await Campaign("KB: caching");

        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url(KnowledgeBaseUrl(campaign));
            _.StatusCodeShouldBe(200);
        });

        result.Context.Response.Headers.CacheControl.ToString().Should().Be(GetKnowledgeBase.CacheControl);
    }
}
