
using Alba;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.Api.Client;
using Testcontainers.PostgreSql;

namespace TakeInitiative.Api.Tests.Integration;

public class AuthenticatedWebAppWithDatabaseFixture : IAsyncLifetime, IWebAppClient
{
    public IAlbaHost AlbaHost { get; private set; } = null!;
    public PostgreSqlContainer PostgreSqlContainer { get; private set; } = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();
    public TakeApiClient Client { get; private set; }

    IAlbaHost IWebAppClient.AlbaHost => throw new NotImplementedException();

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
                    services.AddMartenDB(context.Configuration, IsDevelopment: true);
                })
        );

        // Create a user in the database, with a campaign.
        this.Client = new TakeApiClient(AlbaHost.Server.BaseAddress.ToString(), AlbaHost.GetTestClient());

        // Sign Up.
        var signup = await Result.Try(() => Client.TakeInitiativeApiFeaturesUsersPostSignUpAsync(new PostSignUpRequest() { Username = "TESTING", Password = "Besbing!99", Email = "testing@testing.com" }));
        signup.Should().Succeed();
        // Login and get cookie???
        var login = await Result.Try(() => Client.TakeInitiativeApiFeaturesUsersPutLoginAsync(new PutLoginRequest() { Email = "testing@testing.com", Password = "Besbing!99" }));
        login.Should().Succeed();

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