using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Sessions;

/// <summary>
/// Invariant 5 for session notes: every cell of the visibility table (design step 14a.4)
/// on every read. The author is <see cref="Users.Player"/>, the DM is <see cref="Users.DM"/>
/// and the other player is <see cref="Users.Outsider"/>, joined by code.
/// </summary>
public class SessionNoteVisibilityTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    public enum Viewer { Author, Dm, OtherPlayer }

    private static Users UserFor(Viewer viewer) => viewer switch
    {
        Viewer.Author => Users.Player,
        Viewer.Dm => Users.DM,
        Viewer.OtherPlayer => Users.Outsider,
        _ => throw new ArgumentOutOfRangeException(nameof(viewer)),
    };

    [Theory]
    [InlineData(Visibility.Everyone, false, Viewer.Author, true)]
    [InlineData(Visibility.Everyone, false, Viewer.Dm, true)]
    [InlineData(Visibility.Everyone, false, Viewer.OtherPlayer, true)]
    [InlineData(Visibility.Everyone, true, Viewer.Author, true)]
    [InlineData(Visibility.Everyone, true, Viewer.Dm, true)]
    [InlineData(Visibility.Everyone, true, Viewer.OtherPlayer, false)]
    [InlineData(Visibility.DM, false, Viewer.Author, true)]
    [InlineData(Visibility.DM, false, Viewer.Dm, true)]
    [InlineData(Visibility.DM, false, Viewer.OtherPlayer, false)]
    [InlineData(Visibility.Me, false, Viewer.Author, true)]
    [InlineData(Visibility.Me, false, Viewer.Dm, false)]
    [InlineData(Visibility.Me, false, Viewer.OtherPlayer, false)]
    public async Task EveryRead_FollowsTheVisibilityTable(Visibility visibility, bool hidden, Viewer viewer, bool canSee)
    {
        var campaign = await TestCampaign.Create(fixture, $"Visibility {visibility} {hidden} {viewer}");
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, "The note", visibility)).Value;
        if (hidden)
        {
            fixture.LoginAsUser(Users.DM);
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();
        }

        fixture.LoginAsUser(UserFor(viewer));
        var stream = await fixture.GetSessionStream(campaign.Id);
        stream.Should().Succeed();
        var inStream = stream.Value.Sessions.Single().Notes;

        if (canSee)
        {
            // Visible notes come back marked, never altered per viewer.
            inStream.Should().ContainSingle().Which.IsHidden.Should().Be(hidden);
            var single = await fixture.GetSessionNote(campaign.Id, note.Id);
            single.Should().Succeed();
            single.Value.Note.IsHidden.Should().Be(hidden);
            (await fixture.GetSessionNoteHistory(campaign.Id, note.Id)).Value.Versions.Should().ContainSingle();
        }
        else
        {
            // Hidden things are absent: a 404, never a 403.
            inStream.Should().BeEmpty();
            await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/notes/{note.Id}", null, 404);
            await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/notes/{note.Id}/history", null, 404);
        }
    }

    [Fact]
    public async Task ThePromotedPlayer_SeesDmNotes_AndTheDemotedOneStopsSeeingThem()
    {
        var campaign = await TestCampaign.Create(fixture, "Role changes");
        fixture.LoginAsUser(Users.Player);
        var dmNote = (await fixture.PostSessionNote(campaign.Id, "For the DMs", Visibility.DM)).Value;

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutMemberRole(campaign.Id, campaign.SecondPlayerMemberId!.Value, Role.DM)).Should().Succeed();
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.GetSessionNote(campaign.Id, dmNote.Id)).Should().Succeed();

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutMemberRole(campaign.Id, campaign.SecondPlayerMemberId!.Value, Role.Player)).Should().Succeed();
        fixture.LoginAsUser(Users.Outsider);
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/notes/{dmNote.Id}", null, 404);
    }

    [Fact]
    public async Task TheMineAndRecapsFilters_KeepOnlyMatchingNotesTheCallerCanSee()
    {
        var campaign = await TestCampaign.Create(fixture, "Filters");
        fixture.LoginAsUser(Users.Player);
        var playerRecap = (await fixture.PostSessionNote(campaign.Id, "Recap", isRecap: true)).Value;
        var playerNote = (await fixture.PostSessionNote(campaign.Id, "Player note")).Value;
        var playerSecretRecap = (await fixture.PostSessionNote(campaign.Id, "Secret recap", Visibility.Me, isRecap: true)).Value;
        fixture.LoginAsUser(Users.DM);
        var dmNote = (await fixture.PostSessionNote(campaign.Id, "DM note")).Value;
        var dmRecap = (await fixture.PostSessionNote(campaign.Id, "DM-only recap", Visibility.DM, isRecap: true)).Value;
        (await fixture.PostStartSession(campaign.Id, 2)).Should().Succeed();

        async Task<IEnumerable<Guid>> Ids(Users user, SessionStreamFilter filter)
        {
            fixture.LoginAsUser(user);
            var page = (await fixture.GetSessionStream(campaign.Id, filter)).Value;
            // Sessions come back even when no note matches.
            page.Sessions.Select(s => s.Session.Number).Should().Equal(1, 2);
            return page.Sessions.SelectMany(s => s.Notes).Select(n => n.Id);
        }

        (await Ids(Users.Player, SessionStreamFilter.Mine)).Should().Equal(playerRecap.Id, playerNote.Id, playerSecretRecap.Id);
        (await Ids(Users.DM, SessionStreamFilter.Mine)).Should().Equal(dmNote.Id, dmRecap.Id);
        (await Ids(Users.Outsider, SessionStreamFilter.Mine)).Should().BeEmpty();

        (await Ids(Users.Player, SessionStreamFilter.Recaps)).Should().Equal(playerRecap.Id, playerSecretRecap.Id);
        (await Ids(Users.DM, SessionStreamFilter.Recaps)).Should().Equal(playerRecap.Id, dmRecap.Id);
        (await Ids(Users.Outsider, SessionStreamFilter.Recaps)).Should().Equal(playerRecap.Id);

        (await Ids(Users.Outsider, SessionStreamFilter.All)).Should().Equal(playerRecap.Id, playerNote.Id, dmNote.Id);
        (await Ids(Users.Outsider, SessionStreamFilter.Text)).Should().Equal(playerRecap.Id, playerNote.Id, dmNote.Id);
        (await Ids(Users.DM, SessionStreamFilter.Images)).Should().BeEmpty();
        (await Ids(Users.DM, SessionStreamFilter.Combats)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheGapPrompt_IgnoresNotesTheCallerCannotSee()
    {
        var campaign = await TestCampaign.Create(fixture, "Gap visibility");
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostSessionNote(campaign.Id, "Only for me", Visibility.Me)).Should().Succeed();
        fixture.LoginAsUser(Users.Player);
        (await fixture.PostSessionNote(campaign.Id, "For the DMs", Visibility.DM)).Should().Succeed();

        using (fixture.Clock.Advance(TimeSpan.FromDays(4)))
        {
            // The DM and the author each see a note older than 3 days.
            fixture.LoginAsUser(Users.DM);
            (await fixture.GetSessions(campaign.Id)).Value.SuggestNextSession.Should().BeTrue();
            fixture.LoginAsUser(Users.Player);
            (await fixture.GetSessions(campaign.Id)).Value.SuggestNextSession.Should().BeTrue();

            // The other player sees no note in the current session, so nothing is suggested.
            fixture.LoginAsUser(Users.Outsider);
            (await fixture.GetSessions(campaign.Id)).Value.SuggestNextSession.Should().BeFalse();
            (await fixture.GetSessionStream(campaign.Id)).Value.SuggestNextSession.Should().BeFalse();
        }
    }
}
