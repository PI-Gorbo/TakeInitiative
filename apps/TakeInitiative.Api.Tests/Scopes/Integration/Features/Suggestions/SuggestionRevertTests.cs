using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Suggestions.SuggestionTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Suggestions;

/// <summary>
/// <c>GET suggestions/models</c> and <c>POST suggestions/revert</c> (23c.6–7): one model version's
/// accepted mentions go back to text in the caller's own notes, and nothing else changes.
/// </summary>
public class SuggestionRevertTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private async Task<string> TextOf(Users who, Guid campaignId, Guid noteId)
    {
        fixture.LoginAsUser(who);
        var note = await fixture.GetSessionNote(campaignId, noteId);
        note.Should().Succeed();
        return note.Value.Note.Text;
    }

    [Fact]
    public async Task OnlyThatVersionsMentions_Go_AndHandTypedOnesStay()
    {
        var campaign = await TestCampaign.Create(fixture, "Revert version", withSecondPlayer: false);
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        var mara = await fixture.Entry(Users.DM, campaign.Id, "Mara");

        var one = await fixture.Note(Users.Player, campaign.Id, $"met rellan with {Mention(rellan)}");
        await fixture.Accepted(Users.Player, campaign.Id, one, "rellan", rellan.Id);
        var two = await fixture.Note(Users.Player, campaign.Id, "Mara again");
        await fixture.Accepted(Users.Player, campaign.Id, two, "Mara", mara.Id, version: "older");
        var three = await fixture.Note(Users.Player, campaign.Id, "and rellan");
        await fixture.Accepted(Users.Player, campaign.Id, three, "rellan", rellan.Id);

        (await fixture.ModelsOf(Users.Player, campaign.Id)).Select(m => (m.Model, m.Version, m.Mentions, m.Notes))
            .Should().Equal((TestModel, TestVersion, 2, 2), (TestModel, "older", 1, 1));

        var reverted = await fixture.Reverted(Users.Player, campaign.Id);
        (reverted.Notes, reverted.Mentions).Should().Be((2, 2));
        reverted.CreatedEntries.Should().BeEmpty();

        (await TextOf(Users.Player, campaign.Id, one.Id)).Should().Be($"met rellan with {Mention(rellan)}", "the hand-typed mention stays");
        (await TextOf(Users.Player, campaign.Id, two.Id)).Should().Be($"@[Mara](entry:{mara.Id}) again", "another version is untouched");
        (await TextOf(Users.Player, campaign.Id, three.Id)).Should().Be("and rellan");

        (await fixture.ModelsOf(Users.Player, campaign.Id)).Select(m => m.Version).Should().Equal("older");
        var last = (await TestCampaign.EventsOf(fixture, one.Id)).Last().Data.Should().BeOfType<SessionNoteEdited>().Subject;
        last.Actor.Should().Be(Actor.Member(campaign.PlayerMemberId), "the revert is the author's own edit");

        (await fixture.Reverted(Users.Player, campaign.Id)).Mentions.Should().Be(0, "nothing is left to revert");
    }

    [Fact]
    public async Task OnlyTheCallersNotes_AreReverted()
    {
        var campaign = await TestCampaign.Create(fixture, "Revert mine", withSecondPlayer: false);
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        var players = await fixture.Note(Users.Player, campaign.Id, "met rellan");
        await fixture.Accepted(Users.Player, campaign.Id, players, "rellan", rellan.Id);
        var dms = await fixture.Note(Users.DM, campaign.Id, "saw rellan");
        await fixture.Accepted(Users.DM, campaign.Id, dms, "rellan", rellan.Id);

        (await fixture.Reverted(Users.DM, campaign.Id)).Notes.Should().Be(1);
        (await TextOf(Users.DM, campaign.Id, dms.Id)).Should().Be("saw rellan");
        (await TextOf(Users.Player, campaign.Id, players.Id)).Should().Be($"met @[rellan](entry:{rellan.Id})",
            "a DM cannot revert a player's suggestions (invariant 4)");
    }

    [Fact]
    public async Task AMentionEditedAway_IsSkipped_AndOneThatMoved_IsFound()
    {
        var campaign = await TestCampaign.Create(fixture, "Revert edited", withSecondPlayer: false);
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        var gone = await fixture.Note(Users.Player, campaign.Id, "met rellan");
        await fixture.Accepted(Users.Player, campaign.Id, gone, "rellan", rellan.Id);
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionNote(campaign.Id, gone.Id, "met nobody")).Should().Succeed();

        var moved = await fixture.Note(Users.Player, campaign.Id, "met rellan");
        var linked = await fixture.Accepted(Users.Player, campaign.Id, moved, "rellan", rellan.Id);
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionNote(campaign.Id, moved.Id, "Then we " + linked.Text + ".")).Should().Succeed();

        var reverted = await fixture.Reverted(Users.Player, campaign.Id);
        (reverted.Notes, reverted.Mentions).Should().Be((1, 1));
        (await TextOf(Users.Player, campaign.Id, gone.Id)).Should().Be("met nobody");
        (await TextOf(Users.Player, campaign.Id, moved.Id)).Should().Be("Then we met rellan.");
    }

    [Fact]
    public async Task CreatedEntries_AreListed_AndKept()
    {
        var campaign = await TestCampaign.Create(fixture, "Revert created", withSecondPlayer: false);
        var note = await fixture.Note(Users.Player, campaign.Id, "at Greyhollow Keep");
        var keepId = Guid.NewGuid();
        var (text, start, length) = Linked(note.Text, "Greyhollow Keep", keepId);
        (await fixture.Accept(Users.Player, campaign.Id, note.Id, text, start, length, keepId,
            newEntries: [new { id = keepId, name = "Greyhollow Keep", kind = "Place" }])).Status.Should().Be(200);
        await fixture.Entry(Users.Player, campaign.Id, "Made by hand");

        var reverted = await fixture.Reverted(Users.Player, campaign.Id);
        reverted.CreatedEntries.Select(e => (e.Id, e.Name)).Should().Equal((keepId, "Greyhollow Keep"));
        (await TextOf(Users.Player, campaign.Id, note.Id)).Should().Be("at Greyhollow Keep");

        fixture.LoginAsUser(Users.Player);
        (await fixture.GetEntry(campaign.Id, keepId)).Should().Succeed("the entry is kept");
    }
}
