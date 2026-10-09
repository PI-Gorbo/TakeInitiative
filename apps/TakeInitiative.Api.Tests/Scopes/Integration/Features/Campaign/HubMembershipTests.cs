using System.Security.Claims;
using FakeItEasy;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;

namespace TakeInitiative.Api.Tests.Integration.Features.Campaign;

/// <summary>
/// The SignalR hub is the only place group membership is enforced. It checks the
/// caller against the Campaign projection and puts them in <c>campaign:{id}</c>,
/// <c>member:{memberId}</c> and, for DMs, <c>campaign:{id}:dm</c> (design §9).
/// v1 once compared <c>x.UserId == x.UserId</c> (always true), which let any
/// authenticated user subscribe to any campaign.
/// </summary>
public class HubMembershipTests(AuthenticatedWebAppWithDatabaseFixture fixture)
    : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private const string ConnectionId = "test-connection-id";

    private static HubCallerContext CallerContextFor(Guid userId)
    {
        var context = A.Fake<HubCallerContext>();
        A.CallTo(() => context.ConnectionId).Returns(ConnectionId);
        A.CallTo(() => context.User).Returns(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("UserID", userId.ToString())], "TestAuth")));
        return context;
    }

    private IDocumentSession OpenSession()
        => fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().LightweightSession();

    private async Task<Guid> UserIdOf(Users user)
    {
        fixture.LoginAsUser(user);
        var response = await fixture.GetUser();
        response.Should().Succeed();
        return response.Value.UserId;
    }

    private static void ShouldHaveJoined(IGroupManager groups, string group)
        => A.CallTo(() => groups.AddToGroupAsync(ConnectionId, group, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

    private static void ShouldNotHaveJoined(IGroupManager groups, string group)
        => A.CallTo(() => groups.AddToGroupAsync(ConnectionId, group, A<CancellationToken>._))
            .MustNotHaveHappened();

    [Fact]
    public async Task CampaignHub_RejectsAUserWhoIsNotAMemberOfTheCampaign()
    {
        var nonMemberId = await UserIdOf(Users.Outsider);
        var groups = A.Fake<IGroupManager>();
        var hub = new CampaignHub(new CampaignConnections()) { Context = CallerContextFor(nonMemberId), Groups = groups };

        using var session = OpenSession();
        var join = async () => await hub.Join(session, fixture.SeedData!.CampaignId);

        await join.Should().ThrowAsync<OperationCanceledException>()
            .WithMessage("*must be part of the campaign*");

        A.CallTo(() => groups.AddToGroupAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CampaignHub_PutsADmInTheCampaignMemberAndDmGroups()
    {
        var campaignId = fixture.SeedData!.CampaignId;
        var userId = await UserIdOf(Users.DM);
        fixture.LoginAsUser(Users.DM);
        var memberId = (await fixture.GetCampaign(campaignId)).Value.CurrentMemberId;

        var groups = A.Fake<IGroupManager>();
        var connections = new CampaignConnections();
        var hub = new CampaignHub(connections) { Context = CallerContextFor(userId), Groups = groups };

        using var session = OpenSession();
        await hub.Join(session, campaignId);

        ShouldHaveJoined(groups, CampaignGroups.Campaign(campaignId));
        ShouldHaveJoined(groups, CampaignGroups.Member(memberId));
        ShouldHaveJoined(groups, CampaignGroups.Dms(campaignId));
        connections.ConnectionsOf(memberId).Should().Equal(ConnectionId);
    }

    [Fact]
    public async Task CampaignHub_KeepsAPlayerOutOfTheDmGroup()
    {
        // A campaign of its own, so the other tests' view of the seeded campaign is unaffected.
        fixture.LoginAsUser(Users.DM);
        var created = await fixture.PostCreateCampaign(new() { Name = "Hub player campaign" });
        created.Should().Succeed();
        fixture.LoginAsUser(Users.Player);
        var joined = await fixture.PostJoinCampaign(new() { JoinCode = created.Value.JoinCode });
        joined.Should().Succeed();

        var campaignId = created.Value.Id;
        var playerMemberId = joined.Value.CurrentMemberId;
        var userId = await UserIdOf(Users.Player);
        var groups = A.Fake<IGroupManager>();
        var hub = new CampaignHub(new CampaignConnections()) { Context = CallerContextFor(userId), Groups = groups };

        using var session = OpenSession();
        await hub.Join(session, campaignId);

        ShouldHaveJoined(groups, CampaignGroups.Campaign(campaignId));
        ShouldHaveJoined(groups, CampaignGroups.Member(playerMemberId));
        ShouldNotHaveJoined(groups, CampaignGroups.Dms(campaignId));
    }

    [Fact]
    public async Task RoleChange_MovesTheMembersConnectionsInAndOutOfTheDmGroup()
    {
        var campaignId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var connections = new CampaignConnections();
        connections.Track("c1", memberId);
        connections.Track("c2", memberId);

        var hubContext = A.Fake<IHubContext<CampaignHub>>();
        var groups = A.Fake<IGroupManager>();
        var campaignGroup = A.Fake<IClientProxy>();
        A.CallTo(() => hubContext.Groups).Returns(groups);
        A.CallTo(() => hubContext.Clients.Group(CampaignGroups.Campaign(campaignId))).Returns(campaignGroup);

        await hubContext.NotifyMemberRoleChanged(connections, campaignId, memberId, Role.DM);
        A.CallTo(() => groups.AddToGroupAsync("c1", CampaignGroups.Dms(campaignId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => groups.AddToGroupAsync("c2", CampaignGroups.Dms(campaignId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();

        await hubContext.NotifyMemberRoleChanged(connections, campaignId, memberId, Role.Player);
        A.CallTo(() => groups.RemoveFromGroupAsync("c1", CampaignGroups.Dms(campaignId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => groups.RemoveFromGroupAsync("c2", CampaignGroups.Dms(campaignId), A<CancellationToken>._)).MustHaveHappenedOnceExactly();

        A.CallTo(() => campaignGroup.SendCoreAsync(CampaignHubMessages.MemberRoleChanged, A<object?[]>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task CampaignHub_RejectsAnUnauthenticatedConnection()
    {
        var groups = A.Fake<IGroupManager>();
        var context = A.Fake<HubCallerContext>();
        A.CallTo(() => context.ConnectionId).Returns(ConnectionId);
        A.CallTo(() => context.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));
        var hub = new CampaignHub(new CampaignConnections()) { Context = context, Groups = groups };

        using var session = OpenSession();
        var join = async () => await hub.Join(session, fixture.SeedData!.CampaignId);

        await join.Should().ThrowAsync<OperationCanceledException>();
        A.CallTo(() => groups.AddToGroupAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
