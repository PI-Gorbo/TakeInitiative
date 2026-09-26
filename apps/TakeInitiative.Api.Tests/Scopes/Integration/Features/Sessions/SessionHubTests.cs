using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Sessions;

/// <summary>
/// What each session and note write pushes through <c>CampaignHub</c>: the message names,
/// the groups and the order (design step 14b). The author is <see cref="Users.Player"/>
/// unless a test says otherwise; <see cref="Users.DM"/> owns the campaign.
/// </summary>
public class SessionHubTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private sealed record Groups(TestCampaign Campaign)
    {
        public string All => CampaignGroups.Campaign(Campaign.Id);
        public string Dms => CampaignGroups.Dms(Campaign.Id);
        public string Author => CampaignGroups.Member(Campaign.PlayerMemberId);
        public string DmMember => CampaignGroups.Member(Campaign.DmMemberId);
    }

    private async Task<(TestCampaign Campaign, Groups Groups)> NewCampaign(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name, withSecondPlayer: false);
        return (campaign, new Groups(campaign));
    }

    /// <summary>Runs <paramref name="act"/> and returns the messages it pushed.</summary>
    private async Task<IReadOnlyList<HubMessage>> Pushed(Func<Task> act)
    {
        var mark = fixture.Hub.Messages.Count;
        await act();
        return fixture.Hub.Messages.Skip(mark).ToList();
    }

    private async Task<SessionNoteResponse> Post(TestCampaign campaign, Visibility visibility, Users author = Users.Player)
    {
        fixture.LoginAsUser(author);
        var note = await fixture.PostSessionNote(campaign.Id, $"A {visibility} note", visibility);
        note.Should().Succeed();
        return note.Value;
    }

    private static void ShouldBeUpsert(HubMessage message, Guid noteId, params string[] groups)
    {
        message.Method.Should().Be(CampaignHubMessages.SessionNoteUpserted);
        message.Groups.Should().BeEquivalentTo(groups);
        message.Payload.Should().BeOfType<SessionNoteResponse>().Which.Id.Should().Be(noteId);
    }

    private static void ShouldBeRemoval(HubMessage message, SessionNoteResponse note, params string[] groups)
    {
        message.Method.Should().Be(CampaignHubMessages.SessionNoteRemoved);
        message.Groups.Should().BeEquivalentTo(groups);
        message.Payload.Should().Be(new SessionNoteRemovedMessage(note.Id, note.SessionId));
    }

    [Fact]
    public async Task Post_PushesTheNoteToItsAudienceOnly()
    {
        var (campaign, g) = await NewCampaign("Hub post");

        SessionNoteResponse everyone = null!, dm = null!, me = null!;
        var toEveryone = await Pushed(async () => everyone = await Post(campaign, Visibility.Everyone));
        var toDm = await Pushed(async () => dm = await Post(campaign, Visibility.DM));
        var toMe = await Pushed(async () => me = await Post(campaign, Visibility.Me));

        ShouldBeUpsert(toEveryone.Should().ContainSingle().Subject, everyone.Id, g.All);
        ShouldBeUpsert(toDm.Should().ContainSingle().Subject, dm.Id, g.Dms, g.Author);
        ShouldBeUpsert(toMe.Should().ContainSingle().Subject, me.Id, g.Author);
        toDm.Concat(toMe).Should().NotContain(m => m.Groups.Contains(g.All), "a DM or Me note never goes to the whole campaign");
    }

    [Fact]
    public async Task Post_ByADm_OfADmNote_GoesToTheDmGroupAndTheirOwnGroup()
    {
        var (campaign, g) = await NewCampaign("Hub DM post");
        SessionNoteResponse note = null!;
        var pushed = await Pushed(async () => note = await Post(campaign, Visibility.DM, Users.DM));

        ShouldBeUpsert(pushed.Should().ContainSingle().Subject, note.Id, g.Dms, g.DmMember);
    }

    [Fact]
    public async Task Edit_PushesTheEditedNote_AndAnUnchangedEditPushesNothing()
    {
        var (campaign, g) = await NewCampaign("Hub edit");
        var note = await Post(campaign, Visibility.DM);

        var edited = await Pushed(async () =>
            (await fixture.PutSessionNote(campaign.Id, note.Id, "Changed", isRecap: true)).Should().Succeed());
        var unchanged = await Pushed(async () =>
            (await fixture.PutSessionNote(campaign.Id, note.Id, "Changed", isRecap: true)).Should().Succeed());

        var message = edited.Should().ContainSingle().Subject;
        ShouldBeUpsert(message, note.Id, g.Dms, g.Author);
        ((SessionNoteResponse)message.Payload!).Text.Should().Be("Changed");
        unchanged.Should().BeEmpty();
    }

    [Fact]
    public async Task VisibilityChange_RemovesTheNoteFromWhoLosesIt_ThenPushesItToTheNewAudience()
    {
        var (campaign, g) = await NewCampaign("Hub visibility");
        var note = await Post(campaign, Visibility.Everyone);

        var toDm = await Pushed(async () =>
            (await fixture.PutSessionNoteVisibility(campaign.Id, note.Id, Visibility.DM)).Should().Succeed());
        toDm.Should().HaveCount(2);
        ShouldBeRemoval(toDm[0], note, g.All);
        ShouldBeUpsert(toDm[1], note.Id, g.Dms, g.Author);

        var toMe = await Pushed(async () =>
            (await fixture.PutSessionNoteVisibility(campaign.Id, note.Id, Visibility.Me)).Should().Succeed());
        toMe.Should().HaveCount(2);
        ShouldBeRemoval(toMe[0], note, g.Dms);
        ShouldBeUpsert(toMe[1], note.Id, g.Author);

        // Gaining an audience removes nothing.
        var toEveryone = await Pushed(async () =>
            (await fixture.PutSessionNoteVisibility(campaign.Id, note.Id, Visibility.Everyone)).Should().Succeed());
        ShouldBeUpsert(toEveryone.Should().ContainSingle().Subject, note.Id, g.All);

        var same = await Pushed(async () =>
            (await fixture.PutSessionNoteVisibility(campaign.Id, note.Id, Visibility.Everyone)).Should().Succeed());
        same.Should().BeEmpty();
    }

    [Fact]
    public async Task Hide_RemovesTheNoteFromPlayers_KeepsItForDmsAndTheAuthor_AndTellsTheAuthor()
    {
        var (campaign, g) = await NewCampaign("Hub hide");
        var note = await Post(campaign, Visibility.Everyone);

        fixture.LoginAsUser(Users.DM);
        var hidden = await Pushed(async () =>
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed());

        hidden.Select(m => m.Method).Should().Equal(
            CampaignHubMessages.SessionNoteRemoved, CampaignHubMessages.SessionNoteUpserted, CampaignHubMessages.SessionNoteHidden);
        ShouldBeRemoval(hidden[0], note, g.All);
        ShouldBeUpsert(hidden[1], note.Id, g.Dms, g.Author);
        ((SessionNoteResponse)hidden[1].Payload!).IsHidden.Should().BeTrue();
        hidden[2].Groups.Should().Equal(g.Author);
        hidden[2].Payload.Should().Be(new SessionNoteHiddenMessage(note.Id, note.SessionId, campaign.DmMemberId));

        var again = await Pushed(async () =>
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed());
        again.Should().BeEmpty();

        var unhidden = await Pushed(async () =>
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, false)).Should().Succeed());
        var message = unhidden.Should().ContainSingle().Subject;
        ShouldBeUpsert(message, note.Id, g.All);
        ((SessionNoteResponse)message.Payload!).IsHidden.Should().BeFalse();
    }

    [Fact]
    public async Task Hide_OfADmNote_StaysWithinTheDmGroupAndTheAuthor()
    {
        var (campaign, g) = await NewCampaign("Hub hide DM note");
        var note = await Post(campaign, Visibility.DM);

        fixture.LoginAsUser(Users.DM);
        var hidden = await Pushed(async () =>
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed());

        hidden.Select(m => m.Method).Should().Equal(CampaignHubMessages.SessionNoteUpserted, CampaignHubMessages.SessionNoteHidden);
        ShouldBeUpsert(hidden[0], note.Id, g.Dms, g.Author);
        hidden.Should().NotContain(m => m.Groups.Contains(g.All));
    }

    [Fact]
    public async Task Hide_OfTheDmsOwnNote_DoesNotTellThemAboutIt()
    {
        var (campaign, g) = await NewCampaign("Hub hide own");
        var note = await Post(campaign, Visibility.Everyone, Users.DM);

        var hidden = await Pushed(async () =>
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed());

        hidden.Select(m => m.Method).Should().Equal(CampaignHubMessages.SessionNoteRemoved, CampaignHubMessages.SessionNoteUpserted);
        ShouldBeUpsert(hidden[1], note.Id, g.Dms, g.DmMember);
    }

    [Theory]
    [InlineData(Visibility.Everyone)]
    [InlineData(Visibility.DM)]
    [InlineData(Visibility.Me)]
    public async Task Delete_RemovesTheNoteFromItsAudience(Visibility visibility)
    {
        var (campaign, g) = await NewCampaign($"Hub delete {visibility}");
        var note = await Post(campaign, visibility);

        var deleted = await Pushed(async () =>
            (await fixture.DeleteSessionNote(campaign.Id, note.Id)).Should().Succeed());

        string[] audience = visibility switch
        {
            Visibility.Everyone => [g.All],
            Visibility.DM => [g.Dms, g.Author],
            _ => [g.Author],
        };
        ShouldBeRemoval(deleted.Should().ContainSingle().Subject, note, audience);
    }

    [Fact]
    public async Task HiddenNoteDelete_RemovesItOnlyFromDmsAndTheAuthor()
    {
        var (campaign, g) = await NewCampaign("Hub delete hidden");
        var note = await Post(campaign, Visibility.Everyone);
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();

        fixture.LoginAsUser(Users.Player);
        var deleted = await Pushed(async () =>
            (await fixture.DeleteSessionNote(campaign.Id, note.Id)).Should().Succeed());

        ShouldBeRemoval(deleted.Should().ContainSingle().Subject, note, g.Dms, g.Author);
    }

    [Fact]
    public async Task SessionStartAndTitle_ArePushedToTheWholeCampaign_OnlyWhenSomethingChanged()
    {
        var (campaign, g) = await NewCampaign("Hub sessions");

        fixture.LoginAsUser(Users.Player);
        SessionResponse started = null!;
        var start = await Pushed(async () => started = (await fixture.PostStartSession(campaign.Id, 2)).Value);
        var repeat = await Pushed(async () => (await fixture.PostStartSession(campaign.Id, 2)).Should().Succeed());

        var startMessage = start.Should().ContainSingle().Subject;
        startMessage.Method.Should().Be(CampaignHubMessages.SessionStarted);
        startMessage.Groups.Should().Equal(g.All);
        startMessage.Payload.Should().BeOfType<SessionResponse>()
            .Which.Should().Match<SessionResponse>(s => s.Id == started.Id && s.Number == 2 && s.IsCurrent);
        repeat.Should().BeEmpty("asking for the current number starts nothing");

        fixture.LoginAsUser(Users.DM);
        var titled = await Pushed(async () => (await fixture.PutSessionTitle(campaign.Id, started.Id, "The Heist")).Should().Succeed());
        var sameTitle = await Pushed(async () => (await fixture.PutSessionTitle(campaign.Id, started.Id, "The Heist")).Should().Succeed());

        var titleMessage = titled.Should().ContainSingle().Subject;
        titleMessage.Method.Should().Be(CampaignHubMessages.SessionTitleChanged);
        titleMessage.Groups.Should().Equal(g.All);
        titleMessage.Payload.Should().BeOfType<SessionResponse>().Which.Title.Should().Be("The Heist");
        sameTitle.Should().BeEmpty();
    }

    [Fact]
    public async Task ARejectedWrite_PushesNothing()
    {
        var (campaign, _) = await NewCampaign("Hub rejected");
        var note = await Post(campaign, Visibility.Everyone);

        var pushed = await Pushed(async () =>
        {
            // A player cannot hide, and only the author can edit.
            fixture.LoginAsUser(Users.Player);
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).IsFailure.Should().BeTrue();
            fixture.LoginAsUser(Users.DM);
            (await fixture.PutSessionNote(campaign.Id, note.Id, "Not mine")).IsFailure.Should().BeTrue();
            (await fixture.DeleteSessionNote(campaign.Id, note.Id)).IsFailure.Should().BeTrue();
        });

        pushed.Should().BeEmpty();
    }
}
