using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Invariant 5 and 4 for entries: every cell of the visibility table (step 15a.4) on every
/// read and on a write, and every row of the permission table (15a.5) under both edit
/// access values. The creator is <see cref="Users.Player"/>, the DM is <see cref="Users.DM"/>
/// and the other player is <see cref="Users.Outsider"/>, joined by code.
/// </summary>
public class EntryVisibilityTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    public enum Viewer { Creator, Dm, OtherPlayer }

    private static Users UserFor(Viewer viewer) => viewer switch
    {
        Viewer.Creator => Users.Player,
        Viewer.Dm => Users.DM,
        Viewer.OtherPlayer => Users.Outsider,
        _ => throw new ArgumentOutOfRangeException(nameof(viewer)),
    };

    private static string Url(Guid campaignId, Guid entryId, string? part = null)
        => $"/api/campaigns/{campaignId}/entries/{entryId}" + (part is null ? "" : $"/{part}");

    [Theory]
    [InlineData(Visibility.Everyone, Viewer.Creator, true)]
    [InlineData(Visibility.Everyone, Viewer.Dm, true)]
    [InlineData(Visibility.Everyone, Viewer.OtherPlayer, true)]
    [InlineData(Visibility.DM, Viewer.Creator, true)]
    [InlineData(Visibility.DM, Viewer.Dm, true)]
    [InlineData(Visibility.DM, Viewer.OtherPlayer, false)]
    [InlineData(Visibility.Me, Viewer.Creator, true)]
    [InlineData(Visibility.Me, Viewer.Dm, false)]
    [InlineData(Visibility.Me, Viewer.OtherPlayer, false)]
    public async Task EveryRead_AndAWrite_FollowTheVisibilityTable(Visibility visibility, Viewer viewer, bool canSee)
    {
        var campaign = await TestCampaign.Create(fixture, $"Entry visibility {visibility} {viewer}");
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Glasstaff", EntryKind.Character, visibility)).Value;

        fixture.LoginAsUser(UserFor(viewer));
        var list = await fixture.GetEntries(campaign.Id);
        list.Should().Succeed();

        if (canSee)
        {
            list.Value.Entries.Should().ContainSingle().Which.Entry.Id.Should().Be(entry.Id);
            (await fixture.GetEntry(campaign.Id, entry.Id)).Should().Succeed();
            (await fixture.PutEntryKind(campaign.Id, entry.Id, EntryKind.Other)).Should().Succeed();
        }
        else
        {
            // Hidden things are absent: left out of the list, and a 404 (never a 403) on
            // a read and on every write, before any permission check.
            list.Value.Entries.Should().BeEmpty();
            await fixture.ExpectStatus(HttpMethod.Get, Url(campaign.Id, entry.Id), null, 404);
            await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "kind"), new { kind = "Other" }, 404);
            await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "visibility"), new { visibility = "Everyone" }, 404);
        }
    }

    [Theory]
    [InlineData(EditAccess.Anyone, Viewer.Creator, true, true)]
    [InlineData(EditAccess.Anyone, Viewer.Dm, true, true)]
    [InlineData(EditAccess.Anyone, Viewer.OtherPlayer, true, false)]
    [InlineData(EditAccess.OnlyMe, Viewer.Creator, true, true)]
    [InlineData(EditAccess.OnlyMe, Viewer.Dm, true, true)]
    [InlineData(EditAccess.OnlyMe, Viewer.OtherPlayer, false, false)]
    public async Task EveryChange_FollowsThePermissionTable(EditAccess editAccess, Viewer viewer, bool canEdit, bool canChangeAccess)
    {
        var campaign = await TestCampaign.Create(fixture, $"Entry permissions {editAccess} {viewer}");
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;
        (await fixture.PutEntryEditAccess(campaign.Id, entry.Id, editAccess)).Should().Succeed();

        fixture.LoginAsUser(UserFor(viewer));
        var edit = canEdit ? 200 : 403;
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "name"), new { name = $"Gundren by {viewer}" }, edit);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "kind"), new { kind = "Place" }, edit);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "aliases"), new { aliases = new[] { $"Alias by {viewer}" } }, edit);

        // Access changes are checked even when the value is unchanged.
        var access = canChangeAccess ? 200 : 403;
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "visibility"), new { visibility = "Everyone" }, access);
        await fixture.ExpectStatus(HttpMethod.Put, Url(campaign.Id, entry.Id, "edit-access"), new { editAccess = editAccess.ToString() }, access);

        var events = await TestCampaign.EventsOf(fixture, entry.Id);
        events.Count(e => e.Data is EntryRenamed or EntryKindChanged or EntryAliasAdded).Should().Be(canEdit ? 3 : 0);
    }

    [Fact]
    public async Task ADm_WhoNarrowsAnotherMembersEntryToMe_LosesIt()
    {
        var campaign = await TestCampaign.Create(fixture, "Entry narrowed by a DM");
        fixture.LoginAsUser(Users.Player);
        var entry = (await fixture.PostEntry(campaign.Id, "Gundren")).Value;

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryVisibility(campaign.Id, entry.Id, Visibility.Me)).Should().Succeed();
        await fixture.ExpectStatus(HttpMethod.Get, Url(campaign.Id, entry.Id), null, 404);

        fixture.LoginAsUser(Users.Player);
        (await fixture.GetEntry(campaign.Id, entry.Id)).Value.Visibility.Should().Be(Visibility.Me);
    }
}
