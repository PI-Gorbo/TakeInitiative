
using Alba;
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
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();
    public TakeApiClient Client { get; private set; }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        this.AlbaHost = await Alba.AlbaHost.For<Api.Program>(x =>
            x.UseEnvironment(Environments.Development)
            .ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:TakeDB"] = _postgres.GetConnectionString()
                        });
                })
           .ConfigureServices((context, services) =>
                {
                    services.AddMartenDB(context.Configuration, IsDevelopment: true);
                })
        );

        // Create a user in the database, with a campaign.
        this.Client = new TakeApiClient(AlbaHost.Server.BaseAddress.ToString(), AlbaHost.GetTestClient());

    }
    public async Task DisposeAsync()
    {
        if (AlbaHost != null)
        {
            await AlbaHost.StopAsync();
        }
        await _postgres.StopAsync();
    }

}