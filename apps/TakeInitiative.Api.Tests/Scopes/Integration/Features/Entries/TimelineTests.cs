using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// The timeline and the mention counts (step 15b.5 and 15b.6) against the session-note
/// visibility table (14a): a mention is only as visible as the note it is in. The note's
/// author is <see cref="Users.Player"/>, the DM is <see cref="Users.DM"/> and the other
/// player is <see cref="Users.Outsider"/>. The entries are Everyone entries made by the DM.
/// </summary>
public class TimelineTests(AuthenticatedWebAppWithDatabaseFixture fixture)
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

    private static string Mention(EntryResponse entry) => $"@[{entry.Name}](entry:{entry.Id})";

    private async Task<(TestCampaign Campaign, EntryResponse Entry)> CampaignWithEntry(string name)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        fixture.LoginAsUser(Users.DM);
        var entry = await fixture.PostEntry(campaign.Id, "Gundren");
        entry.Should().Succeed();
        return (campaign, entry.Value);
    }

    private async Task<EntryListItemResponse> ListItem(TestCampaign campaign, Guid entryId)
    {
        var list = await fixture.GetEntries(campaign.Id);
        list.Should().Succeed();
        return list.Value.Entries.Single(e => e.Entry.Id == entryId);
    }

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
    public async Task TheTimelineAndTheCount_FollowTheNoteVisibilityTable(Visibility visibility, bool hidden, Viewer viewer, bool canSee)
    {
        var (campaign, entry) = await CampaignWithEntry($"Timeline {visibility} {hidden} {viewer}");
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostSessionNote(campaign.Id, $"We met {Mention(entry)} on the road.", visibility)).Value;
        if (hidden)
        {
            fixture.LoginAsUser(Users.DM);
            (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();
        }

        fixture.LoginAsUser(UserFor(viewer));
        var timeline = await fixture.GetEntryTimeline(campaign.Id, entry.Id);
        timeline.Should().Succeed();
        var item = await ListItem(campaign, entry.Id);

        if (canSee)
        {
            var only = timeline.Value.Items.Should().ContainSingle().Subject;
            only.Note.Id.Should().Be(note.Id);
            only.Note.IsHidden.Should().Be(hidden);
            only.SessionNumber.Should().Be(1);
            item.MentionCount.Should().Be(1);
            item.LastMentionedAt.Should().Be(note.PostedAt);
        }
        else
        {
            // Absent, and not counted: a count would reveal that the note exists.
            timeline.Value.Items.Should().BeEmpty();
            item.MentionCount.Should().Be(0);
            item.LastMentionedAt.Should().BeNull();
        }
        timeline.Value.HasOlder.Should().BeFalse();
    }

    [Fact]
    public async Task TheCounts_ComeFromVisibleNotesOnly()
    {
        var (campaign, entry) = await CampaignWithEntry("Counts per viewer");
        fixture.LoginAsUser(Users.DM);
        var dmEntry = (await fixture.PostEntry(campaign.Id, "Glasstaff", EntryKind.Character, Visibility.DM)).Value;
        var quiet = (await fixture.PostEntry(campaign.Id, "Sildar")).Value;
        var dmNote = (await fixture.PostSessionNote(campaign.Id, $"{Mention(entry)} and {Mention(dmEntry)} are brothers?", Visibility.DM)).Value;

        fixture.LoginAsUser(Users.Player);
        // A note that mentions an entry twice counts once. An unknown id is kept and never matches.
        var twice = (await fixture.PostSessionNote(campaign.Id,
            $"{Mention(entry)}, {Mention(entry)} again, and @[Nobody](entry:{Guid.NewGuid()})")).Value;

        fixture.LoginAsUser(Users.Outsider);
        var theirs = (await fixture.GetEntries(campaign.Id)).Value.Entries;
        theirs.Select(e => e.Entry.Id).Should().BeEquivalentTo([entry.Id, quiet.Id]);
        theirs.Single(e => e.Entry.Id == entry.Id).MentionCount.Should().Be(1, "the DM note is not counted for a player");
        theirs.Single(e => e.Entry.Id == entry.Id).LastMentionedAt.Should().Be(twice.PostedAt);
        theirs.Single(e => e.Entry.Id == quiet.Id).MentionCount.Should().Be(0);

        fixture.LoginAsUser(Users.DM);
        (await ListItem(campaign, entry.Id)).MentionCount.Should().Be(2);
        var glasstaff = await ListItem(campaign, dmEntry.Id);
        glasstaff.MentionCount.Should().Be(1);
        glasstaff.LastMentionedAt.Should().Be(dmNote.PostedAt);
    }

    [Fact]
    public async Task ADeletedNote_LeavesTheTimeline_AndAnEditThatRemovesTheMentionToo()
    {
        var (campaign, entry) = await CampaignWithEntry("Timeline delete");
        fixture.LoginAsUser(Users.Player);
        var deleted = (await fixture.PostSessionNote(campaign.Id, $"Met {Mention(entry)}.")).Value;
        var edited = (await fixture.PostSessionNote(campaign.Id, $"Met {Mention(entry)} again.")).Value;
        (await fixture.GetEntryTimeline(campaign.Id, entry.Id)).Value.Items.Should().HaveCount(2);

        (await fixture.DeleteSessionNote(campaign.Id, deleted.Id)).Should().Succeed();
        (await fixture.PutSessionNote(campaign.Id, edited.Id, "Met someone again.")).Should().Succeed();

        (await fixture.GetEntryTimeline(campaign.Id, entry.Id)).Value.Items.Should().BeEmpty();
        (await ListItem(campaign, entry.Id)).MentionCount.Should().Be(0);
    }

    [Fact]
    public async Task ARename_LeavesTheTextAlone_AndTheNoteOnTheTimeline()
    {
        var (campaign, entry) = await CampaignWithEntry("Timeline rename");
        fixture.LoginAsUser(Users.Player);
        var text = $"We met {Mention(entry)}.";
        var note = (await fixture.PostSessionNote(campaign.Id, text)).Value;

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryName(campaign.Id, entry.Id, "Gundren Rockseeker")).Should().Succeed();

        // Invariant 6: mentions are stored by id, so the text still reads "Gundren".
        var only = (await fixture.GetEntryTimeline(campaign.Id, entry.Id)).Value.Items.Should().ContainSingle().Subject;
        only.Note.Id.Should().Be(note.Id);
        only.Note.Text.Should().Be(text);
    }

    [Fact]
    public async Task Pages_AreNewestFirst_OldestFirstWithinAPage()
    {
        var (campaign, entry) = await CampaignWithEntry("Timeline paging");
        fixture.LoginAsUser(Users.DM);
        (await fixture.PostStartSession(campaign.Id, 2)).Should().Succeed();
        var posted = new List<Guid>();
        for (var i = 1; i <= 5; i++)
        {
            posted.Add((await fixture.PostSessionNote(campaign.Id, $"Note {i} about {Mention(entry)}")).Value.Id);
            if (i == 2)
            {
                // Something in between that does not mention the entry.
                (await fixture.PostSessionNote(campaign.Id, "Unrelated")).Should().Succeed();
            }
        }

        var newest = (await fixture.GetEntryTimeline(campaign.Id, entry.Id, take: 2)).Value;
        newest.Items.Select(i => i.Note.Id).Should().Equal(posted[3], posted[4]);
        newest.Items.Should().OnlyContain(i => i.SessionNumber == 2);
        newest.HasOlder.Should().BeTrue();

        var middle = (await fixture.GetEntryTimeline(campaign.Id, entry.Id, before: newest.Items[0].Note.PostedAt, take: 2)).Value;
        middle.Items.Select(i => i.Note.Id).Should().Equal(posted[1], posted[2]);
        middle.HasOlder.Should().BeTrue();

        var oldest = (await fixture.GetEntryTimeline(campaign.Id, entry.Id, before: middle.Items[0].Note.PostedAt, take: 2)).Value;
        oldest.Items.Select(i => i.Note.Id).Should().Equal(posted[0]);
        oldest.HasOlder.Should().BeFalse();

        // The default page holds them all; take is 1 to 50.
        (await fixture.GetEntryTimeline(campaign.Id, entry.Id)).Value.Items.Should().HaveCount(5);
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/entries/{entry.Id}/timeline?take=51", null, 400);
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/entries/{entry.Id}/timeline?take=0", null, 400);
    }

    [Fact]
    public async Task AnEntryTheCallerCannotSee_HasNoTimeline()
    {
        var (campaign, _) = await CampaignWithEntry("Timeline hidden entry");
        fixture.LoginAsUser(Users.DM);
        var secret = (await fixture.PostEntry(campaign.Id, "Glasstaff", EntryKind.Character, Visibility.DM)).Value;
        (await fixture.PostSessionNote(campaign.Id, $"An Everyone note about {Mention(secret)}")).Should().Succeed();
        (await fixture.GetEntryTimeline(campaign.Id, secret.Id)).Value.Items.Should().ContainSingle();

        fixture.LoginAsUser(Users.Player);
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/entries/{secret.Id}/timeline", null, 404);
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}/entries/{Guid.NewGuid()}/timeline", null, 404);
    }
}
