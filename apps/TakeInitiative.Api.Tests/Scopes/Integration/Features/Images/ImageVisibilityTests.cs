using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Images;

/// <summary>
/// Step 16b.7, invariant 5 for images: an image on a note is served to exactly the note's
/// audience, read from the note on every request. The author is <see cref="Users.Player"/>,
/// the DM is <see cref="Users.DM"/> and the other player is <see cref="Users.Outsider"/>.
/// </summary>
public class ImageVisibilityTests(AuthenticatedWebAppWithDatabaseFixture fixture)
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

    private async Task<int> Status(Guid campaignId, Guid imageId, string variant = "thumb", bool withEtag = false)
        => (await fixture.GetImageVariant(campaignId, imageId, variant, withEtag ? $"\"{imageId}-{variant}\"" : null))
            .Context.Response.StatusCode;

    private async Task<(TestCampaign Campaign, Guid NoteId, Guid ImageId)> PostImageNote(string name, Visibility visibility)
    {
        var campaign = await TestCampaign.Create(fixture, name);
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        var note = (await fixture.PostImageNote(campaign.Id, "A picture", [image.Id], visibility)).Value;
        return (campaign, note.Id, image.Id);
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
    [InlineData(Visibility.DM, true, Viewer.Author, true)]
    [InlineData(Visibility.DM, true, Viewer.Dm, true)]
    [InlineData(Visibility.DM, true, Viewer.OtherPlayer, false)]
    [InlineData(Visibility.Me, false, Viewer.Author, true)]
    [InlineData(Visibility.Me, false, Viewer.Dm, false)]
    [InlineData(Visibility.Me, false, Viewer.OtherPlayer, false)]
    public async Task TheImage_IsServedExactlyWhenTheNoteIs(Visibility visibility, bool hidden, Viewer viewer, bool canSee)
    {
        var (campaign, noteId, imageId) = await PostImageNote($"Image {visibility} {hidden} {viewer}", visibility);
        if (hidden)
        {
            fixture.LoginAsUser(Users.DM);
            (await fixture.PutSessionNoteHidden(campaign.Id, noteId, true)).Should().Succeed();
        }

        fixture.LoginAsUser(UserFor(viewer));
        var noteStatus = (await fixture.GetSessionNote(campaign.Id, noteId)).IsSuccess ? 200 : 404;
        noteStatus.Should().Be(canSee ? 200 : 404);
        foreach (var variant in new[] { "thumb", "display" })
        {
            (await Status(campaign.Id, imageId, variant)).Should().Be(noteStatus);
            // "Not modified" only after the check: an ETag never turns a 404 into a 304.
            (await Status(campaign.Id, imageId, variant, withEtag: true)).Should().Be(canSee ? 304 : 404);
        }
    }

    [Fact]
    public async Task AVisibilityChange_RevokesThePlayersImage_AtTheNextRequest_EvenWithItsEtag()
    {
        var (campaign, noteId, imageId) = await PostImageNote("Narrowed", Visibility.Everyone);
        fixture.LoginAsUser(Users.Outsider);
        (await Status(campaign.Id, imageId)).Should().Be(200);

        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionNoteVisibility(campaign.Id, noteId, Visibility.DM)).Should().Succeed();
        fixture.LoginAsUser(Users.Outsider);
        (await Status(campaign.Id, imageId)).Should().Be(404);
        (await Status(campaign.Id, imageId, withEtag: true)).Should().Be(404);
        fixture.LoginAsUser(Users.DM);
        (await Status(campaign.Id, imageId)).Should().Be(200);

        fixture.LoginAsUser(Users.Player);
        (await fixture.PutSessionNoteVisibility(campaign.Id, noteId, Visibility.Me)).Should().Succeed();
        fixture.LoginAsUser(Users.DM);
        (await Status(campaign.Id, imageId, withEtag: true)).Should().Be(404);
        fixture.LoginAsUser(Users.Player);
        (await Status(campaign.Id, imageId, withEtag: true)).Should().Be(304);

        (await fixture.PutSessionNoteVisibility(campaign.Id, noteId, Visibility.Everyone)).Should().Succeed();
        fixture.LoginAsUser(Users.Outsider);
        (await Status(campaign.Id, imageId, withEtag: true)).Should().Be(304);
    }

    [Fact]
    public async Task HidingRevokesThePlayersImage_AndUnhidingRestoresIt()
    {
        var (campaign, noteId, imageId) = await PostImageNote("Hidden", Visibility.Everyone);
        fixture.LoginAsUser(Users.Outsider);
        (await Status(campaign.Id, imageId)).Should().Be(200);

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, noteId, true)).Should().Succeed();
        fixture.LoginAsUser(Users.Outsider);
        (await Status(campaign.Id, imageId)).Should().Be(404);
        (await Status(campaign.Id, imageId, withEtag: true)).Should().Be(404);
        fixture.LoginAsUser(Users.Player);
        (await Status(campaign.Id, imageId)).Should().Be(200);

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, noteId, false)).Should().Succeed();
        fixture.LoginAsUser(Users.Outsider);
        (await Status(campaign.Id, imageId)).Should().Be(200);
        (await Status(campaign.Id, imageId, withEtag: true)).Should().Be(304);
    }

    [Fact]
    public async Task ADeletedNotesImage_Is404ForEveryone_EvenWithItsEtag()
    {
        var (campaign, noteId, imageId) = await PostImageNote("Deleted", Visibility.Everyone);
        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteSessionNote(campaign.Id, noteId)).Should().Succeed();

        foreach (var user in new[] { Users.Player, Users.DM, Users.Outsider })
        {
            fixture.LoginAsUser(user);
            (await Status(campaign.Id, imageId)).Should().Be(404);
            (await Status(campaign.Id, imageId, withEtag: true)).Should().Be(404);
        }
    }

    [Fact]
    public async Task AnImageFromAnotherCampaignsUrl_IsA404()
    {
        var (campaign, _, imageId) = await PostImageNote("Cross campaign", Visibility.Everyone);
        var other = await TestCampaign.Create(fixture, "Cross campaign, other", withSecondPlayer: false);
        fixture.LoginAsUser(Users.Player);
        (await Status(other.Id, imageId)).Should().Be(404);
        (await Status(campaign.Id, imageId)).Should().Be(200);
    }
}
