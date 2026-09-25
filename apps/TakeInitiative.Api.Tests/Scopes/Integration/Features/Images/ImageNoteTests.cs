using CSharpFunctionalExtensions;
using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Images;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Images;

/// <summary>
/// Step 16b.7: notes with images. The author is <see cref="Users.Player"/>; the DM is
/// <see cref="Users.DM"/> and the other player <see cref="Users.Outsider"/>, joined by code.
/// </summary>
public class ImageNoteTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private IDocumentStore Store => fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();
    private ImageSweeper Sweeper => fixture.AlbaHost.Services.GetRequiredService<ImageSweeper>();

    private static string NotesUrl(Guid campaignId) => $"/api/campaigns/{campaignId}/notes";

    private async Task<Image?> Load(Guid imageId)
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<Image>(imageId);
    }

    private bool HasBlobs(Guid campaignId, Guid imageId)
        => fixture.Blobs.Contains(Image.BlobKey(campaignId, imageId, "display"))
            || fixture.Blobs.Contains(Image.BlobKey(campaignId, imageId, "thumb"));

    private async Task<int> Thumb(Guid campaignId, Guid imageId)
        => (await fixture.GetImageVariant(campaignId, imageId, "thumb")).Context.Response.StatusCode;

    [Fact]
    public async Task ANoteWithTwoImagesAndACaption_CarriesThemInOrder_AndAttachesThem()
    {
        var campaign = await TestCampaign.Create(fixture, "Two images");
        fixture.LoginAsUser(Users.Player);
        var wide = await fixture.UploadFixture(campaign.Id, ImageFixtures.Wide);
        var webp = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        var posted = await fixture.PostImageNote(campaign.Id, "  The map  ", [wide.Id, webp.Id]);

        posted.Should().Succeed();
        posted.Value.Text.Should().Be("The map");
        posted.Value.Images.Select(i => (i.Id, i.Width, i.Height)).Should().Equal(
            (wide.Id, wide.Width, wide.Height), (webp.Id, webp.Width, webp.Height));
        foreach (var id in new[] { wide.Id, webp.Id })
        {
            var image = (await Load(id))!;
            image.NoteId.Should().Be(posted.Value.Id);
            image.AttachedAt.Should().NotBeNull();
            image.AttachedAt!.Value.Ticks.Should().Be(image.AttachedAt.Value.Ticks - image.AttachedAt.Value.Ticks % 10);
        }

        var events = await TestCampaign.EventsOf(fixture, posted.Value.Id);
        events.Single().Data.Should().BeOfType<SessionNotePosted>()
            .Which.Images.Should().Equal(new NoteImage(wide.Id, wide.Width, wide.Height), new NoteImage(webp.Id, webp.Width, webp.Height));

        (await fixture.GetSessionNote(campaign.Id, posted.Value.Id)).Value.Note.Images.Select(i => i.Id).Should().Equal(wide.Id, webp.Id);
        var inStream = (await fixture.GetSessionStream(campaign.Id)).Value.Sessions.Single().Notes.Single();
        inStream.Images.Select(i => i.Id).Should().Equal(wide.Id, webp.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ANoteWithImagesAndNoText_IsPosted(string text)
    {
        var campaign = await TestCampaign.Create(fixture, $"No caption '{text}'");
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        var posted = await fixture.PostImageNote(campaign.Id, text, [image.Id]);

        posted.Should().Succeed();
        posted.Value.Text.Should().BeEmpty();
        posted.Value.Images.Should().ContainSingle().Which.Id.Should().Be(image.Id);
    }

    [Fact]
    public async Task ANoteWithNeitherTextNorImages_IsA400()
    {
        var campaign = await TestCampaign.Create(fixture, "Neither", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);

        foreach (var imageIds in new[] { null, Array.Empty<Guid>() })
        {
            var (status, body) = await fixture.Send(HttpMethod.Post, NotesUrl(campaign.Id),
                new { text = "  ", visibility = "Everyone", isRecap = false, imageIds });
            status.Should().Be(400);
            body.Should().Contain("\"text\"").And.Contain(SessionNoteTextRule.NeedsTextMessage);
        }
    }

    [Fact]
    public async Task ElevenImages_OrTheSameImageTwice_IsA400()
    {
        var campaign = await TestCampaign.Create(fixture, "Too many", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        var eleven = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray();
        var (status, body) = await fixture.Send(HttpMethod.Post, NotesUrl(campaign.Id),
            new { text = "Too many", visibility = "Everyone", isRecap = false, imageIds = eleven });
        status.Should().Be(400);
        body.Should().Contain("\"imageIds\"").And.Contain("at most 10 images");

        (status, body) = await fixture.Send(HttpMethod.Post, NotesUrl(campaign.Id),
            new { text = "Twice", visibility = "Everyone", isRecap = false, imageIds = new[] { image.Id, image.Id } });
        status.Should().Be(400);
        body.Should().Contain("\"imageIds\"").And.Contain("twice");
        (await Load(image.Id))!.NoteId.Should().BeNull();
    }

    [Fact]
    public async Task AnImageThatIsNotTheCallersToAttach_IsA400_WithOneMessageForEveryCase()
    {
        var campaign = await TestCampaign.Create(fixture, "Not yours");
        var other = await TestCampaign.Create(fixture, "Not yours, elsewhere", withSecondPlayer: false);

        fixture.LoginAsUser(Users.Outsider);
        var othersUpload = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        fixture.LoginAsUser(Users.Player);
        var otherCampaigns = await fixture.UploadFixture(other.Id, ImageFixtures.Webp);
        var deleted = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        (await fixture.DeleteImage(campaign.Id, deleted.Id)).Should().Be(204);
        var onANote = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        (await fixture.PostImageNote(campaign.Id, "First", [onANote.Id])).Should().Succeed();
        var mine = await fixture.UploadFixture(campaign.Id, ImageFixtures.Alpha);

        var bodies = new List<string>();
        foreach (var id in new[] { othersUpload.Id, otherCampaigns.Id, deleted.Id, onANote.Id, Guid.NewGuid() })
        {
            // A good image beside the bad one is not attached either.
            var (status, body) = await fixture.Send(HttpMethod.Post, NotesUrl(campaign.Id),
                new { text = "Stolen", visibility = "Everyone", isRecap = false, imageIds = new[] { mine.Id, id } });
            status.Should().Be(400);
            body.Should().Contain("\"imageIds\"").And.Contain(ImageAttachments.UnavailableMessage);
            bodies.Add(System.Text.RegularExpressions.Regex.Replace(body, "\"traceId\":\"[^\"]*\"", ""));
        }
        bodies.Distinct().Should().ContainSingle("every case gives the same answer");

        (await Load(mine.Id))!.NoteId.Should().BeNull();
        (await Load(othersUpload.Id))!.NoteId.Should().BeNull();
        (await fixture.GetSessionStream(campaign.Id)).Value.Sessions.Single().Notes.Select(n => n.Text).Should().Equal("First");
    }

    [Fact]
    public async Task AnEdit_AddsRemovesAndReorders_AndARemovedImageIsDeletedForReal()
    {
        var campaign = await TestCampaign.Create(fixture, "Edit images");
        fixture.LoginAsUser(Users.Player);
        var a = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        var b = await fixture.UploadFixture(campaign.Id, ImageFixtures.Alpha);
        var c = await fixture.UploadFixture(campaign.Id, ImageFixtures.Wide);
        var note = (await fixture.PostImageNote(campaign.Id, "Maps", [a.Id, b.Id])).Value;

        var edited = await fixture.PutImageNote(campaign.Id, note.Id, "Maps", [c.Id, a.Id]);

        edited.Should().Succeed();
        edited.Value.Images.Select(i => i.Id).Should().Equal(c.Id, a.Id);
        (await Load(c.Id))!.NoteId.Should().Be(note.Id);
        (await Load(a.Id))!.NoteId.Should().Be(note.Id);
        (await Load(b.Id)).Should().BeNull();
        HasBlobs(campaign.Id, b.Id).Should().BeFalse();
        (await Thumb(campaign.Id, b.Id)).Should().Be(404);
        HasBlobs(campaign.Id, a.Id).Should().BeTrue();

        // Reordering alone is an edit; the same list and text append nothing; null keeps them.
        (await fixture.PutImageNote(campaign.Id, note.Id, "Maps", [a.Id, c.Id])).Value.Images.Select(i => i.Id).Should().Equal(a.Id, c.Id);
        (await fixture.PutImageNote(campaign.Id, note.Id, "Maps", [a.Id, c.Id])).Should().Succeed();
        (await fixture.PutImageNote(campaign.Id, note.Id, "", null)).Value.Images.Select(i => i.Id).Should().Equal(a.Id, c.Id);

        var edits = (await TestCampaign.EventsOf(fixture, note.Id)).Skip(1).Select(e => (SessionNoteEdited)e.Data).ToList();
        edits.Select(e => e.Images?.Select(i => i.ImageId).ToArray()).Should().BeEquivalentTo(
            new Guid[]?[] { [c.Id, a.Id], [a.Id, c.Id], null }, o => o.WithStrictOrdering());

        var history = (await fixture.GetSessionNoteHistory(campaign.Id, note.Id)).Value.Versions;
        history.Select(v => v.ImageCount).Should().Equal(2, 2, 2, 2);
        history.Select(v => v.Text).Should().Equal("Maps", "Maps", "Maps", "");

        // With no text left, the last image cannot go.
        var (status, body) = await fixture.Send(HttpMethod.Put, $"{NotesUrl(campaign.Id)}/{note.Id}", new { text = "", isRecap = false, imageIds = Array.Empty<Guid>() });
        status.Should().Be(400);
        body.Should().Contain(SessionNoteTextRule.NeedsTextMessage);
        (await fixture.PutImageNote(campaign.Id, note.Id, "Just text now", [])).Value.Images.Should().BeEmpty();
        (await Load(a.Id)).Should().BeNull();
        (await Load(c.Id)).Should().BeNull();
        (await fixture.GetSessionNoteHistory(campaign.Id, note.Id)).Value.Versions.Select(v => v.ImageCount).Should().Equal(2, 2, 2, 2, 0);
    }

    [Fact]
    public async Task ARemovedImage_WhoseBlobDeleteFails_Is404AtOnce_AndGoneAfterASweep()
    {
        var campaign = await TestCampaign.Create(fixture, "Edit retry", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var a = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        var b = await fixture.UploadFixture(campaign.Id, ImageFixtures.Alpha);
        var note = (await fixture.PostImageNote(campaign.Id, "Two", [a.Id, b.Id])).Value;

        fixture.Blobs.FailNextDeletes(1);
        (await fixture.PutImageNote(campaign.Id, note.Id, "Two", [a.Id])).Should().Succeed();

        (await Load(b.Id))!.DeletedAt.Should().NotBeNull();
        (await Thumb(campaign.Id, b.Id)).Should().Be(404);
        HasBlobs(campaign.Id, b.Id).Should().BeTrue();

        await Sweeper.SweepOnce(fixture.Clock.GetUtcNow());
        (await Load(b.Id)).Should().BeNull();
        HasBlobs(campaign.Id, b.Id).Should().BeFalse();
        (await Thumb(campaign.Id, a.Id)).Should().Be(200);
    }

    [Fact]
    public async Task OnlyTheAuthor_ChangesANotesImages()
    {
        var campaign = await TestCampaign.Create(fixture, "Author only", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        var note = (await fixture.PostImageNote(campaign.Id, "Mine", [image.Id])).Value;

        fixture.LoginAsUser(Users.DM);
        await fixture.ExpectStatus(HttpMethod.Put, $"{NotesUrl(campaign.Id)}/{note.Id}", new { text = "Mine", isRecap = false, imageIds = Array.Empty<Guid>() }, 403);
        await fixture.ExpectStatus(HttpMethod.Delete, $"{NotesUrl(campaign.Id)}/{note.Id}", null, 403);
        (await Thumb(campaign.Id, image.Id)).Should().Be(200);
        // An image on a note is removed by editing the note.
        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteImage(campaign.Id, image.Id)).Should().Be(409);
    }

    [Fact]
    public async Task DeletingANote_DeletesItsImages()
    {
        var campaign = await TestCampaign.Create(fixture, "Delete note");
        fixture.LoginAsUser(Users.Player);
        var a = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        var b = await fixture.UploadFixture(campaign.Id, ImageFixtures.Alpha);
        var note = (await fixture.PostImageNote(campaign.Id, "Going", [a.Id, b.Id])).Value;

        (await fixture.DeleteSessionNote(campaign.Id, note.Id)).Should().Succeed();

        foreach (var id in new[] { a.Id, b.Id })
        {
            (await Load(id)).Should().BeNull();
            HasBlobs(campaign.Id, id).Should().BeFalse();
            (await Thumb(campaign.Id, id)).Should().Be(404);
        }
        // The note's events keep only the ids.
        (await TestCampaign.EventsOf(fixture, note.Id)).First().Data.Should().BeOfType<SessionNotePosted>()
            .Which.Images!.Select(i => i.ImageId).Should().Equal(a.Id, b.Id);
    }

    [Fact]
    public async Task TheNotesEvent_AndTheImageAttach_ShareACorrelationId()
    {
        var campaign = await TestCampaign.Create(fixture, "Correlation", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var a = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        var b = await fixture.UploadFixture(campaign.Id, ImageFixtures.Alpha);
        var note = (await fixture.PostImageNote(campaign.Id, "Posted", [a.Id])).Value;

        await using (var session = Store.QuerySession())
        {
            var posted = (await TestCampaign.EventsOf(fixture, note.Id)).Single();
            var image = (await session.LoadAsync<Image>(a.Id))!;
            (await session.MetadataForAsync(image))!.CorrelationId.Should().NotBeNullOrWhiteSpace().And.Be(posted.CorrelationId);
        }

        (await fixture.PutImageNote(campaign.Id, note.Id, "Posted", [a.Id, b.Id])).Should().Succeed();
        await using (var session = Store.QuerySession())
        {
            var edited = (await TestCampaign.EventsOf(fixture, note.Id)).Last();
            var image = (await session.LoadAsync<Image>(b.Id))!;
            (await session.MetadataForAsync(image))!.CorrelationId.Should().Be(edited.CorrelationId);
            edited.CorrelationId.Should().NotBe((await TestCampaign.EventsOf(fixture, note.Id)).First().CorrelationId);
        }
    }

    [Fact]
    public async Task AnImageNoteWithNoMention_IsTheLooseEndsSeam()
    {
        var campaign = await TestCampaign.Create(fixture, "Loose ends", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var untagged = (await fixture.PostImageNote(campaign.Id, "", [(await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp)).Id])).Value;
        var entryId = Guid.NewGuid();
        await fixture.PostImageNote(campaign.Id, $"Map of @[Cragmaw](entry:{entryId})", [(await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp)).Id]);
        await fixture.PostSessionNote(campaign.Id, "Just text");

        await using var session = Store.QuerySession();
        var found = await session.Query<SessionNote>()
            .Where(n => n.CampaignId == campaign.Id)
            .Where(SessionNote.UntaggedImageNote)
            .ToListAsync();
        found.Select(n => n.Id).Should().Equal(untagged.Id);
    }

    [Fact]
    public async Task ANoteWithNoCaption_CannotBeQuoted()
    {
        var campaign = await TestCampaign.Create(fixture, "Quote image", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var note = (await fixture.PostImageNote(campaign.Id, "", [(await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp)).Id])).Value;
        var entry = (await fixture.PostEntry(campaign.Id, "Cragmaw Hideout", EntryKind.Place)).Value;

        var result = await fixture.PostEntryQuote(campaign.Id, entry.Id, note.Id);
        result.Should().Fail();
    }
}

/// <summary>Step 16b.7: two notes racing for one upload (two tabs), made deterministic.</summary>
public class ImageAttachRaceTests(InterferingSaveFixture fixture) : IClassFixture<InterferingSaveFixture>
{
    [Fact]
    public async Task TwoPostsWithTheSameUpload_GiveOne200AndOne409()
    {
        var campaign = await TestCampaign.Create(fixture, "Attach race", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);

        // The other tab's post runs, and commits, just before this one saves: both passed the check.
        var other = Result.Failure<SessionNoteResponse>("The other tab never posted.");
        fixture.Interference.BeforeNextSave(async _ => other = await fixture.PostImageNote(campaign.Id, "Tab two", [image.Id]));
        var (status, body) = await fixture.Send(HttpMethod.Post, $"/api/campaigns/{campaign.Id}/notes",
            new { text = "Tab one", visibility = "Everyone", isRecap = false, imageIds = new[] { image.Id } });

        fixture.Interference.Pending.Should().Be(0);
        other.Should().Succeed();
        status.Should().Be(409);
        body.Should().Contain("\"imageIds\"").And.Contain(ImageAttachments.ConflictMessage);

        // The loser's note was never written: its event and the image change share one transaction.
        var notes = (await fixture.GetSessionStream(campaign.Id)).Value.Sessions.Single().Notes;
        notes.Should().ContainSingle().Which.Id.Should().Be(other.Value.Id);
        await using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        (await session.LoadAsync<Image>(image.Id))!.NoteId.Should().Be(other.Value.Id);
    }

    [Fact]
    public async Task ThePushedNote_CarriesItsImages()
    {
        var campaign = await TestCampaign.Create(fixture, "Push images", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        var mark = fixture.Hub.Messages.Count;

        var note = (await fixture.PostImageNote(campaign.Id, "Pushed", [image.Id], Visibility.DM)).Value;

        var upserted = fixture.Hub.Messages.Skip(mark).Single(m => m.Method == CampaignHubMessages.SessionNoteUpserted);
        upserted.Payload.Should().BeOfType<SessionNoteResponse>().Which.Images.Should().ContainSingle()
            .Which.Should().Be(new NoteImageResponse { Id = image.Id, Width = image.Width, Height = image.Height });
        upserted.Groups.Should().NotContain(TakeInitiative.Api.Features.Campaigns.CampaignGroups.Campaign(campaign.Id), "a DM note is not pushed to the players");
        note.Images.Should().ContainSingle();
    }
}
