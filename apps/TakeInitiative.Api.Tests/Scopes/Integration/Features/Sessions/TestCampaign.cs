using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Campaigns;

namespace TakeInitiative.Api.Tests.Integration.Features.Sessions;

/// <summary>
/// A campaign created inside a test: <see cref="Users.DM"/> owns it, <see cref="Users.Player"/>
/// joins it, and <see cref="Users.Outsider"/> joins it as a second player when asked to.
/// </summary>
public record TestCampaign(Guid Id, Guid DmMemberId, Guid PlayerMemberId, Guid? SecondPlayerMemberId)
{
    public static async Task<TestCampaign> Create(AuthenticatedWebAppWithDatabaseFixture fixture, string name, bool withSecondPlayer = true)
    {
        fixture.LoginAsUser(Users.DM);
        var created = await fixture.PostCreateCampaign(new() { Name = name });
        created.Should().Succeed();

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

        return new TestCampaign(created.Value.Id, created.Value.OwnerMemberId, player.Value.CurrentMemberId, second);
    }

    public static async Task<IReadOnlyList<Marten.Events.IEvent>> EventsOf(AuthenticatedWebAppWithDatabaseFixture fixture, Guid streamId)
    {
        using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return await session.Events.FetchStreamAsync(streamId);
    }
}
