using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Images;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Images;

/// <summary>
/// Step 16d.6, galleries: a session's image notes and the image notes whose caption mentions
/// an entry, strictly per viewer (invariant 5). <see cref="Users.DM"/> is the DM,
/// <see cref="Users.Player"/> and <see cref="Users.Outsider"/> the players.
/// </summary>
public class GalleryTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private async Task<SessionNoteResponse> ImageNote(
        Users user, Guid campaignId, string caption, Visibility visibility = Visibility.Everyone, int images = 1)
    {
        fixture.LoginAsUser(user);
        var ids = new List<Guid>();
        for (var i = 0; i < images; i++)
        {
            ids.Add((await fixture.UploadFixture(campaignId, ImageFixtures.Webp)).Id);
        }
        var note = await fixture.PostImageNote(campaignId, caption, ids.ToArray(), visibility);
        note.Should().Succeed();
        return note.Value;
    }

    private async Task<GalleryResponse> SessionGallery(Users user, Guid campaignId, Guid sessionId, DateTimeOffset? before = null, int? take = null)
    {
        fixture.LoginAsUser(user);
        var gallery = await fixture.GetSessionImages(campaignId, sessionId, before, take);
        gallery.Should().Succeed();
        return gallery.Value;
    }

    private async Task<GalleryResponse> EntryGallery(Users user, Guid campaignId, Guid entryId, DateTimeOffset? before = null, int? take = null)
    {
        fixture.LoginAsUser(user);
        var gallery = await fixture.GetEntryImages(campaignId, entryId, before, take);
        gallery.Should().Succeed();
        return gallery.Value;
    }

    private async Task<EntryResponse> Entry(Users user, Guid campaignId, string name, Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.PostEntry(campaignId, name, EntryKind.Place, visibility);
        entry.Should().Succeed();
        return entry.Value;
    }

    private static string Mention(EntryResponse entry) => $"@[{entry.Name}](entry:{entry.Id})";

    private static Guid[] NoteIds(GalleryResponse gallery) => gallery.Items.Select(i => i.Note.Id).ToArray();

    [Fact]
    public async Task TheSessionGallery_IsPerViewer_AndLeavesOutTextNotes()
    {
        var campaign = await TestCampaign.Create(fixture, "Session gallery");
        var everyone = await ImageNote(Users.Player, campaign.Id, "The map", images: 2);
        var dmOnly = await ImageNote(Users.DM, campaign.Id, "The villain's lair", Visibility.DM);
        var mine = await ImageNote(Users.Player, campaign.Id, "My sketch", Visibility.Me);
        var hidden = await ImageNote(Users.Outsider, campaign.Id, "A spoiler");
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, hidden.Id, true)).Should().Succeed();
        fixture.LoginAsUser(Users.Player);
        (await fixture.PostSessionNote(campaign.Id, "Only words")).Should().Succeed();
        var sessionId = everyone.SessionId;

        NoteIds(await SessionGallery(Users.DM, campaign.Id, sessionId))
            .Should().Equal(everyone.Id, dmOnly.Id, hidden.Id);
        NoteIds(await SessionGallery(Users.Player, campaign.Id, sessionId))
            .Should().Equal(everyone.Id, mine.Id);
        // The hidden note is its author's and the DMs'.
        NoteIds(await SessionGallery(Users.Outsider, campaign.Id, sessionId))
            .Should().Equal(everyone.Id, hidden.Id);

        // Counts are per viewer too: only images the viewer can see.
        (await SessionGallery(Users.DM, campaign.Id, sessionId)).ImageCount.Should().Be(4);
        (await SessionGallery(Users.Player, campaign.Id, sessionId)).ImageCount.Should().Be(3);
        var gallery = await SessionGallery(Users.Outsider, campaign.Id, sessionId);
        gallery.ImageCount.Should().Be(3);
        gallery.Items[0].SessionNumber.Should().Be(1);
        gallery.Items[0].Note.Images.Should().HaveCount(2);
        gallery.HasOlder.Should().BeFalse();
    }

    [Fact]
    public async Task TheSessionGallery_OfAnotherCampaignsSession_IsA404_AndOutsidersAreRefused()
    {
        var campaign = await TestCampaign.Create(fixture, "Gallery home");
        var other = await TestCampaign.Create(fixture, "Gallery elsewhere", withSecondPlayer: false);
        var note = await ImageNote(Users.Player, campaign.Id, "A picture");

        fixture.LoginAsUser(Users.DM);
        (await fixture.GetStatus(GalleryUrl(other.Id, "sessions", note.SessionId))).Should().Be(404);
        (await fixture.GetStatus(GalleryUrl(campaign.Id, "sessions", Guid.NewGuid()))).Should().Be(404);
        (await fixture.GetStatus(GalleryUrl(campaign.Id, "sessions", note.SessionId, take: 61))).Should().Be(400);
        (await fixture.GetStatus(GalleryUrl(campaign.Id, "sessions", note.SessionId, take: 0))).Should().Be(400);

        // Outsider is not in the other campaign.
        fixture.LoginAsUser(Users.Outsider);
        (await fixture.GetStatus(GalleryUrl(other.Id, "sessions", note.SessionId))).Should().BeOneOf(403, 404);
    }

    [Fact]
    public async Task TheSessionGallery_PagesNewestFirst_OldestFirstWithinAPage()
    {
        var campaign = await TestCampaign.Create(fixture, "Gallery paging", withSecondPlayer: false);
        var notes = new List<SessionNoteResponse>();
        for (var i = 0; i < 5; i++)
        {
            notes.Add(await ImageNote(Users.Player, campaign.Id, $"Picture {i}"));
        }
        var sessionId = notes[0].SessionId;

        var newest = await SessionGallery(Users.DM, campaign.Id, sessionId, take: 2);
        NoteIds(newest).Should().Equal(notes[3].Id, notes[4].Id);
        newest.HasOlder.Should().BeTrue();
        newest.ImageCount.Should().Be(5);

        var middle = await SessionGallery(Users.DM, campaign.Id, sessionId, newest.Items[0].Note.PostedAt, take: 2);
        NoteIds(middle).Should().Equal(notes[1].Id, notes[2].Id);
        middle.HasOlder.Should().BeTrue();

        var oldest = await SessionGallery(Users.DM, campaign.Id, sessionId, middle.Items[0].Note.PostedAt, take: 2);
        NoteIds(oldest).Should().Equal(notes[0].Id);
        oldest.HasOlder.Should().BeFalse();
        oldest.ImageCount.Should().Be(5);
    }

    [Fact]
    public async Task TheEntryGallery_IsTheImageNotesWhoseCaptionMentionsTheEntry_PerViewer()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry gallery");
        var hideout = await Entry(Users.Player, campaign.Id, "Cragmaw Hideout");
        var castle = await Entry(Users.Player, campaign.Id, "Cragmaw Castle");

        var map = await ImageNote(Users.Player, campaign.Id, $"Map of {Mention(hideout)}", images: 2);
        var dmOnly = await ImageNote(Users.DM, campaign.Id, $"The real {Mention(hideout)}", Visibility.DM);
        await ImageNote(Users.Player, campaign.Id, $"Not this one: {Mention(castle)}");
        await ImageNote(Users.Player, campaign.Id, "No mention at all");
        fixture.LoginAsUser(Users.Player);
        (await fixture.PostSessionNote(campaign.Id, $"Words about {Mention(hideout)}")).Should().Succeed();

        // A mention in an article block does not put an image in the gallery.
        (await fixture.PutEntryArticle(campaign.Id, castle.Id, castle.Article.Etag, [new BlockEdit(null, $"Near {Mention(hideout)}.")]))
            .Should().Succeed();

        var dm = await EntryGallery(Users.DM, campaign.Id, hideout.Id);
        NoteIds(dm).Should().Equal(map.Id, dmOnly.Id);
        dm.ImageCount.Should().Be(3);
        var player = await EntryGallery(Users.Player, campaign.Id, hideout.Id);
        NoteIds(player).Should().Equal(map.Id);
        player.ImageCount.Should().Be(2);
        var outsider = await EntryGallery(Users.Outsider, campaign.Id, hideout.Id);
        NoteIds(outsider).Should().Equal(map.Id);
        outsider.Items[0].SessionNumber.Should().Be(1);
        outsider.Items[0].Note.Images.Should().HaveCount(2);
    }

    [Fact]
    public async Task TheEntryGallery_IncludesCaptionsThatMentionAMergedEntry()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry gallery merge");
        var into = await Entry(Users.Player, campaign.Id, "Gundren Rockseeker");
        var from = await Entry(Users.Player, campaign.Id, "Gundren");
        var before = await ImageNote(Users.Player, campaign.Id, $"Portrait of {Mention(from)}");
        var direct = await ImageNote(Users.Outsider, campaign.Id, $"And {Mention(into)}");

        fixture.LoginAsUser(Users.DM);
        (await fixture.PostEntryMerge(campaign.Id, from.Id, into.Id)).Should().Succeed();

        NoteIds(await EntryGallery(Users.Outsider, campaign.Id, into.Id)).Should().Equal(before.Id, direct.Id);
    }

    [Fact]
    public async Task TheEntryGallery_OfAnEntryTheCallerCannotSee_IsA404()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry gallery secret");
        var secret = await Entry(Users.DM, campaign.Id, "The Black Spider", Visibility.DM);
        await ImageNote(Users.DM, campaign.Id, $"Who is {Mention(secret)}?");

        (await EntryGallery(Users.DM, campaign.Id, secret.Id)).Items.Should().HaveCount(1);
        fixture.LoginAsUser(Users.Player);
        (await fixture.GetStatus(GalleryUrl(campaign.Id, "entries", secret.Id))).Should().Be(404);
        (await fixture.GetStatus(GalleryUrl(campaign.Id, "entries", Guid.NewGuid()))).Should().Be(404);
    }

    [Fact]
    public async Task TheEntryGallery_Pages()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry gallery paging", withSecondPlayer: false);
        var entry = await Entry(Users.Player, campaign.Id, "Phandalin");
        var notes = new List<SessionNoteResponse>();
        for (var i = 0; i < 3; i++)
        {
            notes.Add(await ImageNote(Users.Player, campaign.Id, $"{Mention(entry)} {i}"));
        }

        var newest = await EntryGallery(Users.Player, campaign.Id, entry.Id, take: 2);
        NoteIds(newest).Should().Equal(notes[1].Id, notes[2].Id);
        newest.HasOlder.Should().BeTrue();
        var oldest = await EntryGallery(Users.Player, campaign.Id, entry.Id, newest.Items[0].Note.PostedAt, take: 2);
        NoteIds(oldest).Should().Equal(notes[0].Id);
        oldest.HasOlder.Should().BeFalse();
    }
}
