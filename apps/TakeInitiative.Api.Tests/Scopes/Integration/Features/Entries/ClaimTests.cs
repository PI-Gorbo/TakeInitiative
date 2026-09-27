using FluentAssertions;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Tests.Integration.Features.Sessions;
using static TakeInitiative.Api.Tests.Integration.WebAppClientExtensions;

namespace TakeInitiative.Api.Tests.Integration.Features.Entries;

/// <summary>
/// Claims (15g.2): who may claim, assign and unclaim, several claims per member, Character
/// only, and the kind lock. <see cref="Users.DM"/> is the DM, <see cref="Users.Player"/> and
/// <see cref="Users.Outsider"/> the players.
/// </summary>
public class ClaimTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private async Task<EntryResponse> Entry(Users user, Guid campaignId, string name, EntryKind kind = EntryKind.Character, Visibility visibility = Visibility.Everyone)
    {
        fixture.LoginAsUser(user);
        var entry = await fixture.PostEntry(campaignId, name, kind, visibility);
        entry.Should().Succeed();
        return entry.Value;
    }

    private async Task<EntryResponse> Claim(Users user, Guid campaignId, Guid entryId, Guid? memberId)
    {
        fixture.LoginAsUser(user);
        var claimed = await fixture.PutEntryClaim(campaignId, entryId, memberId);
        claimed.Should().Succeed();
        return claimed.Value;
    }

    private async Task<int> Status(Users user, Guid campaignId, Guid entryId, Guid? memberId)
    {
        fixture.LoginAsUser(user);
        return (await fixture.Send(HttpMethod.Put, EntryUrl(campaignId, entryId, "claim"), new { memberId })).Status;
    }

    [Fact]
    public async Task AMember_ClaimsForThemselves_SeveralCharacters_AndTheListShowsTheClaimer()
    {
        var campaign = await TestCampaign.Create(fixture, "Claim own");
        var tordek = await Entry(Users.Player, campaign.Id, "Tordek");
        var dmMade = await Entry(Users.DM, campaign.Id, "Mialee");

        var mark = fixture.Hub.Messages.Count;
        (await Claim(Users.Player, campaign.Id, tordek.Id, campaign.PlayerMemberId)).ClaimedByMemberId.Should().Be(campaign.PlayerMemberId);
        var pushed = fixture.Hub.Messages.Skip(mark).Where(m => m.Method == CampaignHubMessages.EntryUpserted).ToList();
        ((EntrySummaryResponse)pushed.Should().ContainSingle().Subject.Payload!).ClaimedByMemberId.Should().Be(campaign.PlayerMemberId);

        (await Claim(Users.Player, campaign.Id, dmMade.Id, campaign.PlayerMemberId)).ClaimedByMemberId.Should().Be(campaign.PlayerMemberId);

        fixture.LoginAsUser(Users.Outsider);
        var listed = (await fixture.GetEntries(campaign.Id)).Value.Entries;
        listed.Select(e => e.Entry.ClaimedByMemberId).Should().AllBeEquivalentTo(campaign.PlayerMemberId);

        // Claiming again appends nothing.
        var events = (await TestCampaign.EventsOf(fixture, tordek.Id)).Count;
        await Claim(Users.Player, campaign.Id, tordek.Id, campaign.PlayerMemberId);
        (await TestCampaign.EventsOf(fixture, tordek.Id)).Should().HaveCount(events);
    }

    [Fact]
    public async Task APlayer_CannotClaimForOthers_OrTakeAClaimedCharacter_OrUnclaimSomeoneElses()
    {
        var campaign = await TestCampaign.Create(fixture, "Claim limits");
        var tordek = await Entry(Users.Player, campaign.Id, "Tordek");

        (await Status(Users.Player, campaign.Id, tordek.Id, campaign.SecondPlayerMemberId)).Should().Be(403);
        await Claim(Users.Player, campaign.Id, tordek.Id, campaign.PlayerMemberId);
        (await Status(Users.Outsider, campaign.Id, tordek.Id, campaign.SecondPlayerMemberId)).Should().Be(409);
        (await Status(Users.Outsider, campaign.Id, tordek.Id, null)).Should().Be(403);

        // The claimer unclaims their own.
        (await Claim(Users.Player, campaign.Id, tordek.Id, null)).ClaimedByMemberId.Should().BeNull();
        (await Claim(Users.Outsider, campaign.Id, tordek.Id, campaign.SecondPlayerMemberId)).ClaimedByMemberId.Should().Be(campaign.SecondPlayerMemberId);
    }

    [Fact]
    public async Task ADm_AssignsAnyVisibleCharacter_ToAMemberWhoCanSeeIt_AndUnclaims()
    {
        var campaign = await TestCampaign.Create(fixture, "Claim DM");
        var tordek = await Entry(Users.Player, campaign.Id, "Tordek");
        var secret = await Entry(Users.DM, campaign.Id, "Glasstaff", visibility: Visibility.DM);

        (await Claim(Users.DM, campaign.Id, tordek.Id, campaign.SecondPlayerMemberId)).ClaimedByMemberId.Should().Be(campaign.SecondPlayerMemberId);
        (await Claim(Users.DM, campaign.Id, tordek.Id, campaign.PlayerMemberId)).ClaimedByMemberId.Should().Be(campaign.PlayerMemberId);
        (await Claim(Users.DM, campaign.Id, tordek.Id, null)).ClaimedByMemberId.Should().BeNull();

        (await Status(Users.DM, campaign.Id, secret.Id, campaign.PlayerMemberId)).Should().Be(400, "the player cannot see it");
        (await Status(Users.DM, campaign.Id, tordek.Id, Guid.NewGuid())).Should().Be(400, "not a member");
        (await Claim(Users.DM, campaign.Id, secret.Id, campaign.DmMemberId)).ClaimedByMemberId.Should().Be(campaign.DmMemberId);
        (await Status(Users.Player, campaign.Id, secret.Id, campaign.PlayerMemberId)).Should().Be(404);
    }

    [Fact]
    public async Task OnlyACharacter_IsClaimed_AndAClaimedOne_KeepsItsKind()
    {
        var campaign = await TestCampaign.Create(fixture, "Claim kind");
        var place = await Entry(Users.Player, campaign.Id, "Phandalin", EntryKind.Place);
        (await Status(Users.Player, campaign.Id, place.Id, campaign.PlayerMemberId)).Should().Be(409);

        var tordek = await Entry(Users.Player, campaign.Id, "Tordek");
        await Claim(Users.Player, campaign.Id, tordek.Id, campaign.PlayerMemberId);
        fixture.LoginAsUser(Users.DM);
        await fixture.ExpectStatus(HttpMethod.Put, EntryUrl(campaign.Id, tordek.Id, "kind"), new { kind = "Place" }, 409);
        await Claim(Users.Player, campaign.Id, tordek.Id, null);
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutEntryKind(campaign.Id, tordek.Id, EntryKind.Place)).Value.Kind.Should().Be(EntryKind.Place);

        var events = await TestCampaign.EventsOf(fixture, tordek.Id);
        events.Select(e => e.Data).OfType<EntryClaimed>().Single().Actor.MemberId.Should().Be(campaign.PlayerMemberId);
        events.Select(e => e.Data).OfType<EntryUnclaimed>().Should().ContainSingle();
    }
}
