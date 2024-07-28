
using Alba;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TakeInitiative.Api.Bootstrap;
using Testcontainers.PostgreSql;
namespace TakeInitiative.Api.Tests.Integration;

public class WebAppWithDatabaseFixture : IAsyncLifetime, IWebAppClient
{
    public IAlbaHost AlbaHost { get; private set; } = null!;

    public PostgreSqlContainer PostgreSqlContainer { get; private set; } = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await PostgreSqlContainer.StartAsync();
        AlbaHost = await Alba.AlbaHost.For<TakeInitiative.Api.Program>(x =>
            x.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:TakeDB"] = PostgreSqlContainer.GetConnectionString()
                        });
                })
           .ConfigureServices((context, services) =>
                {
                    services.AddMartenDB(context.Configuration, IsDevelopment: true);
                })
        );
    }
    public async Task DisposeAsync()
    {
        if (AlbaHost != null)
        {
            await AlbaHost.StopAsync();
        }
        await PostgreSqlContainer.StopAsync();
    }

}