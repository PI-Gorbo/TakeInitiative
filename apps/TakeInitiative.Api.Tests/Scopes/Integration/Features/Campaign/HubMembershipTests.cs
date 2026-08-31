using System.Security.Claims;
using FakeItEasy;
using FluentAssertions;
using Marten;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;
using TakeInitiative.Api.Features.Combats;

namespace TakeInitiative.Api.Tests.Integration.Features.Campaign;

/// <summary>
/// The SignalR hubs are the only place group membership is enforced, and both hubs
/// used to get the caller wrong: <see cref="CampaignHub.Join"/> compared
/// <c>x.UserId == x.UserId</c> (always true) and <see cref="CombatHub.JoinCombat"/>
/// took the user id as a client-supplied argument. Either let any authenticated user
/// subscribe to a campaign or combat they are not a member of.
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

    [Fact]
    public async Task CombatHub_RejectsAUserWhoIsNotAMemberOfTheCampaign()
    {
        // The DM opens a combat in the seeded campaign.
        fixture.LoginAsUser(Users.DM);
        var plannedCombat = await fixture.PostPlannedCombat(new()
        {
            CampaignId = fixture.SeedData!.CampaignId,
            CombatName = "Hub membership combat"
        });
        plannedCombat.Should().Succeed();

        var openedCombat = await fixture.PostOpenCombat(new() { PlannedCombatId = plannedCombat.Value.Id });
        openedCombat.Should().Succeed();
        var combatId = openedCombat.Value.Combat.Id;

        // A user who is not in the campaign must not be able to subscribe to it,
        // even though the old signature let the client name whichever user it liked.
        var nonMemberId = await UserIdOf(Users.Player);
        var groups = A.Fake<IGroupManager>();
        var hub = new CombatHub { Context = CallerContextFor(nonMemberId), Groups = groups };

        var store = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();
        var join = async () => await hub.JoinCombat(store, combatId);

        await join.Should().ThrowAsync<OperationCanceledException>()
            .WithMessage("*campaign you are not apart of*");

        A.CallTo(() => groups.AddToGroupAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }
}
