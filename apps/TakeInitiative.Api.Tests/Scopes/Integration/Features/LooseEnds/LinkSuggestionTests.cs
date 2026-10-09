using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Features.Users;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.LooseEnds.LooseEndTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.LooseEnds;

/// <summary>The entry matcher, counting its calls, so a test can hold loose ends to one round trip.</summary>
public class CountingEntryMatcher : EntryMatcher
{
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public void Reset() => Interlocked.Exchange(ref calls, 0);

    public override Task<IReadOnlyList<IReadOnlyList<EntryMatch>>> MatchAsync(
        IQuerySession session, Guid campaignId, Member viewer, IReadOnlyList<string> spans, EntryMatchOptions options,
        CancellationToken ct, SearchConnection? connection = null)
    {
        Interlocked.Increment(ref calls);
        return base.MatchAsync(session, campaignId, viewer, spans, options, ct, connection);
    }
}

public class CountingMatcherFixture : AuthenticatedWebAppWithDatabaseFixture
{
    public CountingEntryMatcher Matcher { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
        => services.Replace(ServiceDescriptor.Singleton<EntryMatcher>(Matcher));
}

/// <summary>
/// Link suggestions (19b.2): the entry matcher's hits on an unlinked note's spans, by name or
/// alias, at most three a note, and one matcher round trip for every note in the list.
/// </summary>
public class LinkSuggestionTests(CountingMatcherFixture fixture) : IClassFixture<CountingMatcherFixture>
{
    [Fact]
    public async Task ALowerCaseAlias_SuggestsItsEntry_AtItsOffset()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest alias", withSecondPlayer: false);
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren Rockseeker");
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryAliases(campaign.Id, gundren.Id, "Gundren")).Should().Succeed();
        await fixture.Entry(Users.DM, campaign.Id, "Triboar Trail", kind: EntryKind.Place);

        var note = await fixture.Note(Users.Player, campaign.Id, "we met gundren on the road");
        var item = (await fixture.LooseEndsOf(Users.Player, campaign.Id)).Single();

        item.Note!.Id.Should().Be(note.Id);
        var suggestion = item.Suggestions.Should().ContainSingle().Subject;
        (suggestion.Start, suggestion.Length, suggestion.Text).Should().Be((7, 7, "gundren"));
        suggestion.Entry.Id.Should().Be(gundren.Id);
        suggestion.Entry.Name.Should().Be("Gundren Rockseeker");
        suggestion.Similarity.Should().BeGreaterThanOrEqualTo(0.6);
    }

    [Fact]
    public async Task ManyNotes_TakeOneMatcherRoundTrip_AndTheCountsNone()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest one trip", withSecondPlayer: false);
        var names = new[] { "Sildar", "Tharden", "Nundro", "Glasstaff", "Klarg", "Halia" };
        var entries = new List<EntryResponse>();
        foreach (var name in names)
        {
            entries.Add(await fixture.Entry(Users.DM, campaign.Id, name));
        }
        foreach (var name in names)
        {
            await fixture.Note(Users.Player, campaign.Id, $"Talked to {name} about the Redbrands.");
        }
        await fixture.ImageNote(Users.Player, campaign.Id, "Halia's map");

        fixture.Matcher.Reset();
        var items = await fixture.LooseEndsOf(Users.Player, campaign.Id);
        fixture.Matcher.Calls.Should().Be(1, "every note's spans go to the matcher together");

        items.Should().HaveCount(names.Length + 1);
        items.Where(i => i.Note!.Images.Length == 0).Select(i => i.Suggestions.Single().Entry.Name)
            .Should().BeEquivalentTo(names, "each note suggests the one entry it names");
        items.Single(i => i.Note!.Images.Length > 0).Suggestions.Select(s => s.Text).Should().Equal("Halia");

        fixture.Matcher.Reset();
        (await fixture.LooseEndCountsOf(Users.Player, campaign.Id)).Total.Should().Be(names.Length + 1);
        fixture.Matcher.Calls.Should().Be(0, "the counts never call the matcher");
    }

    [Fact]
    public async Task ANoteGetsAtMostThreeSuggestions_EachEntryOnce()
    {
        var campaign = await TestCampaign.Create(fixture, "Suggest three", withSecondPlayer: false);
        foreach (var name in new[] { "Sildar", "Tharden", "Nundro", "Klarg" })
        {
            await fixture.Entry(Users.DM, campaign.Id, name);
        }
        await fixture.Note(Users.Player, campaign.Id, "Sildar, Sildar, Tharden, Nundro and Klarg");

        var suggestions = (await fixture.LooseEndsOf(Users.Player, campaign.Id)).Single().Suggestions;
        suggestions.Should().HaveCount(3);
        suggestions.Select(s => s.Entry.Id).Should().OnlyHaveUniqueItems();
        suggestions.Select(s => s.Start).Should().BeInAscendingOrder();
    }
}
