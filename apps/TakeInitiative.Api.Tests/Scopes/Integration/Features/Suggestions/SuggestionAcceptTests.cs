using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Features.Suggestions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.Features.Combats.CombatTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Connections.ConnectionTestKit;
using static TakeInitiative.Api.Tests.Integration.Features.Suggestions.SuggestionTestKit;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Suggestions;

/// <summary>
/// Accepting a suggestion with <c>PUT notes/{id}</c> (23c.3–5): the event's Actor carries the model,
/// the history shows it, a created entry carries it too, and only the one suggested link passes.
/// </summary>
public class SuggestionAcceptTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private async Task<SessionNote> Load(Guid noteId)
    {
        await using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return (await session.LoadAsync<SessionNote>(noteId))!;
    }

    [Fact]
    public async Task TheEditsActor_HasTheModel_AndTheHistoryShowsIt()
    {
        var campaign = await TestCampaign.Create(fixture, "Accept link", withSecondPlayer: false);
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        var note = await fixture.Note(Users.Player, campaign.Id, "met rellan at the gates");

        var edited = await fixture.Accepted(Users.Player, campaign.Id, note, "rellan", rellan.Id);
        edited.Text.Should().Be($"met @[rellan](entry:{rellan.Id}) at the gates");

        var last = (await TestCampaign.EventsOf(fixture, note.Id)).Last().Data.Should().BeOfType<SessionNoteEdited>().Subject;
        last.Actor.Should().Be(new Actor(campaign.PlayerMemberId, new ModelSuggestion(TestModel, TestVersion, 0.87)));
        last.Suggestion.Should().Be(new SuggestedSpan(4, 6, rellan.Id));

        (await Load(note.Id)).SuggestedMentions.Should().Equal(
            new SuggestedMention(rellan.Id, 4, "rellan", TestModel, TestVersion, 0.87));

        fixture.LoginAsUser(Users.DM);
        var history = await fixture.GetSessionNoteHistory(campaign.Id, note.Id);
        history.Should().Succeed();
        history.Value.Versions.Select(v => v.Model).Should().Equal(null, new ModelSuggestion(TestModel, TestVersion, 0.87));
    }

    [Fact]
    public async Task CreateAndLink_PutsTheModelOnEntryCreated()
    {
        var campaign = await TestCampaign.Create(fixture, "Accept create", withSecondPlayer: false);
        var note = await fixture.Note(Users.Player, campaign.Id, "at the gates of Greyhollow Keep", Visibility.DM);
        var keepId = Guid.NewGuid();
        var (text, start, length) = Linked(note.Text, "Greyhollow Keep", keepId);

        (await fixture.Accept(Users.Player, campaign.Id, note.Id, text, start, length, keepId, confidence: 1.4,
            newEntries: [new { id = keepId, name = "Greyhollow Keep", kind = "Place" }])).Status.Should().Be(200);

        var created = (await TestCampaign.EventsOf(fixture, keepId)).First().Data.Should().BeOfType<EntryCreated>().Subject;
        created.Actor.Should().Be(new Actor(campaign.PlayerMemberId, new ModelSuggestion(TestModel, TestVersion, 1)), "confidence is clamped");
        created.Kind.Should().Be(EntryKind.Place);
        created.Visibility.Should().Be(Visibility.DM, "the entry takes the note's visibility, as any create from a note");
        created.CreatedFromNoteId.Should().Be(note.Id);
    }

    [Fact]
    public async Task ANonAuthor_Gets403()
    {
        var campaign = await TestCampaign.Create(fixture, "Accept not author", withSecondPlayer: false);
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        var note = await fixture.Note(Users.Player, campaign.Id, "met rellan");
        var (text, start, length) = Linked(note.Text, "rellan", rellan.Id);

        (await fixture.Accept(Users.DM, campaign.Id, note.Id, text, start, length, rellan.Id)).Status.Should().Be(403);
    }

    [Fact]
    public async Task AnyEditButTheOneLink_Is400()
    {
        var campaign = await TestCampaign.Create(fixture, "Accept mismatch", withSecondPlayer: false);
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        var hidden = await fixture.Entry(Users.DM, campaign.Id, "Ember Court", Visibility.DM);
        var note = await fixture.Note(Users.Player, campaign.Id, "met rellan and the Ember Court");
        var (text, start, length) = Linked(note.Text, "rellan", rellan.Id);

        var cases = new[]
        {
            await fixture.Accept(Users.Player, campaign.Id, note.Id, text + " later", start, length, rellan.Id),
            await fixture.Accept(Users.Player, campaign.Id, note.Id, text, start + 1, length, rellan.Id),
            await fixture.Accept(Users.Player, campaign.Id, note.Id, text, start, length, rellan.Id, isRecap: true),
            await fixture.Accept(Users.Player, campaign.Id, note.Id, Linked(note.Text, "Ember Court", hidden.Id).Text,
                note.Text.IndexOf("Ember", StringComparison.Ordinal), 11, hidden.Id),
            await fixture.Accept(Users.Player, campaign.Id, note.Id, text, start, length, rellan.Id,
                newEntries: [new { id = Guid.NewGuid(), name = "Other", kind = "Character" }]),
        };
        cases.Should().AllSatisfy(c => c.Status.Should().Be(400, c.Body));
        cases[0].Body.Should().Contain("suggestion");
        cases[3].Body.Should().NotContain("Ember Court\"", "a hidden entry looks like no entry");

        (await Load(note.Id)).Text.Should().Be("met rellan and the Ember Court", "nothing was written");
    }

    [Fact]
    public async Task AHandTypedMention_IsNeverASuggestedOne()
    {
        var campaign = await TestCampaign.Create(fixture, "Accept by hand", withSecondPlayer: false);
        var rellan = await fixture.Entry(Users.DM, campaign.Id, "Rellan Ashvale");
        var note = await fixture.Note(Users.Player, campaign.Id, "met rellan");
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionNote(campaign.Id, note.Id, $"met {Mention(rellan)}")).Should().Succeed();

        (await Load(note.Id)).SuggestedMentions.Should().BeEmpty();
        var last = (await TestCampaign.EventsOf(fixture, note.Id)).Last().Data.Should().BeOfType<SessionNoteEdited>().Subject;
        last.Actor.Model.Should().BeNull();
    }
}
