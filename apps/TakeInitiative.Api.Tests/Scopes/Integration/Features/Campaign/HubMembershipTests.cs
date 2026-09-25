using System.Security.Claims;
using FakeItEasy;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;

namespace TakeInitiative.Api.Tests.Integration.Features.Campaign;

/// <summary>
/// The SignalR hub is the only place group membership is enforced, and
/// <see cref="CampaignHub.Join"/> used to get the caller wrong: it compared
/// <c>x.UserId == x.UserId</c> (always true), which let any authenticated user
/// subscribe to a campaign they are not a member of.
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

    [Fact]
    public async Task CampaignHub_RejectsAUserWhoIsNotAMemberOfTheCampaign()
    {
        var nonMemberId = await UserIdOf(Users.Player);
        var groups = A.Fake<IGroupManager>();
        var hub = new CampaignHub { Context = CallerContextFor(nonMemberId), Groups = groups };

        using var session = OpenSession();
        var join = async () => await hub.Join(session, fixture.SeedData!.CampaignId);

        await join.Should().ThrowAsync<OperationCanceledException>()
            .WithMessage("*must be part of the campaign*");

        A.CallTo(() => groups.AddToGroupAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task CampaignHub_AdmitsAMemberOfTheCampaign()
    {
        var campaignId = fixture.SeedData!.CampaignId;
        var memberId = await UserIdOf(Users.DM);
        var groups = A.Fake<IGroupManager>();
        var hub = new CampaignHub { Context = CallerContextFor(memberId), Groups = groups };

        using var session = OpenSession();
        await hub.Join(session, campaignId);

        A.CallTo(() => groups.AddToGroupAsync(ConnectionId, campaignId.ToString(), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CampaignHub_RejectsAnUnauthenticatedConnection()
    {
        var groups = A.Fake<IGroupManager>();
        var context = A.Fake<HubCallerContext>();
        A.CallTo(() => context.ConnectionId).Returns(ConnectionId);
        A.CallTo(() => context.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));
        var hub = new CampaignHub { Context = context, Groups = groups };

        using var session = OpenSession();
        var join = async () => await hub.Join(session, fixture.SeedData!.CampaignId);

        await join.Should().ThrowAsync<OperationCanceledException>();
        A.CallTo(() => groups.AddToGroupAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
