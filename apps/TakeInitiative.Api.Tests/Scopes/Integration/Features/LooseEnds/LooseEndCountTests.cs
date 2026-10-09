using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.LooseEnds.LooseEndTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.LooseEnds;

/// <summary>
/// <c>GET loose-ends/counts</c> (19b.4) is the list grouped, for every viewer: the total is the
/// list's length and each session's count is its items, with Wiki-only entries in the total alone.
/// </summary>
public class LooseEndCountTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    [Fact]
    public async Task Counts_AreTheListGrouped_ForThreeViewers()
    {
        var campaign = await TestCampaign.Create(fixture, "Loose counts");
        (await fixture.LooseEndCountsOf(Users.DM, campaign.Id)).Total.Should().Be(0);

        // Session 1: a DM-made Other entry in the Wiki, a DM secret-only article, notes from everyone.
        var glasstaff = await fixture.Entry(Users.DM, campaign.Id, "Glasstaff", kind: EntryKind.Other);
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");
        await fixture.Article(Users.DM, campaign.Id, tharden.Id, new BlockEdit(null, "Secretly dead.", Visibility.DM));
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(tharden)} and {Mention(glasstaff)}.");
        await fixture.Note(Users.DM, campaign.Id, "DM scribble", Visibility.DM);
        await fixture.Note(Users.Player, campaign.Id, "we met gundren");
        await fixture.ImageNote(Users.Outsider, campaign.Id);

        // Session 2: an entry made from a note, and more unlinked notes.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostStartSession(campaign.Id, 2)).Should().Succeed();
        var cave = new NewEntry(Guid.NewGuid(), "Cave", EntryKind.Other);
        fixture.LoginAsUser(Users.Player);
        (await fixture.PostSessionNote(campaign.Id, $"Into the {cave.Mention}.", newEntries: cave)).Should().Succeed();
        await fixture.Note(Users.Player, campaign.Id, "Rested");
        await fixture.Note(Users.Outsider, campaign.Id, "Me too", Visibility.Me);

        foreach (var who in new[] { Users.DM, Users.Player, Users.Outsider })
        {
            var items = await fixture.LooseEndsOf(who, campaign.Id);
            var counts = await fixture.LooseEndCountsOf(who, campaign.Id);

            items.Should().NotBeEmpty("every viewer has something loose here ({0})", who);
            counts.Total.Should().Be(items.Length, "the total is the list's length for {0}", who);
            counts.BySession.Should().BeEquivalentTo(
                items.Where(i => i.SessionId != null).GroupBy(i => i.SessionId!.Value).ToDictionary(g => g.Key, g => g.Count()),
                "each divider counts its own items for {0}", who);
            counts.BySession.Values.Sum().Should().Be(items.Count(i => i.SessionId != null));
        }
    }
}
