using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Reference;
using TakeInitiative.Api.Features.Reference.KnowledgeBase;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Reference;

/// <summary>
/// ⌘K's Reference section (20b.1): after every campaign section, filled from the SRD for any
/// member, ranked on the same ladder as Entries, and absent for an <c>@</c> query and for one
/// character. Reference items have no visibility, so there is nothing to leak here; the link from
/// an entry to an item is <see cref="ReferenceLeakTests"/>' business.
/// </summary>
public class ReferenceSearchTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private async Task<SearchResponse> Search(Guid campaignId, string q, string? sections = null, int? take = null)
    {
        var response = await fixture.GetSearch(campaignId, q, sections, take);
        response.Should().Succeed();
        return response.Value;
    }

    [Fact]
    public async Task Goblin_GivesTheWikisGoblinFirst_ThenTheSrdsGoblins()
    {
        var campaign = await TestCampaign.Create(fixture, "Reference: goblin", withSecondPlayer: false);
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostEntry(campaign.Id, "Goblin")).Should().Succeed();

        var response = await Search(campaign.Id, "goblin");

        response.Sections.Select(s => s.Key).Should().Equal(SearchSectionKey.Entries, SearchSectionKey.Reference);
        response.Section(SearchSectionKey.Entries).Single().Entry!.Entry.Name.Should().Be("Goblin");
        var hits = response.Section(SearchSectionKey.Reference);
        hits.Should().AllSatisfy(h => h.Kind.Should().Be(SearchHitKind.Reference));
        // The three prefix matches tie on similarity, so the shorter name comes first (20a's notes).
        hits.Take(3).Select(h => h.Reference!.Name).Should().Equal("Goblin Boss", "Goblin Minion", "Goblin Warrior");

        var warrior = hits.Single(h => h.Reference!.Id == "goblin-warrior").Reference!;
        warrior.Should().BeEquivalentTo(new SearchReferenceHit
        {
            Provider = "srd52",
            ProviderLabel = "SRD 5.2",
            Id = "goblin-warrior",
            Name = "Goblin Warrior",
            Category = ReferenceCategory.Monster,
            Detail = warrior.Detail,
            Url = null,
            HasStatBlock = true,
            SuggestedKind = EntryKind.Character,
        });
        warrior.Detail.Should().Contain("CR 1/4");
    }

    [Fact]
    public async Task TheHit_HasNoStats()
    {
        var campaign = await TestCampaign.Create(fixture, "Reference: hit size", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        var raw = await fixture.AlbaHost.Scenario(_ => _.Get.Url(SearchUrl(campaign.Id, "goblin warrior", "reference")));
        var body = await raw.ReadAsTextAsync();

        body.Should().Contain("goblin-warrior");
        body.Should().NotContain("3d6").And.NotContain("initiativeRoll");
    }

    [Fact]
    public async Task SectionsTakeAndHasMore_Work()
    {
        var campaign = await TestCampaign.Create(fixture, "Reference: take", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        (await fixture.PostEntry(campaign.Id, "Goblin")).Should().Succeed();

        var one = await Search(campaign.Id, "goblin", sections: "reference", take: 1);
        one.Sections.Should().ContainSingle().Which.Should().Match<SearchSection>(s => s.Key == SearchSectionKey.Reference && s.HasMore);
        one.Section(SearchSectionKey.Reference).Single().Reference!.Name.Should().Be("Goblin Boss");

        var all = await Search(campaign.Id, "goblin boss", sections: "reference", take: 20);
        all.Sections.Single().HasMore.Should().BeFalse("fewer than 21 monsters are anything like \"goblin boss\"");
        all.Section(SearchSectionKey.Reference).First().Reference!.Id.Should().Be("goblin-boss");

        (await Search(campaign.Id, "goblin", sections: "entries,reference")).Sections.Select(s => s.Key)
            .Should().Equal(SearchSectionKey.Entries, SearchSectionKey.Reference);
        (await Search(campaign.Id, "goblin", sections: "entries"))
            .ShouldHaveNoSection(SearchSectionKey.Reference, "it was not asked for");
    }

    [Fact]
    public async Task AnAtQuery_AndOneCharacter_HaveNoReference()
    {
        var campaign = await TestCampaign.Create(fixture, "Reference: scope", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        (await Search(campaign.Id, "goblin")).ShouldHaveSection(SearchSectionKey.Reference, "the control");
        (await Search(campaign.Id, "@goblin")).ShouldHaveNoSection(SearchSectionKey.Reference, "an @ query is entries only");
        (await Search(campaign.Id, "@goblin", sections: "reference")).ShouldHaveNoSection(SearchSectionKey.Reference, "the prefix is the narrower instruction");
        (await Search(campaign.Id, "g")).ShouldHaveNoSection(SearchSectionKey.Reference, "one letter would match half the SRD");
        (await Search(campaign.Id, "gobln", sections: "reference")).Section(SearchSectionKey.Reference)
            .Should().Contain(h => h.Reference!.Id == "goblin-warrior", "the fuzzy rung");
    }

    [Fact]
    public async Task ACampaignWithNoEntries_StillGetsReference_ForEveryMember()
    {
        var campaign = await TestCampaign.Create(fixture, "Reference: empty", withSecondPlayer: false);

        foreach (var who in new[] { Users.DM, Users.Player })
        {
            fixture.LoginAsUser(who);
            var response = await Search(campaign.Id, "owlbear");
            response.Sections.Select(s => s.Key).Should().Equal(SearchSectionKey.Reference);
            response.Section(SearchSectionKey.Reference).First().Reference!.Id.Should().Be("owlbear");
        }

        fixture.LoginAsUser(Users.Stranger);
        (await fixture.GetStatus(SearchUrl(campaign.Id, "owlbear"))).Should().Be(403, "a non-member still gets nothing");
    }

    /// <summary>
    /// An empty <c>knowledge_base_item</c> behaves exactly as an unset <c>IndexPath</c> did (26d₂): no
    /// rows, no errors, and the Reference section is step 20's. Every fixture but
    /// <c>KnowledgeBaseFixture</c> leaves the table empty, which is also the state of a deployment
    /// nobody has run the ingest against — and the state 26e's browse endpoint is what finally lets the
    /// app talk about.
    /// </summary>
    [Fact]
    public async Task WithAnEmptyKnowledgeBase_TheSectionIsTheSrdsAlone()
    {
        var campaign = await TestCampaign.Create(fixture, "Reference: no 5eTools", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        (await Search(campaign.Id, "goblin", take: 20)).Section(SearchSectionKey.Reference)
            .Should().OnlyContain(h => h.Reference!.Provider == "srd52");
        (await Search(campaign.Id, "test gremlin", sections: "reference"))
            .ShouldHaveNoSection(SearchSectionKey.Reference, "an empty corpus has no 5eTools rows");

        // And the provider itself, so this is about the table being empty rather than about the
        // section's merge hiding it: no rows, no exception, and nothing logged as a failure.
        using var scope = fixture.AlbaHost.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<KnowledgeBaseReferenceProvider>();
        (await provider.Search("gremlin", 10, CancellationToken.None)).Should().BeEmpty();
        (await provider.Search("g", 10, CancellationToken.None)).Should().BeEmpty();
        (await provider.Find("monster_test-gremlin_tst", CancellationToken.None)).Should().BeNull();
        (await provider.Get("monster_test-gremlin_tst", CancellationToken.None)).Should().BeNull();
        fixture.Logs.Should().NotContain(l => l.Level >= LogLevel.Error && l.Message.Contains("knowledge_base_item"));
    }

    /// <summary>
    /// The browse endpoint on an empty table: a 200 with a total of zero and no facets, which is the
    /// "nothing ingested" state 26f's page reads. Not a 404 and not an error.
    /// </summary>
    [Fact]
    public async Task TheBrowseEndpoint_OnAnEmptyKnowledgeBase_IsAnEmptyPage()
    {
        var campaign = await TestCampaign.Create(fixture, "Reference: empty browse", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        var page = await fixture.GetKnowledgeBase(campaign.Id);

        page.Total.Should().Be(0);
        page.Items.Should().BeEmpty();
        page.Facets.Categories.Should().BeEmpty();
        page.Facets.Books.Should().BeEmpty();

        var searched = await fixture.GetKnowledgeBase(campaign.Id, q: "gremlin");
        (searched.Total, searched.Items.Length).Should().Be((0, 0));
    }

    [Fact]
    public async Task TheSectionName_IsAccepted()
    {
        var campaign = await TestCampaign.Create(fixture, "Reference: names", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        (await fixture.GetStatus(SearchUrl(campaign.Id, "owlbear", "Reference"))).Should().Be(200);
        (await fixture.GetStatus(SearchUrl(campaign.Id, "owlbear", "5"))).Should().Be(400, "sections are named, never numbered");
    }
}
