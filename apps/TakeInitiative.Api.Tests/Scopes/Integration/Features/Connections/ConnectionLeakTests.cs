using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Connections;

/// <summary>
/// The connection leak tests (19a.6, invariants 5, 7 and 8): a player gets no connection,
/// weight, evidence row, graph node or edge from anything they cannot see, and for every pair
/// the weight they see equals the evidence rows they get. <see cref="Users.Player"/> is the
/// player, <see cref="Users.Outsider"/> a second player.
/// </summary>
public class ConnectionLeakTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public ConnectionLeakTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    private async Task<(TestCampaign Campaign, EntryResponse Gundren, EntryResponse Tharden)> Two(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren");
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");
        return (campaign, gundren, tharden);
    }

    /// <summary>Whether <paramref name="who"/> sees Gundren and Tharden connected, anywhere: the panel, the evidence, the graph.</summary>
    private async Task<bool> SeesConnected(Users who, Guid campaignId, EntryResponse a, EntryResponse b)
    {
        var connections = await fixture.ConnectionsOf(who, campaignId, a.Id);
        var evidence = await fixture.EvidenceOf(who, campaignId, a.Id, b.Id);
        var graph = await fixture.GraphOf(who, campaignId);
        var focused = await fixture.GraphOf(who, campaignId, $"?focus={a.Id}&depth=2");

        var inPanel = connections.With(b) is not null;
        var inGraph = graph.Edges.Any(e => (e.A == a.Id && e.B == b.Id) || (e.A == b.Id && e.B == a.Id));
        var inFocus = focused.Nodes.Any(n => n.Entry.Id == b.Id);
        new[] { inPanel, evidence.Evidence.Length > 0, inGraph, inFocus }.Distinct().Should().ContainSingle(
            "the panel, the evidence and the graph agree for {0}", who);
        return inPanel;
    }

    [Theory]
    [InlineData(Visibility.DM, false)]
    [InlineData(Visibility.Everyone, true)]
    public async Task ADmOrHiddenNote_ConnectsNothingForAPlayer(Visibility visibility, bool hide)
    {
        var (campaign, gundren, tharden) = await Two($"Leak note {visibility} {hide}");
        var note = await fixture.Note(Users.DM, campaign.Id, $"{Mention(gundren)} betrayed {Mention(tharden)}.", visibility);
        if (hide)
        {
            fixture.LoginAsUser(Users.DM);
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();
        }

        (await SeesConnected(Users.Player, campaign.Id, gundren, tharden)).Should().BeFalse();
        (await SeesConnected(Users.DM, campaign.Id, gundren, tharden)).Should().BeTrue();
        (await fixture.Evidence(Users.Player, campaign.Id, gundren.Id, tharden.Id).Ok()).Body.Should().NotContain(note.Id.ToString());
    }

    [Fact]
    public async Task ADmSecretBlock_ConnectsNothingForAPlayer()
    {
        var (campaign, gundren, tharden) = await Two("Leak secret block");
        await fixture.Article(Users.DM, campaign.Id, gundren.Id,
            new BlockEdit(null, "Dwarf prospector."),
            new BlockEdit(null, $"Secretly hates {Mention(tharden)}.", Visibility.DM));

        (await SeesConnected(Users.Player, campaign.Id, gundren, tharden)).Should().BeFalse();
        (await SeesConnected(Users.DM, campaign.Id, gundren, tharden)).Should().BeTrue();
        var raw = await fixture.Evidence(Users.Player, campaign.Id, tharden.Id, gundren.Id).Ok();
        raw.Body.Should().NotContainEquivalentOf("hates");
    }

    [Fact]
    public async Task ADmEntry_IsNoNode_AndNoConnection_AndItsIdIsAbsent()
    {
        var (campaign, gundren, tharden) = await Two("Leak DM entry");
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "Klarg", Visibility.DM);
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(tharden)} was killed by {Mention(klarg)}.");
        await fixture.Article(Users.DM, campaign.Id, gundren.Id, new BlockEdit(null, $"Hunted by {Mention(klarg)}."));
        await fixture.StartedCombat(campaign.Id, "Cave", new { entryId = gundren.Id }, new { entryId = klarg.Id });

        var klargId = klarg.Id.ToString();
        (await fixture.Connections(Users.Player, campaign.Id, tharden.Id).Ok()).Body.Should().NotContain(klargId);
        (await fixture.Connections(Users.Player, campaign.Id, gundren.Id).Ok()).Body.Should().NotContain(klargId);
        (await fixture.Graph(Users.Player, campaign.Id).Ok()).Body.Should().NotContain(klargId);
        (await fixture.Graph(Users.Player, campaign.Id, $"?focus={gundren.Id}&depth=2").Ok()).Body.Should().NotContain(klargId);
        (await fixture.Evidence(Users.Player, campaign.Id, tharden.Id, klarg.Id)).Status.Should().Be(404);

        (await fixture.ConnectionsOf(Users.DM, campaign.Id, klarg.Id)).Select(c => c.Entry.Id).Should().BeEquivalentTo([tharden.Id, gundren.Id]);
    }

    [Fact]
    public async Task AHiddenCombatant_ADraft_AndACombatantWhoseEntryIsDm_ConnectNothingForAPlayer()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak combats");
        var brynn = await fixture.Entry(Users.DM, campaign.Id, "Brynn");
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin");
        var wolf = await fixture.Entry(Users.DM, campaign.Id, "Wolf");
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "Klarg", Visibility.DM);

        await fixture.StartedCombat(campaign.Id, "Hidden ambush", new { entryId = brynn.Id }, new { entryId = goblin.Id, hidden = true });
        await fixture.DraftCombat(campaign.Id, "Planned fight", new { entryId = brynn.Id }, new { entryId = wolf.Id });
        await fixture.StartedCombat(campaign.Id, "Boss", new { entryId = brynn.Id }, new { entryId = klarg.Id });

        var player = await fixture.ConnectionsOf(Users.Player, campaign.Id, brynn.Id);
        player.Should().BeEmpty();
        var raw = await fixture.Graph(Users.Player, campaign.Id);
        raw.Body.Should().NotContain(goblin.Id.ToString()).And.NotContain(wolf.Id.ToString()).And.NotContain(klarg.Id.ToString());

        // The DM sees the hidden combatant and their own entry, and nobody counts a Draft.
        var dm = await fixture.ConnectionsOf(Users.DM, campaign.Id, brynn.Id);
        dm.Select(c => c.Entry.Id).Should().BeEquivalentTo([goblin.Id, klarg.Id]);
        dm.Should().OnlyContain(c => c.Combats == 1);
    }

    [Fact]
    public async Task AnotherMembersMeNoteOrEntry_ConnectsNothingForAPlayer()
    {
        var (campaign, gundren, tharden) = await Two("Leak me");
        await fixture.Note(Users.Outsider, campaign.Id, $"{Mention(gundren)} owes {Mention(tharden)}.", Visibility.Me);
        var diary = await fixture.Entry(Users.Outsider, campaign.Id, "Outsider's diary", Visibility.Me);
        await fixture.Note(Users.Outsider, campaign.Id, $"{Mention(diary)} mentions {Mention(gundren)}.");

        (await SeesConnected(Users.Player, campaign.Id, gundren, tharden)).Should().BeFalse();
        (await SeesConnected(Users.DM, campaign.Id, gundren, tharden)).Should().BeFalse("a Me note is its author's alone");
        (await SeesConnected(Users.Outsider, campaign.Id, gundren, tharden)).Should().BeTrue();
        (await fixture.Connections(Users.Player, campaign.Id, gundren.Id).Ok()).Body.Should().NotContain(diary.Id.ToString());
        (await fixture.ConnectionsOf(Users.Outsider, campaign.Id, gundren.Id)).Select(c => c.Entry.Id).Should().BeEquivalentTo([tharden.Id, diary.Id]);
    }

    [Fact]
    public async Task TheWeightEachViewerSees_EqualsTheEvidenceRowsTheyGet_ForEveryPair()
    {
        var campaign = await TestCampaign.Create(fixture, "Leak weight equals rows");
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren");
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");
        var phandalin = await fixture.Entry(Users.DM, campaign.Id, "Phandalin", kind: EntryKind.Place);
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "Klarg", Visibility.DM);
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin");

        await fixture.Note(Users.DM, campaign.Id, $"{Mention(gundren)} and {Mention(tharden)} near {Mention(phandalin)}.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(tharden)} and {Mention(klarg)} and {Mention(gundren)}.", Visibility.DM);
        await fixture.Note(Users.Player, campaign.Id, $"{Mention(gundren)}, {Mention(gundren)} and {Mention(phandalin)}.");
        await fixture.Note(Users.Outsider, campaign.Id, $"{Mention(tharden)} in {Mention(phandalin)}.", Visibility.Me);
        await fixture.Article(Users.DM, campaign.Id, gundren.Id,
            new BlockEdit(null, $"Brother of {Mention(tharden)}."),
            new BlockEdit(null, $"Knows {Mention(phandalin)} and {Mention(klarg)}.", Visibility.DM));
        await fixture.StartedCombat(campaign.Id, "Road", new { entryId = gundren.Id }, new { entryId = goblin.Id, hidden = true }, new { entryId = tharden.Id });

        foreach (var who in new[] { Users.DM, Users.Player, Users.Outsider })
        {
            var graph = await fixture.GraphOf(who, campaign.Id);
            graph.Edges.Should().NotBeEmpty();
            foreach (var edge in graph.Edges)
            {
                var rows = (await fixture.EvidenceOf(who, campaign.Id, edge.A, edge.B)).Evidence;
                rows.Length.Should().Be(edge.Weight, "{0} sees {1}–{2}", who, edge.A, edge.B);
                (edge.Notes + edge.Blocks + edge.Combats).Should().Be(edge.Weight);
                var panel = (await fixture.ConnectionsOf(who, campaign.Id, edge.A)).Single(c => c.Entry.Id == edge.B);
                panel.Weight.Should().Be(edge.Weight);
            }
        }
    }
}
