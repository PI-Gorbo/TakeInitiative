
using System.Net;
using Alba;
using CSharpFunctionalExtensions;
using FluentAssertions;
using JasperFx.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using TakeInitiative.Api.Bootstrap;
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
    private Users CurrentUser;
    public AuthenticatedWebAppWithDatabaseFixtureSeededData SeedData { get; set; }
    public IInitiativeRoller InitiativeRoller { get; } = Substitute.For<IInitiativeRoller>();

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
                        new ServiceDescriptor(typeof(IInitiativeRoller), InitiativeRoller)
                    );
                    services.AddMartenDB(context.Configuration, IsDevelopment: true);
                })
        );


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

        // Temporary authentication fixed to dm to create the campaign.
        AlbaHost.BeforeEach((c) =>
        {
            c.Request.Headers.Cookie = DmCookie;
        });

        // Create a campaign called 'Super Testing Campaign'
        var createResponse = await this.PostCreateCampaign(new() { CampaignName = "Super Testing Campaign" });
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

        // Overwrite the Before each hook, to implement authentication which is controlled 
        // by the 'CurrentUser' property.
        CurrentUser = Users.DM;
        AlbaHost.BeforeEach((c) =>
        {
            c.Request.Headers.Cookie = CurrentUser switch
            {
                Users.DM => SeedData.DMUserData.Cookie,
                Users.Player => SeedData.PlayerUserData.Cookie,
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
        this.CurrentUser = user;
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
            await this.PostgreSqlContainer.StopAsync();
        }
    }

}