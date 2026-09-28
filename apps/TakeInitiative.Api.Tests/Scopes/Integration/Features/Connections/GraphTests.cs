using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Connections;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;

namespace TakeInitiative.Api.Tests.Integration.Features.Connections;

/// <summary>
/// The graph (19a.5): depth around a focus, every edge among the returned nodes, the kind
/// filter, a merged focus, the whole campaign without a focus, and the 400s.
/// </summary>
public class GraphTests(AuthenticatedWebAppWithDatabaseFixture fixture) : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private record Chain(TestCampaign Campaign, EntryResponse Gundren, EntryResponse Tharden, EntryResponse Phandalin, EntryResponse Klarg, EntryResponse Nezznar, EntryResponse Sildar);

    /// <summary>Gundren – Tharden – Phandalin (a Place) – Klarg – Nezznar, plus Gundren – Klarg, and Sildar alone.</summary>
    private async Task<Chain> Seed(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name, withSecondPlayer: false);
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren");
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");
        var phandalin = await fixture.Entry(Users.DM, campaign.Id, "Phandalin", kind: EntryKind.Place);
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "Klarg");
        var nezznar = await fixture.Entry(Users.DM, campaign.Id, "Nezznar");
        var sildar = await fixture.Entry(Users.DM, campaign.Id, "Sildar");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(gundren)} and {Mention(tharden)}.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(gundren)} and {Mention(tharden)} again.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(tharden)} in {Mention(phandalin)}.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(klarg)} raids {Mention(phandalin)}.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(klarg)} took {Mention(gundren)}.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(nezznar)} pays {Mention(klarg)}.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(sildar)} alone.");
        return new Chain(campaign, gundren, tharden, phandalin, klarg, nezznar, sildar);
    }

    private static string[] Names(ConnectionGraphResponse graph) => graph.Nodes.Select(n => n.Entry.Name).ToArray();

    [Fact]
    public async Task AFocus_AtDepthOneAndTwo_WithEveryEdgeAmongTheNodes()
    {
        var s = await Seed("Graph depth");

        var one = await fixture.GraphOf(Users.Player, s.Campaign.Id, $"?focus={s.Gundren.Id}");
        one.Truncated.Should().BeFalse();
        one.Nodes[0].Entry.Id.Should().Be(s.Gundren.Id);
        one.Nodes[0].Depth.Should().Be(0);
        one.Nodes[0].MentionCount.Should().Be(3);
        Names(one).Should().BeEquivalentTo("Gundren", "Tharden", "Klarg");
        one.Nodes.Where(n => n.Entry.Id != s.Gundren.Id).Should().OnlyContain(n => n.Depth == 1);
        // Tharden – Klarg are not connected, so no edge; Gundren – Tharden weighs 2.
        one.Edges.Select(e => (e.A, e.B, e.Weight)).Should().BeEquivalentTo(new[]
        {
            Edge(s.Gundren.Id, s.Tharden.Id, 2),
            Edge(s.Gundren.Id, s.Klarg.Id, 1),
        });

        var two = await fixture.GraphOf(Users.Player, s.Campaign.Id, $"?focus={s.Gundren.Id}&depth=2");
        Names(two).Should().BeEquivalentTo("Gundren", "Tharden", "Klarg", "Phandalin", "Nezznar");
        two.Nodes.Single(n => n.Entry.Id == s.Phandalin.Id).Depth.Should().Be(2);
        // The triangle Tharden – Phandalin – Klarg is drawn whole, not only the tree edges.
        two.Edges.Should().HaveCount(5);
    }

    [Fact]
    public async Task Kinds_FilterNodes_NeverTheFocus_AndDepthTwoGoesOnlyThroughPassingNodes()
    {
        var s = await Seed("Graph kinds");

        var characters = await fixture.GraphOf(Users.Player, s.Campaign.Id, $"?focus={s.Tharden.Id}&depth=2&kinds=character");
        Names(characters).Should().BeEquivalentTo("Tharden", "Gundren", "Klarg");
        characters.Edges.Should().NotContain(e => e.A == s.Phandalin.Id || e.B == s.Phandalin.Id);

        var places = await fixture.GraphOf(Users.Player, s.Campaign.Id, $"?focus={s.Gundren.Id}&depth=2&kinds=Place");
        Names(places).Should().Equal("Gundren");
        places.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task WithoutAFocus_EveryConnectedEntry()
    {
        var s = await Seed("Graph all");

        var all = await fixture.GraphOf(Users.Player, s.Campaign.Id);
        Names(all).Should().BeEquivalentTo("Gundren", "Tharden", "Phandalin", "Klarg", "Nezznar");
        all.Nodes.Should().OnlyContain(n => n.Depth == null);
        all.Edges.Should().HaveCount(5);
        all.Nodes.Select(n => n.Entry.Id).Should().NotContain(s.Sildar.Id);
    }

    [Fact]
    public async Task AMergedFocus_CentresOnItsTarget()
    {
        var s = await Seed("Graph merged");
        var gob = await fixture.Entry(Users.DM, s.Campaign.Id, "Gob");
        await fixture.Note(Users.DM, s.Campaign.Id, $"{Mention(gob)} fears {Mention(s.Sildar)}.");
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostEntryMerge(s.Campaign.Id, gob.Id, s.Klarg.Id)).Should().Succeed();

        var graph = await fixture.GraphOf(Users.Player, s.Campaign.Id, $"?focus={gob.Id}");
        graph.Nodes[0].Entry.Id.Should().Be(s.Klarg.Id);
        Names(graph).Should().BeEquivalentTo("Klarg", "Phandalin", "Gundren", "Nezznar", "Sildar");
    }

    [Theory]
    [InlineData("?depth=3")]
    [InlineData("?depth=0")]
    [InlineData("?kinds=Dragon")]
    [InlineData("?kinds=7")]
    public async Task BadParameters_AreA400(string query)
    {
        var campaign = await TestCampaign.Create(fixture, $"Graph 400 {query}", withSecondPlayer: false);
        (await fixture.Graph(Users.Player, campaign.Id, query)).Status.Should().Be(400);
    }

    [Fact]
    public async Task AFocusTheCallerCannotSee_IsA404()
    {
        var campaign = await TestCampaign.Create(fixture, "Graph 404", withSecondPlayer: false);
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "Klarg", Visibility.DM);
        (await fixture.Graph(Users.Player, campaign.Id, $"?focus={klarg.Id}")).Status.Should().Be(404);
    }

    private static (Guid, Guid, int) Edge(Guid x, Guid y, int weight)
        => x.CompareTo(y) <= 0 ? (x, y, weight) : (y, x, weight);
}
