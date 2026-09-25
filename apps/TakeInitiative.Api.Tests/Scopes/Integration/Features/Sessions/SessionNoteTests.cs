using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Sessions;

/// <summary>
/// The SessionNote stream: posting (and posting back in time), author-only edits with
/// history, visibility changes and deletes, and DM-only hiding.
/// </summary>
public class SessionNoteTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private static string NoteUrl(TestCampaign campaign, Guid noteId, string suffix = "")
        => $"/api/campaigns/{campaign.Id}/notes/{noteId}{suffix}";

    [Fact]
    public async Task PostingWithoutASession_GoesToTheCurrentSession()
    {
        var campaign = await TestCampaign.Create(fixture, "Post to current");
        fixture.LoginAsUser(Users.Player);
        (await fixture.PostStartSession(campaign.Id, 2)).Should().Succeed();
        var sessions = (await fixture.GetSessions(campaign.Id)).Value;

        var posted = await fixture.PostSessionNote(campaign.Id, "  We met **Gundren** on the road.\n ", Visibility.Everyone, isRecap: true);

        posted.Should().Succeed();
        var note = posted.Value;
        note.SessionId.Should().Be(sessions.CurrentSessionId);
        note.AuthorMemberId.Should().Be(campaign.PlayerMemberId);
        note.Text.Should().Be("We met **Gundren** on the road.");
        note.Visibility.Should().Be(Visibility.Everyone);
        note.IsRecap.Should().BeTrue();
        note.AddedLater.Should().BeFalse();
        note.EditedAt.Should().BeNull();
        note.IsHidden.Should().BeFalse();
        note.HiddenByMemberId.Should().BeNull();
        note.PostedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Posting to the current session by id is not "added later" either.
        (await fixture.PostSessionNote(campaign.Id, "By id", sessionId: sessions.CurrentSessionId))
            .Value.AddedLater.Should().BeFalse();

        var fetched = await fixture.GetSessionNote(campaign.Id, note.Id);
        fetched.Should().Succeed();
        fetched.Value.Note.Should().BeEquivalentTo(note);
        fetched.Value.SessionNumber.Should().Be(2);
    }

    [Fact]
    public async Task PostingToAnOlderSession_SetsAddedLater_AndLandsAtTheEndOfIt()
    {
        var campaign = await TestCampaign.Create(fixture, "Back-posting");
        fixture.LoginAsUser(Users.DM);
        var session1 = (await fixture.GetSessions(campaign.Id)).Value.Sessions.Single();
        var first = (await fixture.PostSessionNote(campaign.Id, "In session 1")).Value;
        (await fixture.PostStartSession(campaign.Id, 2)).Should().Succeed();
        (await fixture.PostSessionNote(campaign.Id, "In session 2")).Should().Succeed();

        var late = await fixture.PostSessionNote(campaign.Id, "Forgot this", sessionId: session1.Id);

        late.Should().Succeed();
        late.Value.SessionId.Should().Be(session1.Id);
        late.Value.AddedLater.Should().BeTrue();

        var stream = (await fixture.GetSessionStream(campaign.Id)).Value;
        stream.Sessions.Select(s => s.Session.Number).Should().Equal(1, 2);
        stream.Sessions[0].Notes.Select(n => n.Id).Should().Equal(first.Id, late.Value.Id);
        stream.Sessions[1].Notes.Select(n => n.Text).Should().Equal("In session 2");

        // A session of another campaign is a 404.
        var other = (await fixture.PostCreateCampaign(new() { Name = "Other campaign" })).Value;
        var otherSession = (await fixture.GetSessions(other.Id)).Value.CurrentSessionId;
        await fixture.ExpectStatus(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/notes",
            new { sessionId = otherSession, text = "Wrong campaign", visibility = "Everyone", isRecap = false }, 404);
    }

    [Fact]
    public async Task OnlyTheAuthor_EditsChangesVisibilityAndDeletes()
    {
        var campaign = await TestCampaign.Create(fixture, "Author only");
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, "Player's note")).Value;
        var dmNote = (await fixture.PostSessionNote(campaign.Id, "For the DMs", Visibility.DM)).Value;

        // Others who can see the note get 403 (the DM, and the second player on the Everyone note).
        foreach (var other in new[] { Users.DM, Users.Outsider })
        {
            fixture.LoginAsUser(other);
            await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, note.Id), new { text = "Hijacked", isRecap = false }, 403);
            await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, note.Id, "/visibility"), new { visibility = "Me" }, 403);
            await fixture.ExpectStatus(HttpMethod.Delete, NoteUrl(campaign, note.Id), null, 403);
        }

        // The DM can see the DM note, so gets 403; the second player cannot, so gets 404.
        fixture.LoginAsUser(Users.DM);
        await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, dmNote.Id), new { text = "x", isRecap = false }, 403);
        fixture.LoginAsUser(Users.Outsider);
        await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, dmNote.Id), new { text = "x", isRecap = false }, 404);
        await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, dmNote.Id, "/visibility"), new { visibility = "Everyone" }, 404);
        await fixture.ExpectStatus(HttpMethod.Delete, NoteUrl(campaign, dmNote.Id), null, 404);
        (await TestCampaign.EventsOf(fixture, note.Id)).Should().ContainSingle();
        (await TestCampaign.EventsOf(fixture, dmNote.Id)).Should().ContainSingle();

        // The author changes the visibility; the same visibility appends nothing.
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionNoteVisibility(campaign.Id, note.Id, Visibility.Me)).Value.Visibility.Should().Be(Visibility.Me);
        (await fixture.PutSessionNoteVisibility(campaign.Id, note.Id, Visibility.Me)).Should().Succeed();
        (await TestCampaign.EventsOf(fixture, note.Id)).Should().HaveCount(2);
        await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, note.Id, "/visibility"), new { visibility = "Nobody" }, 400);

        // The author deletes it.
        (await fixture.DeleteSessionNote(campaign.Id, note.Id)).Should().Succeed();
        await fixture.ExpectStatus(HttpMethod.Get, NoteUrl(campaign, note.Id), null, 404);
    }

    [Fact]
    public async Task AnEdit_SetsEditedAt_AndTheHistoryListsBothVersions()
    {
        var campaign = await TestCampaign.Create(fixture, "Edits");
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, "We found 10gp")).Value;

        var edited = await fixture.PutSessionNote(campaign.Id, note.Id, " We found **20gp** ", isRecap: true);

        edited.Should().Succeed();
        edited.Value.Text.Should().Be("We found **20gp**");
        edited.Value.IsRecap.Should().BeTrue();
        edited.Value.EditedAt.Should().NotBeNull().And.BeOnOrAfter(note.PostedAt);
        edited.Value.PostedAt.Should().Be(note.PostedAt);

        // Unchanged appends nothing.
        (await fixture.PutSessionNote(campaign.Id, note.Id, "We found **20gp**", isRecap: true)).Should().Succeed();
        (await TestCampaign.EventsOf(fixture, note.Id)).Should().HaveCount(2);

        // Anyone who can see the note reads its history, oldest first.
        fixture.LoginAsUser(Users.Outsider);
        var history = await fixture.GetSessionNoteHistory(campaign.Id, note.Id);
        history.Should().Succeed();
        history.Value.Versions.Select(v => (v.Text, v.IsRecap)).Should().Equal(
            ("We found 10gp", false),
            ("We found **20gp**", true));
        history.Value.Versions[0].At.Should().Be(note.PostedAt);
        history.Value.Versions[1].At.Should().Be(edited.Value.EditedAt!.Value);
    }

    [Fact]
    public async Task ADelete_RemovesTheNoteFromTheStream_AndItsHistory_ButKeepsItsEvents()
    {
        var campaign = await TestCampaign.Create(fixture, "Deletes");
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, "Oops")).Value;
        var kept = (await fixture.PostSessionNote(campaign.Id, "Kept")).Value;

        (await fixture.DeleteSessionNote(campaign.Id, note.Id)).Should().Succeed();

        var stream = (await fixture.GetSessionStream(campaign.Id)).Value;
        stream.Sessions.Single().Notes.Select(n => n.Id).Should().Equal(kept.Id);
        await fixture.ExpectStatus(HttpMethod.Get, NoteUrl(campaign, note.Id), null, 404);
        await fixture.ExpectStatus(HttpMethod.Get, NoteUrl(campaign, note.Id, "/history"), null, 404);
        await fixture.ExpectStatus(HttpMethod.Delete, NoteUrl(campaign, note.Id), null, 404);

        using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        (await session.LoadAsync<SessionNote>(note.Id)).Should().BeNull();
        (await TestCampaign.EventsOf(fixture, note.Id)).Select(e => e.Data.GetType())
            .Should().Equal(typeof(SessionNotePosted), typeof(SessionNoteDeleted));
    }

    [Fact]
    public async Task OnlyADm_HidesAndUnhides_AndHidingTwiceAppendsOneEvent()
    {
        var campaign = await TestCampaign.Create(fixture, "Hiding");
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, "Spoiler!")).Value;

        // The author is not a DM, so cannot hide their own note; nor can another player.
        await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, note.Id, "/hidden"), new { hidden = true }, 403);
        fixture.LoginAsUser(Users.Outsider);
        await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, note.Id, "/hidden"), new { hidden = true }, 403);

        fixture.LoginAsUser(Users.DM);
        var hidden = await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true);
        hidden.Should().Succeed();
        hidden.Value.IsHidden.Should().BeTrue();
        hidden.Value.HiddenByMemberId.Should().Be(campaign.DmMemberId);
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();
        (await TestCampaign.EventsOf(fixture, note.Id)).Should().HaveCount(2);

        // Hide survives an edit.
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionNote(campaign.Id, note.Id, "Spoiler, edited")).Value.IsHidden.Should().BeTrue();

        fixture.LoginAsUser(Users.DM);
        var unhidden = await fixture.PutSessionNoteHidden(campaign.Id, note.Id, false);
        unhidden.Value.IsHidden.Should().BeFalse();
        unhidden.Value.HiddenByMemberId.Should().BeNull();
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, false)).Should().Succeed();
        (await TestCampaign.EventsOf(fixture, note.Id)).Select(e => e.Data.GetType()).Should().Equal(
            typeof(SessionNotePosted), typeof(SessionNoteHidden), typeof(SessionNoteEdited), typeof(SessionNoteUnhidden));

        // A DM cannot hide a Me note: it does not exist for them.
        fixture.LoginAsUser(Users.Player);
        var mine = (await fixture.PostSessionNote(campaign.Id, "Private", Visibility.Me)).Value;
        fixture.LoginAsUser(Users.DM);
        await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, mine.Id, "/hidden"), new { hidden = true }, 404);
    }

    [Fact]
    public async Task Text_IsTrimmedRequiredAndAtMost10000Characters()
    {
        var campaign = await TestCampaign.Create(fixture, "Validation");
        fixture.LoginAsUser(Users.Player);
        var url = $"/api/campaigns/{campaign.Id}/notes";

        await fixture.ExpectStatus(HttpMethod.Post, url, new { text = "", visibility = "Everyone", isRecap = false }, 400);
        await fixture.ExpectStatus(HttpMethod.Post, url, new { text = "   \n ", visibility = "Everyone", isRecap = false }, 400);
        await fixture.ExpectStatus(HttpMethod.Post, url, new { text = new string('a', 10_001), visibility = "Everyone", isRecap = false }, 400);
        await fixture.ExpectStatus(HttpMethod.Post, url, new { text = "ok", visibility = "Nobody", isRecap = false }, 400);

        // 10,000 characters after trimming is fine.
        var longest = await fixture.PostSessionNote(campaign.Id, "  " + new string('a', 10_000) + "  ");
        longest.Should().Succeed();
        longest.Value.Text.Should().HaveLength(10_000);

        await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, longest.Value.Id), new { text = " ", isRecap = false }, 400);
        await fixture.ExpectStatus(HttpMethod.Put, NoteUrl(campaign, longest.Value.Id), new { text = new string('b', 10_001), isRecap = false }, 400);

        // The text is stored verbatim: nothing parses mentions or markdown in step 14.
        var mention = "Met @[the old dwarf](entry:3f2a) <b>here</b>";
        (await fixture.PostSessionNote(campaign.Id, mention)).Value.Text.Should().Be(mention);
    }
}
