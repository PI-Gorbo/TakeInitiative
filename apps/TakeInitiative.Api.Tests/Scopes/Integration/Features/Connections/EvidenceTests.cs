using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Connections;
using TakeInitiative.Api.Tests.Integration.Features.Combats;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Connections;

/// <summary>
/// A pair's evidence (19a.4): blocks, then notes newest first, then combats newest first, each
/// with what the web draws, as many rows as the weight, and a plain 200 for an unconnected pair.
/// </summary>
public class EvidenceTests : IClassFixture<RecordingHubFixture>
{
    private readonly RecordingHubFixture fixture;

    public EvidenceTests(RecordingHubFixture fixture)
    {
        this.fixture = fixture;
        UseRealDice(fixture);
    }

    [Fact]
    public async Task Evidence_IsBlocksThenNotesThenCombats_AsManyAsTheWeight()
    {
        var campaign = await TestCampaign.Create(fixture, "Evidence order", withSecondPlayer: false);
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren");
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");

        var first = await fixture.Note(Users.Player, campaign.Id, $"{Mention(gundren)} met {Mention(tharden)}.");
        var second = await fixture.Note(Users.DM, campaign.Id, $"{Mention(tharden)} and {Mention(gundren)} argued.", Visibility.DM);
        var article = await fixture.Article(Users.DM, campaign.Id, gundren.Id,
            new BlockEdit(null, "Dwarf prospector."),
            new BlockEdit(null, $"Brother of {Mention(tharden)}.", Visibility.DM));
        var combatId = await fixture.StartedCombat(campaign.Id, "Cragmaw", new { entryId = gundren.Id }, new { entryId = tharden.Id });

        var evidence = await fixture.EvidenceOf(Users.DM, campaign.Id, gundren.Id, tharden.Id);

        evidence.From.Id.Should().Be(gundren.Id);
        evidence.To.Id.Should().Be(tharden.Id);
        evidence.Evidence.Select(e => e.Kind).Should().Equal(EvidenceKind.Block, EvidenceKind.Note, EvidenceKind.Note, EvidenceKind.Combat);

        var block = evidence.Evidence[0].Block!;
        (block.EntryId, block.EntryName, block.BlockId, block.Visibility).Should().Be((gundren.Id, "Gundren", article.Article.Blocks[1].Id, Visibility.DM));
        block.Snippet.Should().Be($"Brother of {Mention(tharden)}.");
        evidence.Evidence[0].Note.Should().BeNull();

        evidence.Evidence[1].Note!.NoteId.Should().Be(second.Id, "notes are newest first");
        evidence.Evidence[1].Note!.Visibility.Should().Be(Visibility.DM);
        var note = evidence.Evidence[2].Note!;
        (note.NoteId, note.SessionId, note.SessionNumber, note.AuthorMemberId, note.PostedAt, note.HasImages, note.IsHidden)
            .Should().Be((first.Id, first.SessionId, 1, campaign.PlayerMemberId, first.PostedAt, false, false));
        note.Snippet.Should().Be(first.Text);

        var combat = evidence.Evidence[3].Combat!;
        (combat.Card.Id, combat.SessionNumber).Should().Be((combatId, 1));
        combat.Card.Combatants.Select(c => c.EntryId).Should().BeEquivalentTo(new Guid?[] { gundren.Id, tharden.Id });

        var weight = (await fixture.ConnectionsOf(Users.DM, campaign.Id, gundren.Id)).With(tharden)!.Weight;
        weight.Should().Be(evidence.Evidence.Length);

        // Undirected: asked the other way round, the same rows.
        var back = await fixture.EvidenceOf(Users.DM, campaign.Id, tharden.Id, gundren.Id);
        back.Evidence.Should().HaveCount(4);
        back.From.Id.Should().Be(tharden.Id);
    }

    [Fact]
    public async Task ALongNote_IsCutAroundTheTwoMentions()
    {
        var campaign = await TestCampaign.Create(fixture, "Evidence snippet", withSecondPlayer: false);
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren");
        var tharden = await fixture.Entry(Users.DM, campaign.Id, "Tharden");
        var filler = string.Join(" ", Enumerable.Range(0, 80).Select(i => $"word{i}"));
        await fixture.Note(Users.DM, campaign.Id, $"{filler} {Mention(gundren)} met {Mention(tharden)} {filler}");

        var snippet = (await fixture.EvidenceOf(Users.DM, campaign.Id, gundren.Id, tharden.Id)).Evidence.Single().Note!.Snippet;
        snippet.Should().StartWith("…").And.EndWith("…").And.Contain($"{Mention(gundren)} met {Mention(tharden)}");
        snippet.Length.Should().BeLessThan(EvidenceSnippet.Window + 10);
    }

    [Fact]
    public async Task AnUnconnectedPair_IsA200WithNoEvidence_AndAHiddenEndIsA404()
    {
        var campaign = await TestCampaign.Create(fixture, "Evidence none", withSecondPlayer: false);
        var gundren = await fixture.Entry(Users.DM, campaign.Id, "Gundren");
        var sildar = await fixture.Entry(Users.DM, campaign.Id, "Sildar");
        var klarg = await fixture.Entry(Users.DM, campaign.Id, "Klarg", Visibility.DM);

        (await fixture.EvidenceOf(Users.Player, campaign.Id, gundren.Id, sildar.Id)).Evidence.Should().BeEmpty();
        (await fixture.EvidenceOf(Users.Player, campaign.Id, gundren.Id, gundren.Id)).Evidence.Should().BeEmpty();
        (await fixture.Evidence(Users.Player, campaign.Id, gundren.Id, klarg.Id)).Status.Should().Be(404);
        (await fixture.Evidence(Users.Player, campaign.Id, klarg.Id, gundren.Id)).Status.Should().Be(404);
    }
}
