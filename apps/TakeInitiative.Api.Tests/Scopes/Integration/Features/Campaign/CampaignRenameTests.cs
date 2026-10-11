using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using CampaignDoc = TakeInitiative.Api.Features.Campaigns.Campaign;

namespace TakeInitiative.Api.Tests.Integration.Features.Campaign;

/// <summary>
/// Renaming a campaign (SAM-22): <c>PUT /api/campaigns/{id}/name</c> appends
/// <c>CampaignRenamed</c> and pushes <c>campaignRenamed</c> to the whole campaign group.
/// Any DM may rename, which is what separates it from <c>PutMemberRole</c>'s owner-only rule.
/// Each test creates its own campaign so the tests do not depend on each other's order.
/// </summary>
public class CampaignRenameTests(RecordingHubFixture fixture) : IClassFixture<RecordingHubFixture>
{
    private async Task<CampaignResponse> CreateCampaignAsDm(string name)
    {
        fixture.LoginAsUser(Users.DM);
        var created = await fixture.PostCreateCampaign(new() { Name = name });
        created.Should().Succeed();
        return created.Value;
    }

    private async Task<CampaignResponse> JoinAs(Users user, string joinCode)
    {
        fixture.LoginAsUser(user);
        var joined = await fixture.PostJoinCampaign(new() { JoinCode = joinCode });
        joined.Should().Succeed();
        return joined.Value;
    }

    private async Task<IReadOnlyList<JasperFx.Events.IEvent>> EventsOf(Guid campaignId)
    {
        using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return await session.Events.FetchStreamAsync(campaignId);
    }

    private async Task<IReadOnlyList<HubMessage>> Pushed(Func<Task> act)
    {
        var mark = fixture.Hub.Messages.Count;
        await act();
        return fixture.Hub.Messages.Skip(mark).ToList();
    }

    [Fact]
    public async Task ADm_RenamesTheCampaign()
    {
        var campaign = await CreateCampaignAsDm("The Sunless Citadel");

        var renamed = await fixture.PutCampaignName(campaign.Id, "The Forge of Fury");
        renamed.Should().Succeed();
        renamed.Value.Name.Should().Be("The Forge of Fury");

        // The rest of the campaign is untouched.
        renamed.Value.Id.Should().Be(campaign.Id);
        renamed.Value.JoinCode.Should().Be(campaign.JoinCode);
        renamed.Value.OwnerMemberId.Should().Be(campaign.OwnerMemberId);

        // And it is persisted, not just echoed.
        var after = await fixture.GetCampaign(campaign.Id);
        after.Value.Name.Should().Be("The Forge of Fury");

        var events = await EventsOf(campaign.Id);
        events.Should().HaveCount(2);
        events[1].Data.Should().BeOfType<CampaignRenamed>()
            .Which.Actor.MemberId.Should().Be(campaign.OwnerMemberId);
    }

    [Fact]
    public async Task ANonOwnerDm_RenamesTheCampaign()
    {
        var campaign = await CreateCampaignAsDm("Non-owner DM renames");
        var player = await JoinAs(Users.Player, campaign.JoinCode);

        fixture.LoginAsUser(Users.DM);
        (await fixture.PutMemberRole(campaign.Id, player.CurrentMemberId, Role.DM)).Should().Succeed();

        // Settings management is a DM's, not only the owner's (design §1).
        fixture.LoginAsUser(Users.Player);
        var renamed = await fixture.PutCampaignName(campaign.Id, "Renamed by a promoted DM");
        renamed.Should().Succeed();
        renamed.Value.Name.Should().Be("Renamed by a promoted DM");
        ((IActorEvent)(await EventsOf(campaign.Id))[^1].Data).Actor.MemberId.Should().Be(player.CurrentMemberId);
    }

    [Fact]
    public async Task OnlyADm_RenamesTheCampaign()
    {
        var campaign = await CreateCampaignAsDm("Players cannot rename");
        await JoinAs(Users.Player, campaign.JoinCode);
        var url = $"/api/campaigns/{campaign.Id}/name";

        // A player is a member, but not a DM.
        fixture.LoginAsUser(Users.Player);
        await fixture.ExpectStatus(HttpMethod.Put, url, new { name = "Player's rename" }, 403);

        // A non-member gets 403 before anything is appended.
        fixture.LoginAsUser(Users.Outsider);
        await fixture.ExpectStatus(HttpMethod.Put, url, new { name = "Outsider's rename" }, 403);

        // An unknown campaign is a 404.
        fixture.LoginAsUser(Users.DM);
        await fixture.ExpectStatus(
            HttpMethod.Put, $"/api/campaigns/{Guid.NewGuid()}/name", new { name = "No such campaign" }, 404);

        (await EventsOf(campaign.Id)).Should().HaveCount(2);
        var after = await fixture.GetCampaign(campaign.Id);
        after.Value.Name.Should().Be("Players cannot rename");
    }

    [Fact]
    public async Task TheSameName_AppendsNothing()
    {
        var campaign = await CreateCampaignAsDm("Unchanged");

        var unchanged = await fixture.PutCampaignName(campaign.Id, "Unchanged");
        unchanged.Should().Succeed();
        unchanged.Value.Name.Should().Be("Unchanged");
        (await EventsOf(campaign.Id)).Should().HaveCount(1);

        // The name is trimmed first, so surrounding space is not a change either.
        (await fixture.PutCampaignName(campaign.Id, "  Unchanged  ")).Should().Succeed();
        (await EventsOf(campaign.Id)).Should().HaveCount(1);
    }

    [Fact]
    public async Task AName_IsTrimmedAndBounded()
    {
        var campaign = await CreateCampaignAsDm("Bounds");
        var url = $"/api/campaigns/{campaign.Id}/name";

        var trimmed = await fixture.PutCampaignName(campaign.Id, "  The Lost Mine  ");
        trimmed.Should().Succeed();
        trimmed.Value.Name.Should().Be("The Lost Mine");

        // Blank and over-long names are rejected on `name`.
        var blank = await fixture.Send(HttpMethod.Put, url, new { name = "   " });
        blank.Status.Should().Be(400);
        blank.Body.Should().Contain("name");

        await fixture.ExpectStatus(HttpMethod.Put, url, new { name = new string('x', CampaignDoc.NameMaxLength + 1) }, 400);

        // The longest allowed name is accepted, measured after trimming.
        var longest = new string('x', CampaignDoc.NameMaxLength);
        (await fixture.PutCampaignName(campaign.Id, $" {longest} ")).Value.Name.Should().Be(longest);

        var after = await fixture.GetCampaign(campaign.Id);
        after.Value.Name.Should().Be(longest);
    }

    [Fact]
    public async Task ARename_PushesToTheWholeCampaign()
    {
        var campaign = await CreateCampaignAsDm("Push on rename");

        var pushed = await Pushed(async () =>
            (await fixture.PutCampaignName(campaign.Id, "Pushed")).Should().Succeed());

        // The name is not secret: it goes to the campaign group, not the DM group.
        var message = pushed.Should().ContainSingle().Which;
        message.Method.Should().Be(CampaignHubMessages.CampaignRenamed);
        message.Groups.Should().BeEquivalentTo([CampaignGroups.Campaign(campaign.Id)]);

        // An unchanged name pushes nothing.
        var again = await Pushed(async () =>
            (await fixture.PutCampaignName(campaign.Id, "Pushed")).Should().Succeed());
        again.Should().BeEmpty();
    }
}
