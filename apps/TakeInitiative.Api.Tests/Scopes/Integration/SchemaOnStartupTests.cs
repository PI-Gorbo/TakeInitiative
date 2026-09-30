using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using TakeInitiative.Api.Bootstrap;
using TakeInitiative.KnowledgeBase.Schema;
using Testcontainers.PostgreSql;

using ApiBootstrap = TakeInitiative.Api.Bootstrap.Bootstrap;

namespace TakeInitiative.Api.Tests.Integration;

/// <summary>
/// The startup schema migration (26c). <c>ApplyAllDatabaseChangesOnStartup()</c> used to be guarded
/// by <c>if (IsDevelopment)</c>, and the API's dockerfile sets no <c>ASPNETCORE_ENVIRONMENT</c>: a
/// container ran as Production and created nothing at all, not even <c>pg_trgm</c> and
/// <c>unaccent</c>, which hang off no document type and so are never reached by Marten's lazy
/// per-document auto-create. ⌘K's <c>word_similarity()</c> would have failed at query time.
/// <para>
/// These hosts run as Production — <see cref="HostBuilder"/>'s default environment — so what they
/// exercise is exactly the deployed path, with nothing but the connection string and the flag in
/// configuration. Each case gets its own database on one container, because an extension belongs to
/// a database rather than to a schema and the whole point is which extensions exist.
/// </para>
/// <para>
/// <see cref="TheSetting_Absent_AppliesTheSchema"/> is also what keeps the integration fixtures
/// working: they used to pass <c>IsDevelopment: true</c> and now say nothing, so their schema comes
/// from this default. Their side of it is asserted by the search suite, which reads the extensions
/// and the indexes out of the fixture's own database.
/// </para>
/// </summary>
public class SchemaOnStartupTests(SchemaOnStartupTests.PostgresFixture fixture) : IClassFixture<SchemaOnStartupTests.PostgresFixture>
{
    /// <summary>One container for the class; each test makes its own database inside it.</summary>
    public sealed class PostgresFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .Build();

        public string AdminConnectionString => _postgres.GetConnectionString();

        public Task InitializeAsync() => _postgres.StartAsync();

        public Task DisposeAsync() => _postgres.StopAsync();
    }

    [Fact]
    public async Task TheFlag_Off_LeavesTheSchemaAlone()
    {
        var connectionString = await FreshDatabase("apply_schema_off");

        await StartAndStop(connectionString, applySchemaOnStartup: false);

        (await Extensions(connectionString)).Should().NotContain("pg_trgm").And.NotContain("unaccent");
        (await Tables(connectionString)).Should().BeEmpty("nothing in the startup path applies DDL when the flag is off");
    }

    [Fact]
    public async Task TheFlag_On_AppliesTheSchema()
    {
        var connectionString = await FreshDatabase("apply_schema_on");

        await StartAndStop(connectionString, applySchemaOnStartup: true);

        await AssertTheSchemaIsThere(connectionString);
    }

    [Fact]
    public async Task TheSetting_Absent_AppliesTheSchema()
    {
        var connectionString = await FreshDatabase("apply_schema_default");

        await StartAndStop(connectionString, applySchemaOnStartup: null);

        // The default matters on its own: a fresh deployment has to work without anyone knowing the
        // setting exists.
        await AssertTheSchemaIsThere(connectionString);
    }

    private static async Task AssertTheSchemaIsThere(string connectionString)
    {
        (await Extensions(connectionString)).Should().Contain("pg_trgm").And.Contain("unaccent");

        var tables = await Tables(connectionString);
        // The event store and one document table per registered type, all before the host finished
        // starting and therefore before Kestrel would open the port.
        tables.Should().Contain("mt_events").And.Contain("mt_streams");
        tables.Should().Contain("mt_doc_entry").And.Contain("mt_doc_campaign").And.Contain("mt_doc_sessionnote");

        // The knowledge base's table (26c). It hangs off no document type — it is written by a CLI and
        // has no aggregate — so, exactly like the two extensions above, nothing in Marten's lazy
        // per-document auto-create would ever reach it. It exists because it is an extended schema
        // object and this setting applies them.
        tables.Should().Contain(KnowledgeBaseSchema.TableName);

        // And its three indexes, which is also the assertion that the ordering holds: the trigram
        // index is `using gin (lower(name) gin_trgm_ops)`, so it can only have been created after
        // pg_trgm was. If Weasel ever stopped writing extended schema objects in the order they were
        // added, the start above would have failed outright.
        var indexes = await Indexes(connectionString, KnowledgeBaseSchema.TableName);
        indexes.Should()
            .Contain(KnowledgeBaseSchema.SearchIndexName)
            .And.Contain(KnowledgeBaseSchema.NameTrigramIndexName)
            .And.Contain(KnowledgeBaseSchema.FilterIndexName);
    }

    /// <summary>
    /// Starts a host with nothing but <c>AddMartenDB</c>, lets its hosted services
    /// run, and stops it. Marten applies the schema as one of them, so everything the assertions
    /// look at was written before <c>StartAsync</c> returned.
    /// </summary>
    private static async Task StartAndStop(string connectionString, bool? applySchemaOnStartup)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:TakeDB"] = connectionString,
        };
        if (applySchemaOnStartup is not null)
        {
            settings[ApiBootstrap.ApplySchemaOnStartupKey] = applySchemaOnStartup.Value ? "true" : "false";
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        using var host = new HostBuilder()
            .UseEnvironment(Environments.Production)
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(config);
                services.AddLogging();
                services.AddMartenDB(config);
            })
            .Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    private async Task<string> FreshDatabase(string name)
    {
        await using var admin = new NpgsqlConnection(fixture.AdminConnectionString);
        await admin.OpenAsync();
        await using var command = admin.CreateCommand();
        // The name is a constant from this file, never input; Postgres takes no parameter here.
        command.CommandText = $"create database \"{name}\"";
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(fixture.AdminConnectionString) { Database = name }.ConnectionString;
    }

    private static Task<List<string>> Extensions(string connectionString) =>
        Query(connectionString, "select extname from pg_extension where extname <> 'plpgsql'");

    private static Task<List<string>> Tables(string connectionString) =>
        Query(connectionString, "select tablename from pg_tables where schemaname = 'public'");

    private static Task<List<string>> Indexes(string connectionString, string table) =>
        // The table name is a constant from the knowledge base's schema, never input.
        Query(
            connectionString,
            $"select indexname from pg_indexes where schemaname = 'public' and tablename = '{table}'");

    private static async Task<List<string>> Query(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }
}
