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
/// An entry's connections (19a.3): notes, article blocks and started combats add up to the
/// weight, in the documented order, once per source, and through merges.
/// </summary>
public class ConnectionTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public ConnectionTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    [Fact]
    public async Task NotesBlocksAndCombats_AddUp_HeaviestFirst()
    {
        var campaign = await TestCampaign.Create(fixture, "Connections add up", withSecondPlayer: false);
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren");
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");
        var phandalin = await fixture.Entry(Users.DM, campaign.Id, "Phandalin", kind: EntryKind.Place);
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin");
        var quiet = await fixture.Entry(Users.DM, campaign.Id, "Sildar");

        var note = await fixture.Note(Users.DM, campaign.Id, $"{Mention(gundren)} and {Mention(tharden)} found the mine near {Mention(phandalin)}.");
        await fixture.Article(Users.DM, campaign.Id, gundren.Id, new BlockEdit(null, $"Brother of {Mention(tharden)}."));
        await fixture.StartedCombat(campaign.Id, "Ambush", new { entryId = gundren.Id }, new { entryId = goblin.Id, count = 2 });

        var connections = await fixture.ConnectionsOf(Users.Player, campaign.Id, gundren.Id);

        connections.Select(c => c.Entry.Name).Should().Equal("Tharden", "Goblin", "Phandalin");
        var t = connections.With(tharden)!;
        (t.Weight, t.Notes, t.Blocks, t.Combats).Should().Be((2, 1, 1, 0));
        t.LastAt.Should().Be(note.PostedAt);
        var g = connections.With(goblin)!;
        (g.Weight, g.Combats).Should().Be((1, 1), "two goblins of one entry are one piece of evidence");
        g.LastAt.Should().BeAfter(note.PostedAt, "the combat started after the note");
        connections.With(quiet).Should().BeNull();

        // Undirected: Tharden sees Gundren the same way. The block joins its article's entry only.
        var fromTharden = await fixture.ConnectionsOf(Users.Player, campaign.Id, tharden.Id);
        fromTharden.With(gundren)!.Weight.Should().Be(2);
        fromTharden.With(phandalin)!.Weight.Should().Be(1);
    }

    [Fact]
    public async Task BlocksOnly_HaveNoLastAt_AndBlocksInOneArticleDoNotJoinEachOther()
    {
        var campaign = await TestCampaign.Create(fixture, "Connections blocks", withSecondPlayer: false);
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren");
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");
        var nundro = await fixture.Entry(Users.DM, campaign.Id, "Nundro");
        await fixture.Article(Users.DM, campaign.Id, gundren.Id,
            new BlockEdit(null, $"Brother of {Mention(tharden)}, {Mention(tharden)} again."),
            new BlockEdit(null, $"Also brother of {Mention(nundro)}."));

        var connections = await fixture.ConnectionsOf(Users.Player, campaign.Id, gundren.Id);
        connections.Select(c => (c.Entry.Name, c.Weight, c.Blocks)).Should().Equal(("Nundro", 1, 1), ("Tharden", 1, 1));
        connections.Should().OnlyContain(c => c.LastAt == null);

        (await fixture.ConnectionsOf(Users.Player, campaign.Id, tharden.Id)).Select(c => c.Entry.Id).Should().Equal(gundren.Id);
    }

    [Fact]
    public async Task AMergedEntry_CountsForItsTarget_AndNeverForItself()
    {
        var campaign = await TestCampaign.Create(fixture, "Connections merged", withSecondPlayer: false);
        var gob = await fixture.Entry(Users.DM, campaign.Id, "Gob");
        var goblin = await fixture.Entry(Users.DM, campaign.Id, "Goblin");
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "Klarg");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(gob)} serves {Mention(klarg)}.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(goblin)} and {Mention(klarg)} again.");
        await fixture.Note(Users.DM, campaign.Id, $"{Mention(gob)} is {Mention(goblin)}.");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PostEntryMerge(campaign.Id, gob.Id, goblin.Id)).Should().Succeed();

        // The old id answers for its target.
        var connections = await fixture.ConnectionsOf(Users.Player, campaign.Id, gob.Id);
        connections.Should().ContainSingle();
        connections[0].Entry.Id.Should().Be(klarg.Id);
        connections[0].Weight.Should().Be(2);

        (await fixture.ConnectionsOf(Users.Player, campaign.Id, klarg.Id)).Select(c => (c.Entry.Id, c.Weight)).Should().Equal((goblin.Id, 2));
    }

    [Fact]
    public async Task AnEntryTheCallerCannotSee_IsA404()
    {
        var campaign = await TestCampaign.Create(fixture, "Connections 404", withSecondPlayer: false);
        var secret = await fixture.Entry(Users.DM, campaign.Id, "Secret", Visibility.DM);
        (await fixture.Connections(Users.Player, campaign.Id, secret.Id)).Status.Should().Be(404);
        (await fixture.Connections(Users.Player, campaign.Id, Guid.NewGuid())).Status.Should().Be(404);
        (await fixture.Connections(Users.DM, campaign.Id, secret.Id)).Status.Should().Be(200);
    }
}
