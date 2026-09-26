using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Sessions;

/// <summary>
/// The Session stream: Session 1 with every campaign, starting the next session by
/// expected number, DM-only titles, and the gap prompt.
/// </summary>
public class SessionTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    [Fact]
    public async Task ANewCampaign_HasSession1AsItsCurrentSession_InTheSameTransaction()
    {
        fixture.LoginAsUser(Users.DM);
        var campaign = (await fixture.PostCreateCampaign(new() { Name = "Session 1" })).Value;

        var sessions = await fixture.GetSessions(campaign.Id);
        sessions.Should().Succeed();
        var session1 = sessions.Value.Sessions.Should().ContainSingle().Subject;
        session1.Number.Should().Be(1);
        session1.Title.Should().BeNull();
        session1.IsCurrent.Should().BeTrue();
        session1.StartedByMemberId.Should().Be(campaign.OwnerMemberId);
        session1.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        sessions.Value.CurrentSessionId.Should().Be(session1.Id);
        sessions.Value.SuggestNextSession.Should().BeFalse();

        var campaignEvents = await TestCampaign.EventsOf(fixture, campaign.Id);
        var sessionEvents = await TestCampaign.EventsOf(fixture, session1.Id);
        var started = sessionEvents.Should().ContainSingle().Subject;
        started.Data.Should().BeEquivalentTo(new SessionStarted(Actor.Member(campaign.OwnerMemberId), campaign.Id, 1));
        started.CorrelationId.Should().NotBeNullOrWhiteSpace()
            .And.Be(campaignEvents.Single().CorrelationId);
    }

    [Fact]
    public async Task AnyMember_StartsTheNextSession()
    {
        var campaign = await TestCampaign.Create(fixture, "Next session");

        fixture.LoginAsUser(Users.Player);
        var session2 = await fixture.PostStartSession(campaign.Id, 2);
        session2.Should().Succeed();
        session2.Value.Number.Should().Be(2);
        session2.Value.IsCurrent.Should().BeTrue();
        session2.Value.StartedByMemberId.Should().Be(campaign.PlayerMemberId);

        fixture.LoginAsUser(Users.Outsider);
        (await fixture.PostStartSession(campaign.Id, 3)).Value.Number.Should().Be(3);

        fixture.LoginAsUser(Users.DM);
        var sessions = (await fixture.GetSessions(campaign.Id)).Value;
        sessions.Sessions.Select(s => s.Number).Should().Equal(3, 2, 1);
        sessions.Sessions.Select(s => s.IsCurrent).Should().Equal(true, false, false);
        sessions.CurrentSessionId.Should().Be(sessions.Sessions[0].Id);
    }

    [Fact]
    public async Task StartingTheCurrentNumberAgain_ReturnsItWithoutAppending()
    {
        var campaign = await TestCampaign.Create(fixture, "Double tap");

        fixture.LoginAsUser(Users.Player);
        var first = (await fixture.PostStartSession(campaign.Id, 2)).Value;
        fixture.LoginAsUser(Users.DM);
        var second = await fixture.PostStartSession(campaign.Id, 2);

        second.Should().Succeed();
        second.Value.Should().BeEquivalentTo(first);
        (await TestCampaign.EventsOf(fixture, first.Id)).Should().ContainSingle();
        (await fixture.GetSessions(campaign.Id)).Value.Sessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task MembersStartingTheSameSessionAtOnce_AllEndOnOneSession()
    {
        var campaign = await TestCampaign.Create(fixture, "Race", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        var started = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => fixture.PostStartSession(campaign.Id, 2)));

        started.Should().AllSatisfy(r => r.Should().Succeed());
        started.Select(r => r.Value.Id).Distinct().Should().ContainSingle();
        (await fixture.GetSessions(campaign.Id)).Value.Sessions.Select(s => s.Number).Should().Equal(2, 1);
    }

    [Fact]
    public async Task StartingAnyOtherNumber_IsAConflict()
    {
        var campaign = await TestCampaign.Create(fixture, "Skip ahead");
        var url = $"/api/campaigns/{campaign.Id}/sessions";

        fixture.LoginAsUser(Users.Player);
        await fixture.ExpectStatus(HttpMethod.Post, url, new { number = 3 }, 409);
        (await fixture.PostStartSession(campaign.Id, 2)).Should().Succeed();
        // An older number is a conflict too; only the current one is returned as-is.
        await fixture.ExpectStatus(HttpMethod.Post, url, new { number = 1 }, 409);
        await fixture.ExpectStatus(HttpMethod.Post, url, new { number = 0 }, 400);
        (await fixture.GetSessions(campaign.Id)).Value.Sessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task OnlyADm_ChangesTheTitle()
    {
        var campaign = await TestCampaign.Create(fixture, "Titles");
        fixture.LoginAsUser(Users.DM);
        var session1 = (await fixture.GetSessions(campaign.Id)).Value.Sessions.Single();
        var url = $"/api/campaigns/{campaign.Id}/sessions/{session1.Id}/title";

        fixture.LoginAsUser(Users.Player);
        await fixture.ExpectStatus(HttpMethod.Put, url, new { title = "Mine now" }, 403);

        fixture.LoginAsUser(Users.DM);
        var titled = await fixture.PutSessionTitle(campaign.Id, session1.Id, "  The Triboar Trail ");
        titled.Should().Succeed();
        titled.Value.Title.Should().Be("The Triboar Trail");

        // An unchanged title appends nothing.
        (await fixture.PutSessionTitle(campaign.Id, session1.Id, "The Triboar Trail")).Should().Succeed();
        (await TestCampaign.EventsOf(fixture, session1.Id)).Should().HaveCount(2);

        await fixture.ExpectStatus(HttpMethod.Put, url, new { title = new string('x', 101) }, 400);

        // Blank clears it.
        (await fixture.PutSessionTitle(campaign.Id, session1.Id, " ")).Value.Title.Should().BeNull();
        (await TestCampaign.EventsOf(fixture, session1.Id)).Should().HaveCount(3);

        // A promoted DM can change it too.
        (await fixture.PutMemberRole(campaign.Id, campaign.PlayerMemberId, Role.DM)).Should().Succeed();
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionTitle(campaign.Id, session1.Id, "Cragmaw")).Value.Title.Should().Be("Cragmaw");

        // A session from another campaign is a 404.
        fixture.LoginAsUser(Users.DM);
        var other = (await fixture.PostCreateCampaign(new() { Name = "Other" })).Value;
        await fixture.ExpectStatus(HttpMethod.Put, $"/api/campaigns/{other.Id}/sessions/{session1.Id}/title", new { title = "x" }, 404);
    }

    [Fact]
    public async Task AnOutsider_Gets403Everywhere()
    {
        var campaign = await TestCampaign.Create(fixture, "Outsider", withSecondPlayer: false);
        fixture.LoginAsUser(Users.DM);
        var session1 = (await fixture.GetSessions(campaign.Id)).Value.Sessions.Single();
        var note = (await fixture.PostSessionNote(campaign.Id, "A note")).Value;
        var c = $"/api/campaigns/{campaign.Id}";

        fixture.LoginAsUser(Users.Outsider);
        await fixture.ExpectStatus(HttpMethod.Get, $"{c}/sessions", null, 403);
        await fixture.ExpectStatus(HttpMethod.Post, $"{c}/sessions", new { number = 2 }, 403);
        await fixture.ExpectStatus(HttpMethod.Put, $"{c}/sessions/{session1.Id}/title", new { title = "x" }, 403);
        await fixture.ExpectStatus(HttpMethod.Get, $"{c}/stream", null, 403);
        await fixture.ExpectStatus(HttpMethod.Post, $"{c}/notes", new { text = "hi", visibility = "Everyone", isRecap = false }, 403);
        await fixture.ExpectStatus(HttpMethod.Get, $"{c}/notes/{note.Id}", null, 403);
        await fixture.ExpectStatus(HttpMethod.Put, $"{c}/notes/{note.Id}", new { text = "x", isRecap = false }, 403);
        await fixture.ExpectStatus(HttpMethod.Put, $"{c}/notes/{note.Id}/visibility", new { visibility = "Me" }, 403);
        await fixture.ExpectStatus(HttpMethod.Put, $"{c}/notes/{note.Id}/hidden", new { hidden = true }, 403);
        await fixture.ExpectStatus(HttpMethod.Delete, $"{c}/notes/{note.Id}", null, 403);
        await fixture.ExpectStatus(HttpMethod.Get, $"{c}/notes/{note.Id}/history", null, 403);

        // Nothing was appended.
        (await TestCampaign.EventsOf(fixture, session1.Id)).Should().ContainSingle();
        (await TestCampaign.EventsOf(fixture, note.Id)).Should().ContainSingle();

        // An unknown campaign is a 404.
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{Guid.NewGuid()}/sessions", null, 404);
    }

    [Fact]
    public async Task TheGapPrompt_IsOffForAnEmptySessionAndATodaysNote_AndOnForANoteOlderThan3Days()
    {
        var campaign = await TestCampaign.Create(fixture, "Gap prompt");
        fixture.LoginAsUser(Users.Player);

        // An empty current session is waiting to be used, however much time passes.
        using (fixture.Clock.Advance(TimeSpan.FromDays(30)))
        {
            (await fixture.GetSessions(campaign.Id)).Value.SuggestNextSession.Should().BeFalse();
        }

        (await fixture.PostSessionNote(campaign.Id, "Today")).Should().Succeed();
        (await fixture.GetSessions(campaign.Id)).Value.SuggestNextSession.Should().BeFalse();
        (await fixture.GetSessionStream(campaign.Id)).Value.SuggestNextSession.Should().BeFalse();

        using (fixture.Clock.Advance(TimeSpan.FromDays(4)))
        {
            (await fixture.GetSessions(campaign.Id)).Value.SuggestNextSession.Should().BeTrue();
            (await fixture.GetSessionStream(campaign.Id)).Value.SuggestNextSession.Should().BeTrue();

            // Starting the next session turns it off again: the new session is empty.
            var current = (await fixture.GetSessions(campaign.Id)).Value.Sessions[0].Number;
            (await fixture.PostStartSession(campaign.Id, current + 1)).Should().Succeed();
            (await fixture.GetSessions(campaign.Id)).Value.SuggestNextSession.Should().BeFalse();
        }

        // Exactly 3 days is not a gap yet.
        SessionGap.IsGap(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(3)).Should().BeFalse();
        SessionGap.IsGap(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(3).AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public async Task EveryEvent_CarriesAnActorAndACorrelationId()
    {
        var campaign = await TestCampaign.Create(fixture, "Session provenance");
        fixture.LoginAsUser(Users.Player);
        var session2 = (await fixture.PostStartSession(campaign.Id, 2)).Value;
        var note = (await fixture.PostSessionNote(campaign.Id, "First")).Value;
        (await fixture.PutSessionNote(campaign.Id, note.Id, "Second")).Should().Succeed();
        (await fixture.PutSessionNoteVisibility(campaign.Id, note.Id, Visibility.DM)).Should().Succeed();
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionTitle(campaign.Id, session2.Id, "Two")).Should().Succeed();
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, false)).Should().Succeed();
        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteSessionNote(campaign.Id, note.Id)).Should().Succeed();

        var events = (await TestCampaign.EventsOf(fixture, session2.Id))
            .Concat(await TestCampaign.EventsOf(fixture, note.Id))
            .ToList();
        events.Select(e => e.Data.GetType()).Should().Equal(
            typeof(SessionStarted), typeof(SessionTitleChanged),
            typeof(SessionNotePosted), typeof(SessionNoteEdited), typeof(SessionNoteVisibilityChanged),
            typeof(SessionNoteHidden), typeof(SessionNoteUnhidden), typeof(SessionNoteDeleted));
        events.Should().AllSatisfy(e =>
        {
            e.Data.Should().BeAssignableTo<IActorEvent>().Which.Actor.MemberId.Should().NotBeEmpty();
            e.CorrelationId.Should().NotBeNullOrWhiteSpace();
            e.Headers.Should().ContainKey("request");
        });
        events.Select(e => ((IActorEvent)e.Data).Actor.MemberId).Should().Equal(
            campaign.PlayerMemberId, campaign.DmMemberId,
            campaign.PlayerMemberId, campaign.PlayerMemberId, campaign.PlayerMemberId,
            campaign.DmMemberId, campaign.DmMemberId, campaign.PlayerMemberId);
        events.Select(e => e.CorrelationId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task TheStream_PagesBySession_EndingAtTheCurrentOne()
    {
        var campaign = await TestCampaign.Create(fixture, "Paging", withSecondPlayer: false);
        fixture.LoginAsUser(Users.DM);
        for (var number = 2; number <= 5; number++)
        {
            (await fixture.PostStartSession(campaign.Id, number)).Should().Succeed();
            (await fixture.PostSessionNote(campaign.Id, $"Note in {number}")).Should().Succeed();
        }

        var first = (await fixture.GetSessionStream(campaign.Id)).Value;
        first.Sessions.Select(s => s.Session.Number).Should().Equal(3, 4, 5);
        first.Sessions[^1].Session.IsCurrent.Should().BeTrue();
        first.CurrentSessionId.Should().Be(first.Sessions[^1].Session.Id);
        first.HasOlder.Should().BeTrue();
        first.Sessions.Select(s => s.Notes.Single().Text).Should().Equal("Note in 3", "Note in 4", "Note in 5");

        var older = (await fixture.GetSessionStream(campaign.Id, before: 3)).Value;
        older.Sessions.Select(s => s.Session.Number).Should().Equal(1, 2);
        older.Sessions[0].Notes.Should().BeEmpty();
        older.HasOlder.Should().BeFalse();
        older.CurrentSessionId.Should().Be(first.CurrentSessionId);

        (await fixture.GetSessionStream(campaign.Id, take: 10)).Value.Sessions.Should().HaveCount(5);
        (await fixture.GetSessionStream(campaign.Id, before: 5, take: 1)).Value.Sessions.Single().Session.Number.Should().Be(4);
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/stream?take=11", null, 400);
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/stream?take=0", null, 400);
    }

    [Fact]
    public async Task TheTextAndImagesFilters_SplitTheNotes_UnderVisibility()
    {
        var campaign = await TestCampaign.Create(fixture, "Text and images");
        fixture.LoginAsUser(Users.Player);
        var text = (await fixture.PostSessionNote(campaign.Id, "Words")).Value;
        var picture = (await fixture.PostImageNote(campaign.Id, "",
            [(await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp)).Id])).Value;
        var secretPicture = (await fixture.PostImageNote(campaign.Id, "For the DM",
            [(await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp)).Id], Visibility.DM)).Value;
        fixture.LoginAsUser(Users.DM);
        var dmText = (await fixture.PostSessionNote(campaign.Id, "DM words", Visibility.DM)).Value;

        async Task<Guid[]> Ids(Users user, SessionStreamFilter filter)
        {
            fixture.LoginAsUser(user);
            return [.. (await fixture.GetSessionStream(campaign.Id, filter)).Value.Sessions.SelectMany(s => s.Notes).Select(n => n.Id)];
        }

        (await Ids(Users.Player, SessionStreamFilter.Text)).Should().Equal(text.Id);
        (await Ids(Users.Player, SessionStreamFilter.Images)).Should().Equal(picture.Id, secretPicture.Id);
        (await Ids(Users.DM, SessionStreamFilter.Text)).Should().Equal(text.Id, dmText.Id);
        (await Ids(Users.DM, SessionStreamFilter.Images)).Should().Equal(picture.Id, secretPicture.Id);
        (await Ids(Users.Outsider, SessionStreamFilter.Text)).Should().Equal(text.Id);
        (await Ids(Users.Outsider, SessionStreamFilter.Images)).Should().Equal(picture.Id);
        (await Ids(Users.Outsider, SessionStreamFilter.All)).Should().Equal(text.Id, picture.Id);

        // Removing a note's images moves it to Text.
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutImageNote(campaign.Id, secretPicture.Id, "For the DM", [])).Should().Succeed();
        (await Ids(Users.DM, SessionStreamFilter.Images)).Should().Equal(picture.Id);
        (await Ids(Users.DM, SessionStreamFilter.Text)).Should().Equal(text.Id, secretPicture.Id, dmText.Id);
    }

    [Fact]
    public async Task ANoteProjectedBeforeImages_IsText_WithNoDatabaseReset()
    {
        var campaign = await TestCampaign.Create(fixture, "Old note", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, "From step 15")).Value;

        // A document written before step 16 has neither field in its JSON.
        var store = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();
        await using (var session = store.LightweightSession())
        {
            session.QueueSqlCommand("update mt_doc_sessionnote set data = data - 'Images' - 'HasImages' where id = ?", note.Id);
            await session.SaveChangesAsync();
        }

        (await fixture.GetSessionStream(campaign.Id, SessionStreamFilter.Text)).Value.Sessions.Single().Notes.Select(n => n.Id).Should().Equal(note.Id);
        (await fixture.GetSessionStream(campaign.Id, SessionStreamFilter.Images)).Value.Sessions.Single().Notes.Should().BeEmpty();
        (await fixture.GetSessionNote(campaign.Id, note.Id)).Value.Note.Images.Should().BeEmpty();
    }
}
