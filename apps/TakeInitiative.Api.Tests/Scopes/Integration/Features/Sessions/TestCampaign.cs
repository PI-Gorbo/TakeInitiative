using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;

namespace TakeInitiative.Api.Tests.Integration.Features.Sessions;

/// <summary>
/// A campaign created inside a test: <see cref="Users.DM"/> owns it, <see cref="Users.Player"/>
/// joins it, and <see cref="Users.Outsider"/> joins it as a second player when asked to.
/// Creating a campaign starts no session, so the DM starts Session 1 for the tests that
/// post notes. A test about the sessionless state creates its campaign itself.
/// </summary>
public record TestCampaign(Guid Id, Guid DmMemberId, Guid PlayerMemberId, Guid? SecondPlayerMemberId, string JoinCode = "")
{
    /// <summary>Another user joins this campaign, as a Player. Their member id comes back.</summary>
    public async Task<Guid> Join(AuthenticatedWebAppWithDatabaseFixture fixture, Users who)
    {
        fixture.LoginAsUser(who);
        var joined = await fixture.PostJoinCampaign(new() { JoinCode = JoinCode });
        joined.Should().Succeed();
        return joined.Value.CurrentMemberId;
    }

    /// <summary>Promotes a member to DM, as the owner. The tests that need two DMs use it.</summary>
    public async Task PromoteToDm(AuthenticatedWebAppWithDatabaseFixture fixture, Guid memberId)
    {
        fixture.LoginAsUser(Users.DM);
        (await fixture.PutMemberRole(Id, memberId, Role.DM)).Should().Succeed();
    }


    public static async Task<TestCampaign> Create(AuthenticatedWebAppWithDatabaseFixture fixture, string name, bool withSecondPlayer = true)
    {
        fixture.LoginAsUser(Users.DM);
        var created = await fixture.PostCreateCampaign(new() { Name = name });
        created.Should().Succeed();
        (await fixture.PostStartSession(created.Value.Id, 1)).Should().Succeed();

        fixture.LoginAsUser(Users.Player);
        var player = await fixture.PostJoinCampaign(new() { JoinCode = created.Value.JoinCode });
        player.Should().Succeed();

        Guid? second = null;
        if (withSecondPlayer)
        {
            fixture.LoginAsUser(Users.Outsider);
            var joined = await fixture.PostJoinCampaign(new() { JoinCode = created.Value.JoinCode });
            joined.Should().Succeed();
            second = joined.Value.CurrentMemberId;
        }

        return new TestCampaign(
            created.Value.Id, created.Value.OwnerMemberId, player.Value.CurrentMemberId, second, created.Value.JoinCode);
    }

    public static async Task<IReadOnlyList<Marten.Events.IEvent>> EventsOf(AuthenticatedWebAppWithDatabaseFixture fixture, Guid streamId)
    {
        using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return await session.Events.FetchStreamAsync(streamId);
    }
}
