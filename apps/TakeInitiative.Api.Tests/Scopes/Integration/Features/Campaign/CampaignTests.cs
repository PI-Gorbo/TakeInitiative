using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using CampaignDoc = TakeInitiative.Api.Features.Campaigns.Campaign;

namespace TakeInitiative.Api.Tests.Integration.Features.Campaign;

/// <summary>
/// The v2 Campaign stream: CampaignCreated, MemberJoined and MemberRoleChanged,
/// projected inline into the Campaign document that holds all membership.
/// Each test creates its own campaign so the tests do not depend on each other's order.
/// </summary>
public class CampaignTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
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

    private async Task<IReadOnlyList<Marten.Events.IEvent>> EventsOf(Guid campaignId)
    {
        using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return await session.Events.FetchStreamAsync(campaignId);
    }

    [Fact]
    public async Task Create_MakesTheOwnerTheOnlyMemberAsADm()
    {
        var campaign = await CreateCampaignAsDm("Owner is DM");

        campaign.Name.Should().Be("Owner is DM");
        campaign.JoinCode.Should().HaveLength(CampaignDoc.JoinCodeLength);
        campaign.Members.Should().ContainSingle();
        var owner = campaign.Members.Single();
        owner.MemberId.Should().Be(campaign.OwnerMemberId).And.Be(campaign.CurrentMemberId);
        owner.Role.Should().Be(Role.DM);
        owner.IsOwner.Should().BeTrue();
        owner.Username.Should().Be("TESTING");
        campaign.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        owner.JoinedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        var mine = await fixture.GetCampaigns();
        mine.Should().Succeed();
        mine.Value.Campaigns.Should().ContainSingle(c => c.Id == campaign.Id)
            .Which.Should().BeEquivalentTo(new { Role = Role.DM, IsOwner = true, MemberCount = 1 });
    }

    [Fact]
    public async Task JoinByCode_AddsThePlayer()
    {
        var campaign = await CreateCampaignAsDm("Join by code");

        // Codes are case-insensitive and trimmed.
        var joined = await JoinAs(Users.Player, $"  {campaign.JoinCode.ToLowerInvariant()} ");

        joined.Id.Should().Be(campaign.Id);
        joined.Members.Should().HaveCount(2);
        var player = joined.Members.Single(m => m.MemberId == joined.CurrentMemberId);
        player.Role.Should().Be(Role.Player);
        player.IsOwner.Should().BeFalse();
        player.Username.Should().Be("TESTING2");

        var playersCampaigns = await fixture.GetCampaigns();
        playersCampaigns.Value.Campaigns.Should().ContainSingle(c => c.Id == campaign.Id)
            .Which.Should().BeEquivalentTo(new { Role = Role.Player, IsOwner = false, MemberCount = 2 });

        // The owner sees the new member too.
        fixture.LoginAsUser(Users.DM);
        (await fixture.GetCampaign(campaign.Id)).Value.Members.Should().HaveCount(2);
    }

    [Fact]
    public async Task JoiningTwice_IsANoOp()
    {
        var campaign = await CreateCampaignAsDm("Join twice");

        var first = await JoinAs(Users.Player, campaign.JoinCode);
        var second = await JoinAs(Users.Player, campaign.JoinCode);

        second.Members.Should().BeEquivalentTo(first.Members);
        (await EventsOf(campaign.Id)).Select(e => e.Data.GetType()).Should()
            .Equal(typeof(CampaignCreated), typeof(MemberJoined));

        // The owner joining their own campaign is a no-op as well.
        await JoinAs(Users.DM, campaign.JoinCode);
        (await EventsOf(campaign.Id)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Join_WithAnUnknownCode_IsRejected()
    {
        fixture.LoginAsUser(Users.Player);
        await fixture.ExpectStatus(HttpMethod.Post, "/api/campaigns/join", new { joinCode = "NOPE0000" }, 400);
    }

    [Fact]
    public async Task OnlyTheOwner_ChangesRoles()
    {
        var campaign = await CreateCampaignAsDm("Only the owner");
        var player = await JoinAs(Users.Player, campaign.JoinCode);
        var playerMemberId = player.CurrentMemberId;
        var url = $"/api/campaigns/{campaign.Id}/members/{playerMemberId}/role";

        // A player cannot promote themselves.
        fixture.LoginAsUser(Users.Player);
        await fixture.ExpectStatus(HttpMethod.Put, url, new { role = "DM" }, 403);

        // A non-member gets 403 before anything is appended.
        fixture.LoginAsUser(Users.Outsider);
        await fixture.ExpectStatus(HttpMethod.Put, url, new { role = "DM" }, 403);
        await fixture.ExpectStatus(HttpMethod.Get, $"/api/campaigns/{campaign.Id}", null, 403);
        (await EventsOf(campaign.Id)).Should().HaveCount(2);

        // The owner promotes the player.
        fixture.LoginAsUser(Users.DM);
        var promoted = await fixture.PutMemberRole(campaign.Id, playerMemberId, Role.DM);
        promoted.Should().Succeed();
        promoted.Value.Members.Single(m => m.MemberId == playerMemberId).Role.Should().Be(Role.DM);

        // Being a DM is not enough: only the owner changes roles.
        fixture.LoginAsUser(Users.Player);
        await fixture.ExpectStatus(HttpMethod.Put, url, new { role = "Player" }, 403);

        // Setting the role a member already has appends nothing.
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutMemberRole(campaign.Id, playerMemberId, Role.DM)).Should().Succeed();
        (await EventsOf(campaign.Id)).Should().HaveCount(3);

        // And the owner can demote them again.
        var demoted = await fixture.PutMemberRole(campaign.Id, playerMemberId, Role.Player);
        demoted.Value.Members.Single(m => m.MemberId == playerMemberId).Role.Should().Be(Role.Player);
    }

    [Fact]
    public async Task TheOwner_CannotBeDemoted()
    {
        var campaign = await CreateCampaignAsDm("Owner stays DM");
        var player = await JoinAs(Users.Player, campaign.JoinCode);
        var ownerUrl = $"/api/campaigns/{campaign.Id}/members/{campaign.OwnerMemberId}/role";

        fixture.LoginAsUser(Users.DM);
        await fixture.ExpectStatus(HttpMethod.Put, ownerUrl, new { role = "Player" }, 400);

        // A promoted DM cannot demote the owner either.
        (await fixture.PutMemberRole(campaign.Id, player.CurrentMemberId, Role.DM)).Should().Succeed();
        fixture.LoginAsUser(Users.Player);
        await fixture.ExpectStatus(HttpMethod.Put, ownerUrl, new { role = "Player" }, 403);

        fixture.LoginAsUser(Users.DM);
        var after = await fixture.GetCampaign(campaign.Id);
        after.Value.Members.Single(m => m.IsOwner).Role.Should().Be(Role.DM);
    }

    [Fact]
    public async Task EveryEvent_CarriesAnActorAndACorrelationId()
    {
        var campaign = await CreateCampaignAsDm("Provenance");
        var player = await JoinAs(Users.Player, campaign.JoinCode);
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutMemberRole(campaign.Id, player.CurrentMemberId, Role.DM)).Should().Succeed();

        var events = await EventsOf(campaign.Id);
        events.Should().HaveCount(3);
        events.Should().AllSatisfy(e =>
        {
            e.Data.Should().BeAssignableTo<IActorEvent>()
                .Which.Actor.MemberId.Should().NotBeEmpty();
            e.CorrelationId.Should().NotBeNullOrWhiteSpace();
            e.Headers.Should().ContainKey("request");
        });

        // The actor is the member who caused the event.
        ((IActorEvent)events[0].Data).Actor.MemberId.Should().Be(campaign.OwnerMemberId);
        ((IActorEvent)events[1].Data).Actor.MemberId.Should().Be(player.CurrentMemberId);
        ((IActorEvent)events[2].Data).Actor.MemberId.Should().Be(campaign.OwnerMemberId);

        // Each request has its own correlation id.
        events.Select(e => e.CorrelationId).Should().OnlyHaveUniqueItems();
    }
}
