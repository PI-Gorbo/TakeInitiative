
using System.Collections.Concurrent;
using System.Net;
using Alba;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Api.Features.Images;
using TakeInitiative.Api.Features.Sessions;
using TakeInitiative.Api.Features.Users;
using TakeInitiative.Utilities;
using Testcontainers.PostgreSql;

namespace TakeInitiative.Api.Tests.Integration;

public record UserSeedData
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Email { get; set; }
    public required StringValues Cookie { get; set; }
}

public enum Users
{
    DM,
    Player,
    /// <summary>A signed-up user who is not a member of the seeded campaign.</summary>
    Outsider,
    /// <summary>
    /// A second player, for the cases that need two of them: a hidden note's author and
    /// "another player" (17a's leak matrix) cannot be the same person.
    /// </summary>
    Player2,
    /// <summary>
    /// A signed-up user who joins nothing a test creates. <see cref="Outsider"/> joins a
    /// <c>TestCampaign</c> by default, so the cases about a non-member (a 403) and about a
    /// member of another campaign need someone who is never in this one.
    /// </summary>
    Stranger,
}

public record AuthenticatedWebAppWithDatabaseFixtureSeededData
{
    public required UserSeedData DMUserData { get; set; }
    public required UserSeedData PlayerUserData { get; set; }
    public required UserSeedData OutsiderUserData { get; set; }
    public required UserSeedData Player2UserData { get; set; }
    public required UserSeedData StrangerUserData { get; set; }
    public required string CampaignName { get; set; }
    public required Guid CampaignId { get; set; }
}

public class AuthenticatedWebAppWithDatabaseFixture : IAsyncLifetime, IWebAppClient
{
    public IAlbaHost AlbaHost { get; private set; } = null!;
    public PostgreSqlContainer PostgreSqlContainer { get; private set; } = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();
    private Users CurrentUser;
    public AuthenticatedWebAppWithDatabaseFixtureSeededData? SeedData { get; set; }
    public IDiceRoller DiceRoller { get; } = A.Fake<IDiceRoller>();
    /// <summary>The gap prompt's clock. Tests move it forward to exercise the gap prompt, and must reset it.</summary>
    public ShiftableTimeProvider Clock { get; } = new();
    /// <summary>The blob store (step 16a). S3BlobStoreTests covers the real one against MinIO.</summary>
    public InMemoryBlobStore Blobs { get; } = new();
    /// <summary>
    /// Every log record the API wrote while this fixture lived (<see cref="CapturingLoggerProvider"/>),
    /// so a test can assert that nothing logged what should never be logged.
    /// </summary>
    public ConcurrentQueue<LoggedRecord> Logs { get; } = new();

    /// <summary>Lets a derived fixture swap services in the host (for example the hub context).</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services) { }

    /// <summary>
    /// Runs once the host is up and its schema applied, for a derived fixture that needs rows in a
    /// table no endpoint writes. The knowledge base (26d₂) is the only one: it is written by the
    /// ingest CLI, so a test host's table is empty unless a fixture seeds it — which is also the
    /// state a deployment with nothing ingested is in, and what most of the Reference tests want.
    /// </summary>
    /// <param name="connectionString">The container's database, the same one the host is bound to.</param>
    protected virtual Task SeedDatabaseAsync(string connectionString) => Task.CompletedTask;

    public async Task InitializeAsync()
    {
        await PostgreSqlContainer.StartAsync();
        AlbaHost = await HostStartup.Start(() => Alba.AlbaHost.For<Api.Program>(x =>
            x.UseEnvironment(Environments.Development)
            .ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:TakeDB"] = PostgreSqlContainer.GetConnectionString(),
                            ["Blobs:CreateBucket"] = "false",
                            // Tests sweep by hand (ImageSweeper.SweepOnce), never in the background.
                            ["Images:SweepStartDelay"] = "1.00:00:00",
                        });
                })
           .ConfigureServices((context, services) =>
                {
                    services.Replace(
                        new ServiceDescriptor(typeof(IDiceRoller), DiceRoller)
                    );
                    services.AddKeyedSingleton<TimeProvider>(SessionGap.ClockKey, Clock);
                    services.Replace(ServiceDescriptor.Singleton<IBlobStore>(Blobs));
                    services.AddSingleton<ILoggerProvider>(new CapturingLoggerProvider(Logs));
                    services.AddMartenDB(context.Configuration);
                    ConfigureTestServices(services);
                })
        ));

        // After the host, because the host is what creates the schema
        // (ApplyAllDatabaseChangesOnStartup), and before the users, because a seeded corpus should
        // be there for the first request a test makes.
        await SeedDatabaseAsync(PostgreSqlContainer.GetConnectionString());

        // Seed database with tiny seed.
        // Sign Up With User1 Credentials.
        var DmCookie = await CreateUserWithData(new PostSignUpRequest()
        {
            Email = "testing@testingk.com",
            Password = "Besbing!99",
            Username = "TESTING"
        });

        var PlayerCookie = await CreateUserWithData(new PostSignUpRequest()
        {
            Email = "testing2@testingk.com",
            Password = "Besbing!100",
            Username = "TESTING2"
        });

        var OutsiderCookie = await CreateUserWithData(new PostSignUpRequest()
        {
            Email = "testing3@testingk.com",
            Password = "Besbing!101",
            Username = "TESTING3"
        });

        var Player2Cookie = await CreateUserWithData(new PostSignUpRequest()
        {
            Email = "testing4@testingk.com",
            Password = "Besbing!102",
            Username = "TESTING4"
        });

        var StrangerCookie = await CreateUserWithData(new PostSignUpRequest()
        {
            Email = "testing5@testingk.com",
            Password = "Besbing!103",
            Username = "TESTING5"
        });

        // Temporary authentication fixed to dm to create the campaign.
        AlbaHost.BeforeEach((c) =>
        {
            c.Request.Headers.Cookie = DmCookie;
        });

        // Create a campaign called 'Super Testing Campaign'
        var createResponse = await this.PostCreateCampaign(new() { Name = "Super Testing Campaign" });
        createResponse.Should().Succeed();

        SeedData = new()
        {
            CampaignName = createResponse.Value.Name,
            CampaignId = createResponse.Value.Id,
            DMUserData = new UserSeedData()
            {
                Email = "testing@testingk.com",
                Password = "Besbing!99",
                Username = "TESTING",
                Cookie = DmCookie
            },
            PlayerUserData = new UserSeedData()
            {
                Email = "testing2@testingk.com",
                Password = "Besbing!100",
                Username = "TESTING2",
                Cookie = PlayerCookie
            },
            OutsiderUserData = new UserSeedData()
            {
                Email = "testing3@testingk.com",
                Password = "Besbing!101",
                Username = "TESTING3",
                Cookie = OutsiderCookie
            },
            Player2UserData = new UserSeedData()
            {
                Email = "testing4@testingk.com",
                Password = "Besbing!102",
                Username = "TESTING4",
                Cookie = Player2Cookie
            },
            StrangerUserData = new UserSeedData()
            {
                Email = "testing5@testingk.com",
                Password = "Besbing!103",
                Username = "TESTING5",
                Cookie = StrangerCookie
            }
        };

        // Overwrite the Before each hook, to implement authentication which is controlled 
        // by the 'CurrentUser' property.
        CurrentUser = Users.DM;
        AlbaHost.BeforeEach((c) =>
        {
            c.Request.Headers.Cookie = CurrentUser switch
            {
                Users.DM => SeedData.DMUserData.Cookie,
                Users.Player => SeedData.PlayerUserData.Cookie,
                Users.Outsider => SeedData.OutsiderUserData.Cookie,
                Users.Player2 => SeedData.Player2UserData.Cookie,
                Users.Stranger => SeedData.StrangerUserData.Cookie,
                _ => throw new NotImplementedException(),
            };
        });
    }

    private async Task<StringValues> CreateUserWithData(PostSignUpRequest seedData)
    {
        // Seed database with tiny seed.
        // Sign Up With User1 Credentials.
        var cookieResult = await this.SignUp(seedData);
        cookieResult.Should().Succeed();
        return cookieResult.Value;
    }

    public AuthenticatedWebAppWithDatabaseFixture LoginAsUser(Users user)
    {
        CurrentUser = user;
        return this;
    }

    public Cookie ParseCookieString(string cookieString)
    {
        var parts = cookieString.Split(';');
        var cookieValue = parts[0].Trim();
        var cookie = new Cookie(".AspNetCore.Cookies", cookieValue);

        foreach (var part in parts[1..])
        {
            var keyValue = part.Split('=');

            if (keyValue.Length == 2)
            {
                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                switch (key.ToLower())
                {
                    case "expires":
                        if (DateTime.TryParse(value, out var expires))
                        {
                            cookie.Expires = expires;
                        }
                        break;
                    case "max-age":
                        // Max-Age is generally not needed if Expires is set
                        break;
                    case "domain":
                        cookie.Domain = value;
                        break;
                    case "path":
                        cookie.Path = value;
                        break;
                    case "samesite":
                        // SameSite is not directly supported in the Cookie class; it might require custom handling
                        break;
                    case "httponly":
                        cookie.HttpOnly = true;
                        break;
                }
            }
        }

        return cookie;
    }

    public async Task DisposeAsync()
    {
        if (AlbaHost != null)
        {
            await AlbaHost.StopAsync();
        }

        if (PostgreSqlContainer != null)
        {
            await PostgreSqlContainer.StopAsync();
        }
    }

}