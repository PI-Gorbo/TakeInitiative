using System.Net;
using Alba;
using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using TakeInitiative.Api.Features.Users;

namespace TakeInitiative.Api.Tests.Integration.Features.User;

/// <summary>
/// Signing in through SamStack (SAM-70): existing players are linked by email on their first request
/// and keep their user id; anyone else is a new player. The fixture's seeded users are SamStack users
/// already, so an unlinked player from the password days is written straight to the database.
/// </summary>
public class SamstackSignInTests(AuthenticatedWebAppWithDatabaseFixture fixture) : IClassFixture<AuthenticatedWebAppWithDatabaseFixture>
{
    private static string CookieFor(string token) => $"__Secure-samstack.session_token={token}";

    private string SignIn(string token, string authUserId, string email, string name = "Someone")
        => fixture.Auth.SignIn(token, new SamstackUser(authUserId, email, name));

    private async Task<(HttpStatusCode Status, GetUserResponse? User)> GetUser(string cookie)
    {
        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url("/api/user");
            // Replaces the fixture's seeded user's cookie, so only this session is sent.
            _.ConfigureHttpContext(c => c.Request.Headers.Cookie = $"theme=dark; {cookie}");
            _.IgnoreStatusCode();
        });
        var status = (HttpStatusCode)result.Context.Response.StatusCode;
        return (status, status == HttpStatusCode.OK ? await result.ReadAsJsonAsync<GetUserResponse>() : null);
    }

    private async Task<Guid> SeededUserId(Users user)
    {
        var response = await fixture.LoginAsUser(user).GetUser();
        return response.Value.UserId;
    }

    /// <summary>A player who signed up with a password and has never signed in through SamStack.</summary>
    private async Task<Guid> UnlinkedPlayer(string email)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            // Identity's upper-invariant normalisation, which is what the password sign-up stored.
            NormalizedEmail = email.ToUpperInvariant(),
            UserName = "Old Timer",
            NormalizedUserName = "OLD TIMER",
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        await using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        session.Store(user);
        await session.SaveChangesAsync();
        return user.Id;
    }

    private async Task<ApplicationUser> Load(Guid userId)
    {
        await using var session = fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return (await session.LoadAsync<ApplicationUser>(userId))!;
    }

    [Fact]
    public async Task AnExistingPlayerIsLinkedByEmailOnTheirFirstSignIn()
    {
        var oldTimerId = await UnlinkedPlayer("old.timer@testingk.com");

        // Different case from the address they signed up with.
        var (status, user) = await GetUser(SignIn("old-timer", "auth-old-timer", "Old.Timer@TestingK.com"));

        status.Should().Be(HttpStatusCode.OK);
        user!.UserId.Should().Be(oldTimerId, "the player keeps their user id, and with it their campaigns");
        (await Load(oldTimerId)).AuthUserId.Should().Be("auth-old-timer");
        fixture.Logs.Should().Contain(l => l.Message.Contains($"Linked user {oldTimerId} to SamStack user auth-old-timer"));
    }

    [Fact]
    public async Task ALinkedPlayerStaysLinkedWhenTheirEmailChanges()
    {
        var playerId = await SeededUserId(Users.Player);

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
        var player2Id = await SeededUserId(Users.Player2);

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
    public async Task TheOldPasswordCookieNoLongerSignsAnyoneIn()
        => (await GetUser(".AspNetCore.Cookies=a-ticket-from-before-SAM-73")).Status.Should().Be(HttpStatusCode.Unauthorized);
}

public class SamstackDevUserFixture : AuthenticatedWebAppWithDatabaseFixture
{
    protected override void ConfigureTestServices(IServiceCollection services)
        => services.Configure<SamstackAuthOptions>(opts =>
            opts.DevUser = new SamstackDevUser { Id = "dev-user", Email = "dev@takeinitiative.local", Name = "Dev" });
}

/// <summary>Local development: the .samstack.org cookie never reaches localhost, so the API signs in a fixed user.</summary>
public class SamstackDevUserTests(SamstackDevUserFixture fixture) : IClassFixture<SamstackDevUserFixture>
{
    [Fact]
    public async Task ARequestWithoutASessionIsTheDevUser()
    {
        var result = await fixture.AlbaHost.Scenario(_ =>
        {
            _.Get.Url("/api/user");
            _.ConfigureHttpContext(c => c.Request.Headers.Cookie = Microsoft.Extensions.Primitives.StringValues.Empty);
            _.StatusCodeShouldBe(200);
        });

        (await result.ReadAsJsonAsync<GetUserResponse>())!.Username.Should().Be("Dev");
    }
}
