using System.Collections.Concurrent;
using System.Net;
using Alba;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TakeInitiative.Api.Features.Users;

namespace TakeInitiative.Api.Tests.Integration.Features.User;

/// <summary>Auth.samstack.org, played by the test: each session cookie answers what the test set.</summary>
public class FakeSamstackSessions : ISamstackSessions
{
    public ConcurrentDictionary<string, SamstackSession> Sessions { get; } = new();

    public Task<SamstackSession> Get(string sessionCookie, CancellationToken ct)
        => Sessions.TryGetValue(sessionCookie, out var session)
            ? Task.FromResult(session)
            : throw new HttpRequestException("auth.samstack.org is down");
}

public class SamstackSessionsFixture : AuthenticatedWebAppWithDatabaseFixture
{
    public FakeSamstackSessions Auth { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
        => services.Replace(ServiceDescriptor.Singleton<ISamstackSessions>(Auth));
}

/// <summary>
/// Signing in through SamStack (SAM-70): existing players are linked by email on their first request
/// and keep their user id; anyone else is a new player.
/// </summary>
public class SamstackSignInTests(SamstackSessionsFixture fixture) : IClassFixture<SamstackSessionsFixture>
{
    private static string CookieFor(string token) => $"__Secure-samstack.session_token={token}";

    private string SignIn(string token, string authUserId, string email, string name = "Someone")
    {
        fixture.Auth.Sessions[CookieFor(token)] = new SamstackSession.SignedIn(new SamstackUser(authUserId, email, name));
        return CookieFor(token);
    }

    private async Task<(HttpStatusCode Status, GetUserResponse? User)> GetUser(string cookie)
    {
        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url("/api/user");
            // Replaces the fixture's password cookie, so only the SamStack session is sent.
            _.ConfigureHttpContext(c => c.Request.Headers.Cookie = $"theme=dark; {cookie}");
            _.IgnoreStatusCode();
        });
        var status = (HttpStatusCode)result.Context.Response.StatusCode;
        return (status, status == HttpStatusCode.OK ? await result.ReadAsJsonAsync<GetUserResponse>() : null);
    }

    private async Task<Guid> PasswordUserId(Users user)
    {
        var response = await fixture.LoginAsUser(user).GetUser();
        return response.Value.UserId;
    }

    private async Task<ApplicationUser> Load(Guid userId)
    {
        await using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return (await session.LoadAsync<ApplicationUser>(userId))!;
    }

    [Fact]
    public async Task AnExistingPlayerIsLinkedByEmailOnTheirFirstSignIn()
    {
        var dmId = await PasswordUserId(Users.DM);

        // Different case from the address the DM signed up with.
        var (status, user) = await GetUser(SignIn("dm", "auth-dm", "TESTING@testingk.com"));

        status.Should().Be(HttpStatusCode.OK);
        user!.UserId.Should().Be(dmId, "the player keeps their user id, and with it their campaigns");
        (await Load(dmId)).AuthUserId.Should().Be("auth-dm");
        fixture.Logs.Should().Contain(l => l.Message.Contains($"Linked user {dmId} to SamStack user auth-dm"));
    }

    [Fact]
    public async Task ALinkedPlayerStaysLinkedWhenTheirEmailChanges()
    {
        var playerId = await PasswordUserId(Users.Player);
        (await GetUser(SignIn("player", "auth-player", "testing2@testingk.com"))).User!.UserId.Should().Be(playerId);

        var (status, user) = await GetUser(SignIn("player-later", "auth-player", "renamed@gmail.com"));

        status.Should().Be(HttpStatusCode.OK);
        user!.UserId.Should().Be(playerId);
    }

    [Fact]
    public async Task SomeoneWithNoAccountIsANewPlayer()
    {
        var (status, user) = await GetUser(SignIn("newcomer", "auth-newcomer", "newcomer@gmail.com", "New Comer"));

        status.Should().Be(HttpStatusCode.OK);
        user!.Username.Should().Be("New Comer");
        var created = await Load(user.UserId);
        created.AuthUserId.Should().Be("auth-newcomer");
        created.Email.Should().Be("newcomer@gmail.com");

        (await GetUser(CookieFor("newcomer"))).User!.UserId.Should().Be(user.UserId, "they are created once");
    }

    [Fact]
    public async Task AnEmailAlreadyLinkedToAnotherSamstackAccountIsRefused()
    {
        var player2Id = await PasswordUserId(Users.Player2);
        (await GetUser(SignIn("player2", "auth-player2", "testing4@testingk.com"))).User!.UserId.Should().Be(player2Id);

        var (status, _) = await GetUser(SignIn("imposter", "auth-imposter", "testing4@testingk.com"));

        status.Should().Be(HttpStatusCode.Unauthorized);
        (await Load(player2Id)).AuthUserId.Should().Be("auth-player2");
    }

    [Fact]
    public async Task ASignedOutSessionIsUnauthorised()
    {
        fixture.Auth.Sessions[CookieFor("signed-out")] = new SamstackSession.SignedOut();
        (await GetUser(CookieFor("signed-out"))).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ASessionWithoutAccessIsForbidden()
    {
        fixture.Auth.Sessions[CookieFor("denied")] = new SamstackSession.Denied();
        (await GetUser(CookieFor("denied"))).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthBeingDownIsUnauthorisedRatherThanAnError()
    {
        (await GetUser(CookieFor("unknown"))).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ThePasswordSignInStillWorks()
    {
        var response = await fixture.LoginAsUser(Users.Stranger).GetUser();
        response.Should().Succeed();
    }
}
