using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// The entry matcher (17a.9, §11a) on its own, not through the endpoint: many spans in one round
/// trip, the similarity threshold, the viewer's visibility, and merged entries. ⌘K's Entries
/// section, loose ends (19) and suggestions (23) all go through this, so it is tested as the
/// service it is.
/// </summary>
public class EntryMatcherTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>, IAsyncLifetime
{
    private readonly EntryMatcher _matcher = new();
    private TestCampaign _campaign = null!;
    private Member _dm = null!;
    private Member _player = null!;

    private Guid _gundren;
    private Guid _sildar;
    private Guid _iarno;
    private Guid _agatha;
    private Guid _klarg;

    private IDocumentStore Store => fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();

    public async Task InitializeAsync()
    {
        _campaign = await TestCampaign.Create(fixture, "Entry matcher", withSecondPlayer: false);

        fixture.LoginAsUser(Users.Player);
        _gundren = await Entry("Gundren Rockseeker", Visibility.Everyone, "Rockseeker");
        _sildar = await Entry("Sildar Hallwinter");
        _klarg = await Entry("Klarg");
        await Entry("Redbrand Ruffian of the Alley");
        await Entry("Redbrand Ruffian of the Gate");

        fixture.LoginAsUser(Users.DM);
        _iarno = await Entry("Iarno Albrek", Visibility.DM, "Glasstaff");
        _agatha = await Entry("Agatha", Visibility.Me);

        // Klarg is merged into Gundren, so its name is Gundren's alias (15g) and it is listed
        // nowhere itself.
        (await fixture.PostEntryMerge(_campaign.Id, _klarg, _gundren)).Should().Succeed();

        await using var session = Store.QuerySession();
        var campaign = (await session.LoadAsync<Api.Features.Campaigns.Campaign>(_campaign.Id))!;
        _dm = campaign.MemberById(_campaign.DmMemberId)!;
        _player = campaign.MemberById(_campaign.PlayerMemberId)!;
        _dm.Role.Should().Be(Role.DM);
        _player.Role.Should().Be(Role.Player);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> Entry(string name, Visibility visibility = Visibility.Everyone, params string[] aliases)
    {
        var entry = await fixture.PostEntry(_campaign.Id, name, EntryKind.Character, visibility);
        entry.Should().Succeed();
        if (aliases.Length > 0)
        {
            (await fixture.PutEntryAliases(_campaign.Id, entry.Value.Id, aliases)).Should().Succeed();
        }
        return entry.Value.Id;
    }

    private async Task<IReadOnlyList<IReadOnlyList<EntryMatch>>> Match(
        Member viewer, IReadOnlyList<string> spans, EntryMatchOptions? options = null)
    {
        await using var session = Store.QuerySession();
        return await _matcher.MatchAsync(
            session, _campaign.Id, viewer, spans, options ?? EntryMatchOptions.Default, default);
    }

    [Fact]
    public async Task EverySpan_IsAnsweredInOrder_InOneCall()
    {
        var results = await Match(_player, ["gundren", "sildar", "nothinghere", "   ", ""]);

        results.Should().HaveCount(5, "one list per span, in the order they were given");
        results[0].Should().ContainSingle().Which.EntryId.Should().Be(_gundren);
        results[1].Should().ContainSingle().Which.EntryId.Should().Be(_sildar);
        results[2].Should().BeEmpty("no name is close to it");
        results[3].Should().BeEmpty("a blank span matches nothing, and the ordinals still line up");
        results[4].Should().BeEmpty();
    }

    [Fact]
    public async Task AnEntryThatMatchesBothItsNameAndAnAlias_ComesBackOnce_OnItsBetterMatch()
    {
        // "rockseeker" is exactly the alias and a word of the name: one row, the better rung.
        var matches = (await Match(_player, ["rockseeker"]))[0];

        matches.Should().ContainSingle();
        matches[0].EntryId.Should().Be(_gundren);
        matches[0].Category.Should().Be(0);
        matches[0].IsAlias.Should().BeTrue();
        matches[0].MatchedName.Should().Be("Rockseeker", "the name or alias as it is stored, not folded");
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(0.7, true)]
    [InlineData(0.8, false)]
    public async Task TheThreshold_DecidesWhetherATypoMatches(double minSimilarity, bool expected)
    {
        // word_similarity('gundrn', 'gundren rockseeker') is about 0.71.
        var matches = (await Match(_player, ["gundrn"], EntryMatchOptions.Default with { MinSimilarity = minSimilarity }))[0];

        matches.Any(m => m.EntryId == _gundren).Should().Be(expected);
        if (expected)
        {
            var match = matches.Single(m => m.EntryId == _gundren);
            match.Category.Should().Be(4, "a typo is the fuzzy rung");
            match.Similarity.Should().BeApproximately(0.71, 0.02);
        }
    }

    [Fact]
    public async Task AShortSpan_NeverMatchesFuzzily()
    {
        // Under three characters word_similarity matches nearly everything, so the fuzzy rung is
        // off: "kl" still finds Klarg by prefix, but "xz" finds nothing at all.
        (await Match(_player, ["si"]))[0].Should().ContainSingle().Which.Category.Should().BeLessThanOrEqualTo(1);
        (await Match(_player, ["xz"]))[0].Should().BeEmpty();
    }

    [Fact]
    public async Task FuzzyOnly_LeavesTheExactAndPrefixRungsOut()
    {
        (await Match(_player, ["gundren"], EntryMatchOptions.Default with { FuzzyOnly = true }))[0]
            .Should().BeEmpty("an exact match is not a fuzzy one, and the caller found it already");

        (await Match(_player, ["gundrn"], EntryMatchOptions.Default with { FuzzyOnly = true }))[0]
            .Should().ContainSingle().Which.Category.Should().Be(4);
    }

    [Fact]
    public async Task Take_LimitsEachSpansMatches_Independently()
    {
        var two = await Match(_player, ["redbrand", "gundren"], EntryMatchOptions.Default with { Take = 1 });
        two[0].Should().ContainSingle("take is per span");
        two[1].Should().ContainSingle();

        (await Match(_player, ["redbrand"], EntryMatchOptions.Default with { Take = 5 }))[0]
            .Should().HaveCount(2, "both Redbrands match the word prefix");
    }

    [Fact]
    public async Task AViewerMatchesOnlyTheEntriesTheyCanSee()
    {
        // A DM entry and the DM's own Me entry: the DM matches both, by name and by alias.
        var asDm = await Match(_dm, ["iarno", "glasstaff", "agatha"]);
        asDm[0].Should().ContainSingle().Which.EntryId.Should().Be(_iarno);
        asDm[1].Should().ContainSingle().Which.EntryId.Should().Be(_iarno);
        asDm[2].Should().ContainSingle().Which.EntryId.Should().Be(_agatha);

        var asPlayer = await Match(_player, ["iarno", "glasstaff", "agatha"]);
        asPlayer.Should().AllSatisfy(matches => matches.Should().BeEmpty());
    }

    [Fact]
    public async Task AMergedEntry_IsLeftOut_AndItsNameResolvesToItsTarget()
    {
        foreach (var viewer in new[] { _player, _dm })
        {
            var matches = (await Match(viewer, ["klarg"]))[0];
            matches.Should().ContainSingle();
            matches[0].EntryId.Should().Be(_gundren, "a merged entry's name is its target's alias");
            matches[0].EntryId.Should().NotBe(_klarg);
            matches[0].IsAlias.Should().BeTrue();
            matches[0].MatchedName.Should().Be("Klarg");
        }
    }
}
