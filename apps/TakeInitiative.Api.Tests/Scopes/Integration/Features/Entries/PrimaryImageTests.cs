using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Images;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// An entry's primary image (SAM-12): who sets it, which images may be one, and the clear that
/// runs when a note stops being one everyone can see. <see cref="Users.DM"/> is the DM,
/// <see cref="Users.Player"/> and <see cref="Users.Outsider"/> the players.
/// </summary>
public class PrimaryImageTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private static string Mention(EntryResponse entry) => $"@[{entry.Name}](entry:{entry.Id})";

    private async Task<EntryResponse> Entry(Users user, Guid campaignId, string name, Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.PostEntry(campaignId, name, EntryKind.Character, visibility);
        entry.Should().Succeed();
        return entry.Value;
    }

    /// <summary>An image note whose caption mentions <paramref name="mentions"/>, and the one image on it.</summary>
    private async Task<(SessionNoteResponse Note, Guid ImageId)> ImageNote(
        Users user, Guid campaignId, EntryResponse? mentions, Visibility visibility = Visibility.Everyone, string prefix = "Look at")
    {
        fixture.LoginAsUser(user);
        var image = await fixture.UploadFixture(campaignId, ImageFixtures.Webp);
        var caption = mentions is null ? prefix : $"{prefix} {Mention(mentions)}";
        var note = await fixture.PostImageNote(campaignId, caption, [image.Id], visibility);
        note.Should().Succeed();
        return (note.Value, image.Id);
    }

    private async Task<EntryResponse> Set(Users user, Guid campaignId, Guid entryId, Guid? imageId)
    {
        fixture.LoginAsUser(user);
        var saved = await fixture.PutEntryPrimaryImage(campaignId, entryId, imageId);
        saved.Should().Succeed();
        return saved.Value;
    }

    private async Task<(int Status, string Body)> Refused(Users user, Guid campaignId, Guid entryId, Guid? imageId)
    {
        fixture.LoginAsUser(user);
        return await fixture.Send(HttpMethod.Put, EntryUrl(campaignId, entryId, "primary-image"), new { imageId });
    }

    private async Task<Guid?> Read(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        return (await fixture.GetEntry(campaignId, entryId)).Value.PrimaryImageId;
    }

    private async Task<Guid?> ReadFromList(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        var list = await fixture.GetEntries(campaignId);
        list.Should().Succeed();
        return list.Value.Entries.Single(e => e.Entry.Id == entryId).Entry.PrimaryImageId;
    }

    private async Task<IReadOnlyList<HubMessage>> Pushed(Func<Task> act)
    {
        var mark = fixture.Hub.Messages.Count;
        await act();
        return fixture.Hub.Messages.Skip(mark).ToList();
    }

    private async Task<IReadOnlyList<EntryChangeType>> HistoryTypes(Users user, Guid campaignId, Guid entryId)
    {
        fixture.LoginAsUser(user);
        var history = await fixture.GetEntryHistory(campaignId, entryId);
        history.Should().Succeed();
        return history.Value.Items.Select(i => i.Change.Type).ToList();
    }

    [Fact]
    public async Task Set_ShowsOnTheEntryAndTheList_AndNamesTheImageInHistory()
    {
        var campaign = await TestCampaign.Create(fixture, "Primary set");
        var vex = await Entry(Users.Player, campaign.Id, "Vex");
        var (_, imageId) = await ImageNote(Users.Player, campaign.Id, vex);

        (await Set(Users.Player, campaign.Id, vex.Id, imageId)).PrimaryImageId.Should().Be(imageId);

        // Everyone who can see the entry sees it: the id is not per viewer.
        (await Read(Users.DM, campaign.Id, vex.Id)).Should().Be(imageId);
        (await Read(Users.Outsider, campaign.Id, vex.Id)).Should().Be(imageId);
        (await ReadFromList(Users.Outsider, campaign.Id, vex.Id)).Should().Be(imageId);

        var history = await fixture.GetEntryHistory(campaign.Id, vex.Id);
        history.Should().Succeed();
        var row = history.Value.Items.Last();
        row.Change.Type.Should().Be(EntryChangeType.PrimaryImageSet);
        row.Change.ImageId.Should().Be(imageId);
        row.ActorMemberId.Should().Be(campaign.PlayerMemberId);
    }

    [Fact]
    public async Task Null_RemovesIt_AndAnUnchangedWritePushesNothing()
    {
        var campaign = await TestCampaign.Create(fixture, "Primary clear");
        var vex = await Entry(Users.Player, campaign.Id, "Vex");
        var (_, imageId) = await ImageNote(Users.Player, campaign.Id, vex);

        var set = await Pushed(async () => await Set(Users.Player, campaign.Id, vex.Id, imageId));
        var again = await Pushed(async () => await Set(Users.Player, campaign.Id, vex.Id, imageId));
        var cleared = await Pushed(async () => (await Set(Users.Player, campaign.Id, vex.Id, null)).PrimaryImageId.Should().BeNull());
        var clearedAgain = await Pushed(async () => await Set(Users.Player, campaign.Id, vex.Id, null));

        set.Should().ContainSingle().Which.Method.Should().Be(CampaignHubMessages.EntryUpserted);
        set[0].Payload.Should().BeOfType<EntrySummaryResponse>().Which.PrimaryImageId.Should().Be(imageId);
        again.Should().BeEmpty("the same image appends nothing");
        cleared.Should().ContainSingle();
        cleared[0].Payload.Should().BeOfType<EntrySummaryResponse>().Which.PrimaryImageId.Should().BeNull();
        clearedAgain.Should().BeEmpty("clearing nothing appends nothing");

        (await Read(Users.Player, campaign.Id, vex.Id)).Should().BeNull();
        (await HistoryTypes(Users.Player, campaign.Id, vex.Id))
            .Should().EndWith([EntryChangeType.PrimaryImageSet, EntryChangeType.PrimaryImageCleared]);
    }

    [Fact]
    public async Task OnlyALiveImageEveryoneCanSee_FromTheEntrysGallery_CanBePrimary()
    {
        var campaign = await TestCampaign.Create(fixture, "Primary guard");
        var vex = await Entry(Users.Player, campaign.Id, "Vex");
        var other = await Entry(Users.Player, campaign.Id, "Tharden");

        var (_, dmOnly) = await ImageNote(Users.DM, campaign.Id, vex, Visibility.DM);
        var (_, mine) = await ImageNote(Users.Player, campaign.Id, vex, Visibility.Me);
        var (hiddenNote, hidden) = await ImageNote(Users.Outsider, campaign.Id, vex);
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, hiddenNote.Id, true)).Should().Succeed();
        var (_, elsewhere) = await ImageNote(Users.Player, campaign.Id, other);
        var (_, unmentioned) = await ImageNote(Users.Player, campaign.Id, null);

        // Never posted, so it is on no note: an unsent draft is nobody's primary image.
        fixture.LoginAsUser(Users.Player);
        var unposted = (await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp)).Id;

        // Another campaign's image.
        var elsewhereCampaign = await TestCampaign.Create(fixture, "Primary guard other");
        var otherVex = await Entry(Users.Player, elsewhereCampaign.Id, "Vex");
        var (_, foreign) = await ImageNote(Users.Player, elsewhereCampaign.Id, otherVex);

        foreach (var (imageId, why) in new (Guid, string)[]
        {
            (dmOnly, "a DM note's image is not seen by the players"),
            (mine, "a Me note's image is its author's alone"),
            (hidden, "a hidden note's image is not seen by the players"),
            (elsewhere, "it is not in this entry's gallery"),
            (unmentioned, "its caption mentions no entry"),
            (unposted, "it is on no note"),
            (foreign, "it belongs to another campaign"),
            (Guid.NewGuid(), "there is no such image"),
        })
        {
            var (status, body) = await Refused(Users.DM, campaign.Id, vex.Id, imageId);
            status.Should().Be(400, why);
            // One message for every reason, so another member's image id tells the caller nothing.
            body.Should().Contain(EntryPrimaryImages.UnavailableMessage).And.Contain(EntryPrimaryImages.ErrorKey);
        }

        (await Read(Users.DM, campaign.Id, vex.Id)).Should().BeNull("nothing was accepted");

        // A deleted image is refused too, even though it was fine a moment ago.
        var (deletedNote, deleted) = await ImageNote(Users.Player, campaign.Id, vex);
        await Set(Users.Player, campaign.Id, vex.Id, deleted);
        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteSessionNote(campaign.Id, deletedNote.Id)).Should().Succeed();
        (await Refused(Users.DM, campaign.Id, vex.Id, deleted)).Status.Should().Be(400);
    }

    [Fact]
    public async Task OnlyWhoeverMayEditTheEntry_SetsIt()
    {
        var campaign = await TestCampaign.Create(fixture, "Primary access");
        var vex = await Entry(Users.Player, campaign.Id, "Vex");
        var (_, imageId) = await ImageNote(Users.Player, campaign.Id, vex);

        // Edit access Anyone, the default: another player may set it.
        await Set(Users.Outsider, campaign.Id, vex.Id, imageId);

        fixture.LoginAsUser(Users.Player);
        (await fixture.PutEntryEditAccess(campaign.Id, vex.Id, EditAccess.OnlyMe)).Should().Succeed();
        (await Refused(Users.Outsider, campaign.Id, vex.Id, imageId)).Status.Should().Be(403);
        // The creator and the DMs still may.
        await Set(Users.DM, campaign.Id, vex.Id, null);
        await Set(Users.Player, campaign.Id, vex.Id, imageId);

        // An entry the caller cannot see is a 404, never a 403.
        var secret = await Entry(Users.DM, campaign.Id, "The informant", Visibility.DM);
        (await Refused(Users.Outsider, campaign.Id, secret.Id, imageId)).Status.Should().Be(404);
    }

    [Fact]
    public async Task AMentionOfAMergedEntry_CountsAsTheTargetsGallery()
    {
        var campaign = await TestCampaign.Create(fixture, "Primary merge");
        var vex = await Entry(Users.Player, campaign.Id, "Vex");
        var duplicate = await Entry(Users.Player, campaign.Id, "Vex Thornwood");
        var (_, imageId) = await ImageNote(Users.Player, campaign.Id, duplicate);

        fixture.LoginAsUser(Users.Player);
        (await fixture.PostEntryMerge(campaign.Id, duplicate.Id, vex.Id)).Should().Succeed();

        // The caption still names the merged id, and the gallery resolves it (16d), so it may be primary.
        (await Set(Users.Player, campaign.Id, vex.Id, imageId)).PrimaryImageId.Should().Be(imageId);
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("dm")]
    [InlineData("me")]
    [InlineData("deleted")]
    [InlineData("edited out")]
    public async Task WhenTheNoteStopsBeingPublic_ThePrimaryImageIsCleared(string how)
    {
        var campaign = await TestCampaign.Create(fixture, $"Primary drift {how}");
        var vex = await Entry(Users.Player, campaign.Id, "Vex");
        var (note, imageId) = await ImageNote(Users.Player, campaign.Id, vex);
        await Set(Users.Player, campaign.Id, vex.Id, imageId);

        var pushed = await Pushed(async () =>
        {
            switch (how)
            {
                case "hidden":
                    fixture.LoginAsUser(Users.DM);
                    (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();
                    break;
                case "dm":
                    fixture.LoginAsUser(Users.Player);
                    (await fixture.PutSessionNoteVisibility(campaign.Id, note.Id, Visibility.DM)).Should().Succeed();
                    break;
                case "me":
                    fixture.LoginAsUser(Users.Player);
                    (await fixture.PutSessionNoteVisibility(campaign.Id, note.Id, Visibility.Me)).Should().Succeed();
                    break;
                case "deleted":
                    fixture.LoginAsUser(Users.Player);
                    (await fixture.DeleteSessionNote(campaign.Id, note.Id)).Should().Succeed();
                    break;
                case "edited out":
                    fixture.LoginAsUser(Users.Player);
                    (await fixture.PutImageNote(campaign.Id, note.Id, $"Still about {Mention(vex)}", [])).Should().Succeed();
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(how), how, null);
            }
        });

        (await Read(Users.DM, campaign.Id, vex.Id)).Should().BeNull($"the image is no longer one everyone can see ({how})");
        (await ReadFromList(Users.Outsider, campaign.Id, vex.Id)).Should().BeNull();
        (await HistoryTypes(Users.Player, campaign.Id, vex.Id)).Should().EndWith([EntryChangeType.PrimaryImageCleared]);

        // The clear reaches every open client, so no card keeps serving an id it cannot fetch.
        var summaries = pushed
            .Where(m => m.Method == CampaignHubMessages.EntryUpserted)
            .Select(m => m.Payload)
            .OfType<EntrySummaryResponse>()
            .Where(s => s.Id == vex.Id)
            .ToList();
        summaries.Should().ContainSingle().Which.PrimaryImageId.Should().BeNull();
    }

    [Fact]
    public async Task UnhidingTheNote_DoesNotBringThePrimaryImageBack()
    {
        var campaign = await TestCampaign.Create(fixture, "Primary unhide");
        var vex = await Entry(Users.Player, campaign.Id, "Vex");
        var (note, imageId) = await ImageNote(Users.Player, campaign.Id, vex);
        await Set(Users.Player, campaign.Id, vex.Id, imageId);

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, true)).Should().Succeed();
        (await fixture.PutSessionNoteHidden(campaign.Id, note.Id, false)).Should().Succeed();

        (await Read(Users.DM, campaign.Id, vex.Id)).Should().BeNull("the pick was explicit, so restoring it is too");
        // And it can be picked again, because the image is public once more.
        (await Set(Users.Player, campaign.Id, vex.Id, imageId)).PrimaryImageId.Should().Be(imageId);
    }

    [Fact]
    public async Task AnEditThatKeepsTheImage_LeavesThePrimaryImageAlone()
    {
        var campaign = await TestCampaign.Create(fixture, "Primary edit keeps");
        var vex = await Entry(Users.Player, campaign.Id, "Vex");
        var (note, imageId) = await ImageNote(Users.Player, campaign.Id, vex);
        await Set(Users.Player, campaign.Id, vex.Id, imageId);

        fixture.LoginAsUser(Users.Player);
        // The caption changes and the image list is left out, so the images are kept.
        (await fixture.PutImageNote(campaign.Id, note.Id, $"A better caption for {Mention(vex)}", null)).Should().Succeed();
        (await Read(Users.DM, campaign.Id, vex.Id)).Should().Be(imageId);

        // Rewording the caption so it no longer mentions the entry does not clear it either:
        // the image is still public, and eligibility is checked when it is picked.
        fixture.LoginAsUser(Users.Player);
        (await fixture.PutImageNote(campaign.Id, note.Id, "No mention at all", null)).Should().Succeed();
        (await Read(Users.DM, campaign.Id, vex.Id)).Should().Be(imageId);
    }

    [Fact]
    public async Task ADeletedImagesEntry_IsClearedEvenWhenAnotherNoteKeepsItsOwn()
    {
        var campaign = await TestCampaign.Create(fixture, "Primary two entries");
        var vex = await Entry(Users.Player, campaign.Id, "Vex");
        var tharden = await Entry(Users.Player, campaign.Id, "Tharden");

        // One note, one image, mentioning both entries: both may use it, and both lose it.
        fixture.LoginAsUser(Users.Player);
        var image = await fixture.UploadFixture(campaign.Id, ImageFixtures.Webp);
        var shared = await fixture.PostImageNote(campaign.Id, $"{Mention(vex)} and {Mention(tharden)}", [image.Id]);
        shared.Should().Succeed();
        await Set(Users.Player, campaign.Id, vex.Id, image.Id);
        await Set(Users.Player, campaign.Id, tharden.Id, image.Id);

        // And a second entry whose image is on a different note, which is untouched.
        var keeps = await Entry(Users.Player, campaign.Id, "Ember Court");
        var (_, keptImage) = await ImageNote(Users.Player, campaign.Id, keeps);
        await Set(Users.Player, campaign.Id, keeps.Id, keptImage);

        fixture.LoginAsUser(Users.Player);
        (await fixture.DeleteSessionNote(campaign.Id, shared.Value.Id)).Should().Succeed();

        (await Read(Users.DM, campaign.Id, vex.Id)).Should().BeNull();
        (await Read(Users.DM, campaign.Id, tharden.Id)).Should().BeNull();
        (await Read(Users.DM, campaign.Id, keeps.Id)).Should().Be(keptImage, "its own note is untouched");
    }
}
