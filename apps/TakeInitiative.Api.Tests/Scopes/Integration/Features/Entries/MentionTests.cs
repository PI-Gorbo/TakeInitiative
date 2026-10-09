using System.Text.Json;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using NewEntry = TakeInitiative.Api.Tests.Integration.WebAppClientExtensions.NewEntry;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Mentions on notes and entries created with a note (step 15b.2 and 15b.4). The author is
/// <see cref="Users.Player"/>, the DM is <see cref="Users.DM"/> and the other player is
/// <see cref="Users.Outsider"/>.
/// </summary>
public class MentionTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private string NotesUrl(Guid campaignId) => $"/api/campaigns/{campaignId}/notes";

    private async Task<SessionNote> StoredNote(Guid noteId)
    {
        using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return (await session.LoadAsync<SessionNote>(noteId))!;
    }

    private async Task<(int Status, JsonElement Errors)> PostExpectingError(Guid campaignId, object body)
    {
        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Post.Json(body).ToUrl(NotesUrl(campaignId));
            _.IgnoreStatusCode();
        });
        var json = JsonDocument.Parse(await result.ReadAsTextAsync()).RootElement;
        return (result.Context.Response.StatusCode, json.GetProperty("errors").Clone());
    }

    private static object NoteBody(string text, Visibility visibility, params NewEntry[] newEntries) => new
    {
        text,
        visibility = visibility.ToString(),
        isRecap = false,
        newEntries = newEntries.Select(e => new { id = e.Id, name = e.Name, kind = e.Kind.ToString() }).ToArray(),
    };

    [Fact]
    public async Task ANote_StoresTheIdsItMentions_AndAnEditThatRemovesAMentionClearsIt()
    {
        var campaign = await TestCampaign.Create(fixture, "Mention ids", withSecondPlayer: false);
        fixture.LoginAsUser(Users.DM);
        var gundren = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        var klarg = (await fixture.PostEntry(campaign.Id, "Klarg")).Value;
        var unknown = Guid.NewGuid();

        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id,
            $"@[Klarg](entry:{klarg.Id}) took @[Gundren](entry:{gundren.Id}), says @[Klarg](entry:{klarg.Id}). `@[Code](entry:{unknown})`")).Value;
        (await StoredNote(note.Id)).MentionedEntryIds.Should().Equal(klarg.Id, gundren.Id);

        (await fixture.PutSessionNote(campaign.Id, note.Id, $"@[Gundren](entry:{gundren.Id}) got away.")).Should().Succeed();
        (await StoredNote(note.Id)).MentionedEntryIds.Should().Equal(gundren.Id);

        (await fixture.PutSessionNote(campaign.Id, note.Id, "Nobody got away.")).Should().Succeed();
        (await StoredNote(note.Id)).MentionedEntryIds.Should().BeEmpty();
    }

    [Fact]
    public async Task NewEntries_TakeTheNotesVisibilityAndAuthor_InItsTransaction_AndArePushedAfterIt()
    {
        var campaign = await TestCampaign.Create(fixture, "New entries", withSecondPlayer: false);
        var gundren = new NewEntry(Guid.NewGuid(), "Gundren");
        var phandalin = new NewEntry(Guid.NewGuid(), "Phandalin", EntryKind.Place);

        fixture.LoginAsUser(Users.Player);
        var mark = fixture.Hub.Messages.Count;
        var note = (await fixture.PostSessionNote(campaign.Id,
            $"{gundren.Mention} hired us to reach {phandalin.Mention}.", Visibility.DM, newEntries: [gundren, phandalin])).Value;
        var pushed = fixture.Hub.Messages.Skip(mark).ToList();

        foreach (var created in new[] { gundren, phandalin })
        {
            var entry = (await fixture.GetEntry(campaign.Id, created.Id)).Value;
            entry.Name.Should().Be(created.Name);
            entry.Kind.Should().Be(created.Kind);
            entry.Visibility.Should().Be(Visibility.DM);
            entry.CreatorMemberId.Should().Be(campaign.PlayerMemberId);
        }

        // One transaction: the entries share the note's correlation id and name it as their source.
        var noteEvent = (await TestCampaign.EventsOf(fixture, note.Id)).Single();
        foreach (var created in new[] { gundren, phandalin })
        {
            var entryEvent = (await TestCampaign.EventsOf(fixture, created.Id)).Single();
            entryEvent.CorrelationId.Should().NotBeNullOrWhiteSpace().And.Be(noteEvent.CorrelationId);
            var data = entryEvent.Data.Should().BeOfType<EntryCreated>().Subject;
            data.CreatedFromNoteId.Should().Be(note.Id);
            data.Actor.MemberId.Should().Be(campaign.PlayerMemberId);
        }

        // The note first, then each entry, each to the DM note's audience.
        pushed.Select(m => m.Method).Should().Equal(
            CampaignHubMessages.SessionNoteUpserted, CampaignHubMessages.EntryUpserted, CampaignHubMessages.EntryUpserted);
        pushed.Skip(1).Select(m => ((EntrySummaryResponse)m.Payload!).Id).Should().Equal(gundren.Id, phandalin.Id);
        pushed.Skip(1).Should().OnlyContain(m => m.Groups.SequenceEqual(pushed[0].Groups));

        // Mentioned and counted from the start.
        var listed = (await fixture.GetEntries(campaign.Id)).Value.Entries.Single(e => e.Entry.Id == gundren.Id);
        listed.MentionCount.Should().Be(1);
    }

    [Fact]
    public async Task AMeNote_MakesAMeEntry()
    {
        var campaign = await TestCampaign.Create(fixture, "Me entry");
        var secret = new NewEntry(Guid.NewGuid(), "My hunch");

        fixture.LoginAsUser(Users.Player);
        (await fixture.PostSessionNote(campaign.Id, $"About {secret.Mention}", Visibility.Me, newEntries: [secret])).Should().Succeed();
        (await fixture.GetEntry(campaign.Id, secret.Id)).Value.Visibility.Should().Be(Visibility.Me);

        foreach (var other in new[] { Users.DM, Users.Outsider })
        {
            fixture.LoginAsUser(other);
            await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/entries/{secret.Id}", null, 404);
            (await fixture.GetEntries(campaign.Id)).Value.Entries.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task AnEdit_CanCreateEntries_AndAHiddenEveryoneNoteMakesADmEntry()
    {
        var campaign = await TestCampaign.Create(fixture, "Edit creates");
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, "We met a dwarf.")).Value;
        var gundren = new NewEntry(Guid.NewGuid(), "Gundren");
        (await fixture.PutSessionNote(campaign.Id, note.Id, $"We met {gundren.Mention}.", newEntries: [gundren])).Should().Succeed();
        var entry = (await fixture.GetEntry(campaign.Id, gundren.Id)).Value;
        entry.Visibility.Should().Be(Visibility.Everyone);
        (await TestCampaign.EventsOf(fixture, gundren.Id)).Single().CorrelationId
            .Should().Be((await TestCampaign.EventsOf(fixture, note.Id)).Last().CorrelationId);

        // A hidden note reaches only its author and the DMs, so its new entries do too.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();
        fixture.LoginAsUser(Users.Player);
        var klarg = new NewEntry(Guid.NewGuid(), "Klarg");
        (await fixture.PutSessionNote(campaign.Id, note.Id, $"We met {gundren.Mention} and {klarg.Mention}.", newEntries: [klarg])).Should().Succeed();
        (await fixture.GetEntry(campaign.Id, klarg.Id)).Value.Visibility.Should().Be(Visibility.DM);
        fixture.LoginAsUser(Users.Outsider);
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/entries/{klarg.Id}", null, 404);
    }

    [Fact]
    public async Task AnUnmentionedNewId_IsA400_AndNothingIsCreated()
    {
        var campaign = await TestCampaign.Create(fixture, "Unmentioned", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var gundren = new NewEntry(Guid.NewGuid(), "Gundren");

        var (status, errors) = await PostExpectingError(campaign.Id, NoteBody("We met a dwarf.", Visibility.Everyone, gundren));

        status.Should().Be(400);
        errors.TryGetProperty(NewEntries.ErrorKey, out _).Should().BeTrue();
        (await fixture.GetEntries(campaign.Id)).Value.Entries.Should().BeEmpty();
        (await fixture.GetSessionStream(campaign.Id)).Value.Sessions.Single().Notes.Should().BeEmpty();
    }

    [Fact]
    public async Task AReusedId_IsA409_AndTheNoteIsNotPostedTwice()
    {
        var campaign = await TestCampaign.Create(fixture, "Reused id", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var gundren = new NewEntry(Guid.NewGuid(), "Gundren");
        var body = NoteBody($"We met {gundren.Mention}.", Visibility.Everyone, gundren);
        (await fixture.PostSessionNote(campaign.Id, $"We met {gundren.Mention}.", newEntries: [gundren])).Should().Succeed();

        // A retry after a timeout sends the same ids.
        var (status, errors) = await PostExpectingError(campaign.Id, body);

        status.Should().Be(409);
        errors.GetProperty(NewEntries.AlreadyCreatedEntryIdKey)[0].GetString().Should().Be(gundren.Id.ToString());
        (await fixture.GetSessionStream(campaign.Id)).Value.Sessions.Single().Notes.Should().ContainSingle();
    }

    [Fact]
    public async Task ANameTheAuthorCanSeeAlready_IsA409WithTheExistingId()
    {
        var campaign = await TestCampaign.Create(fixture, "Duplicate new name", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var existing = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        var again = new NewEntry(Guid.NewGuid(), "GUNDREN");

        var (status, errors) = await PostExpectingError(campaign.Id, NoteBody($"{again.Mention}!", Visibility.Everyone, again));

        status.Should().Be(409);
        errors.GetProperty(PostEntry.ExistingEntryIdKey)[0].GetString().Should().Be(existing.Id.ToString());
        errors.GetProperty(NewEntries.NewEntryIdKey)[0].GetString().Should().Be(again.Id.ToString());
        (await fixture.GetEntries(campaign.Id)).Value.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task TheList_IsValidated()
    {
        var campaign = await TestCampaign.Create(fixture, "New entry validation", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        var eleven = Enumerable.Range(1, 11).Select(i => new NewEntry(Guid.NewGuid(), $"Goblin {i}")).ToArray();
        var sameName = new[] { new NewEntry(Guid.NewGuid(), "Klarg"), new NewEntry(Guid.NewGuid(), "klarg") };
        var sameId = Guid.NewGuid();
        var sameIds = new[] { new NewEntry(sameId, "Klarg"), new NewEntry(sameId, "Sildar") };
        var blank = new[] { new NewEntry(Guid.NewGuid(), "  ") };
        foreach (var list in new[] { eleven, sameName, sameIds, blank })
        {
            var text = string.Join(" ", list.Select(e => e.Mention));
            var (status, _) = await PostExpectingError(campaign.Id, NoteBody(text, Visibility.Everyone, list));
            status.Should().Be(400);
        }
        (await fixture.GetEntries(campaign.Id)).Value.Entries.Should().BeEmpty();
    }
}
