
using System.Net.Http.Headers;
using Alba;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
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

public record AuthenticatedWebAppWithDatabaseFixtureSeededData
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public required string Email { get; set; }
    public required string CampaignName { get; set; }
    public required Guid CampaignId { get; set; }
}

public class AuthenticatedWebAppWithDatabaseFixture : IAsyncLifetime, IWebAppClient
{
    public IAlbaHost AlbaHost { get; private set; } = null!;
    public PostgreSqlContainer PostgreSqlContainer { get; private set; } = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();
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
        var httpClient = AlbaHost.GetTestClient();
        this.Client = new TakeApiClient(AlbaHost.Server.BaseAddress.ToString(), httpClient);

        // Seed database with tiny seed.
        // Sign Up.
        var signup = await Result.Try(() => Client.TakeInitiativeApiFeaturesUsersPostSignUpAsync(new PostSignUpRequest() { Username = "TESTING", Password = "Besbing!99", Email = "testing@testing.com" }));
        signup.Should().Succeed();

        // Login, fetch the cookie and attach it to the HTTP context for auth.
        var response = await AlbaHost.Scenario(_ =>
        {
            _.Put.Json<PutLoginRequest>(new()
            {
                Email = "testing@testing.com",
                Password = "Besbing!99"
            }).ToUrl("/api/login");
            _.StatusCodeShouldBeOk();
        });
        var headers = response.Context.Response.Headers;
        var cookie = headers["Set-Cookie"].First();
        httpClient.DefaultRequestHeaders.Add("Cookie", cookie);

        // Create a campaign called 'Super Testing Campaign'
        var createResponse = await Result.Try(() => Client.TakeInitiativeApiFeaturesCampaignsPostCreateCampaignAsync(new() { CampaignName = "Super Testing Campaign" }));
        createResponse.Should().Succeed();

        this.SeedData = new()
        {
            CampaignName = createResponse.Value.CampaignName,
            CampaignId = createResponse.Value.Id,
            Email = "testing@testing.com",
            Password = "Besbing!99",
            Username = "TESTING"
        };
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