
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Alba;
using CSharpFunctionalExtensions;
using FluentAssertions;
using JasperFx.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Api.Client;
using TakeInitiative.Utilities;
using Testcontainers.PostgreSql;

namespace TakeInitiative.Api.Tests.Integration;

public record UserSeedData
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Email { get; set; }
    public required string Cookie { get; set; }
}

public enum Users
{
    DM,
    Player
}

public record AuthenticatedWebAppWithDatabaseFixtureSeededData
{
    public required UserSeedData DMUserData { get; set; }
    public required UserSeedData PlayerUserData { get; set; }
    public required string CampaignName { get; set; }
    public required Guid CampaignId { get; set; }
}

public class AuthenticatedWebAppWithDatabaseFixture : IAsyncLifetime, IWebAppClient
{
    public IAlbaHost AlbaHost { get; private set; } = null!;
    public PostgreSqlContainer PostgreSqlContainer { get; private set; } = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();
    private HttpClient albaHttpClient;
    private Users CurrentUser;
    public TakeApiClient Client { get; private set; }
    public AuthenticatedWebAppWithDatabaseFixtureSeededData SeedData { get; set; }
    public IDiceRoller DiceRollerSubstitute => Substitute.For<IDiceRoller>();


    public async Task InitializeAsync()
    {
        await this.PostgreSqlContainer.StartAsync();
        this.AlbaHost = await Alba.AlbaHost.For<Api.Program>(x =>
            x.UseEnvironment(Environments.Development)
            .ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:TakeDB"] = this.PostgreSqlContainer.GetConnectionString()
                        });
                })
           .ConfigureServices((context, services) =>
                {
                    services.Replace(
                        new ServiceDescriptor(typeof(IDiceRoller), DiceRollerSubstitute)
                    );
                    services.AddMartenDB(context.Configuration, IsDevelopment: true);
                })
        );

        // Create a user in the database, with a campaign.
        this.albaHttpClient = AlbaHost.GetTestClient();
        this.Client = new TakeApiClient(AlbaHost.Server.BaseAddress.ToString(), this.albaHttpClient);

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

        this.albaHttpClient.DefaultRequestHeaders.Add("Cookie", DmCookie);
        this.CurrentUser = Users.DM;

        // Create a campaign called 'Super Testing Campaign'
        var createResponse = await Result.Try(() => Client.TakeInitiativeApiFeaturesCampaignsPostCreateCampaignAsync(new() { CampaignName = "Super Testing Campaign" }));
        createResponse.Should().Succeed();

        this.SeedData = new()
        {
            CampaignName = createResponse.Value.CampaignName,
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
                Email = "testing@testingk.com",
                Password = "Besbing!99",
                Username = "TESTING",
                Cookie = PlayerCookie
            }
        };
    }

    private async Task<string> CreateUserWithData(PostSignUpRequest seedData)
    {
        // Seed database with tiny seed.
        // Sign Up With User1 Credentials.
        var signup = await Result.Try(() => Client.TakeInitiativeApiFeaturesUsersPostSignUpAsync(seedData));
        signup.Should().Succeed();

        // Login, fetch the cookie and attach it to the HTTP context for auth.
        var response = await AlbaHost.Scenario(_ =>
        {
            _.Put.Json<PutLoginRequest>(new()
            {
                Email = seedData.Email,
                Password = seedData.Password,
            }).ToUrl("/api/login");
            _.StatusCodeShouldBeOk();
        });
        var headers = response.Context.Response.Headers;
        var cookie = headers["Set-Cookie"].First();
        cookie.Should().NotBeNull();
        return cookie;
    }

    public AuthenticatedWebAppWithDatabaseFixture LoginAsUser(Users user)
    {
        this.albaHttpClient.DefaultRequestHeaders.Remove("Cookie");
        if (user == Users.DM)
        {
            this.albaHttpClient.DefaultRequestHeaders.Add("Cookie", this.SeedData.DMUserData.Cookie);
        }
        else
        {
            this.albaHttpClient.DefaultRequestHeaders.Add("Cookie", this.SeedData.PlayerUserData.Cookie);
        }
        this.CurrentUser = user;

        return this;
    }

    public async Task<HubConnection> GetCombatSignalRConnection(Guid CombatId)
    {
        var serverAddress = this.AlbaHost.Server.BaseAddress.ToString().Replace("http://", "");
        var client = this.AlbaHost.Server.CreateWebSocketClient();
        string currentCookie = this.CurrentUser switch
        {
            Users.DM => this.SeedData.DMUserData.Cookie,
            Users.Player => this.SeedData.PlayerUserData.Cookie,
            _ => throw new NotImplementedException(),
        };
        var cookieContainer = new CookieContainer();
        var cookie = currentCookie.ReplaceFirst(".AspNetCore.Cookies=", "");
        var parsedCookieValue = ParseCookieString(cookie);
        cookieContainer.Add(this.AlbaHost.Server.BaseAddress, parsedCookieValue);
        var httpHandler = this.AlbaHost.Server.CreateHandler();

        var hubConnection = new HubConnectionBuilder()
            .WithUrl($"ws://{serverAddress}combatHub", o =>
            {
                o.HttpMessageHandlerFactory = _ => httpHandler;
                o.Cookies = cookieContainer;
            })
            .Build();

        await hubConnection.StartAsync();
        await hubConnection.SendAsync("JoinCombat", CombatId);

        return hubConnection;
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
            await this.PostgreSqlContainer.StopAsync();
        }
    }

}