using Npgsql;

using TakeInitiative.KnowledgeBase.Schema;

using Testcontainers.PostgreSql;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// One Postgres container for every store test, with the knowledge base's schema applied.
/// </summary>
/// <remarks>
/// <para>
/// <c>postgres:16-alpine</c>, which is what <c>compose.dev.yml</c> and the API's Alba fixtures pin
/// (<c>apps/TakeInitiative.Api.Tests/Scopes/Integration/*Fixture.cs</c>). The schema has a generated
/// column and a <c>gin_trgm_ops</c> index in it; the version those are exercised against should be
/// the version dev and CI run, not whatever <c>latest</c> is this month.
/// </para>
/// <para>
/// <b>The schema is applied from <see cref="KnowledgeBaseSchema.CreateSql" />, which is the same SQL
/// the API runs on startup.</b> That is the point of the DDL being a constant in the package rather
/// than a Weasel table built in the API: these tests write to the table the API creates, not to a
/// hand-rolled approximation of it, so a column that is spelled differently in one of the two places
/// fails here rather than on an operator's machine.
/// </para>
/// <para>
/// Tests share the container and the database and isolate themselves by <b>provider</b> instead,
/// which is the first half of the primary key and the unit the prune threshold is measured in. A
/// test that needs a database with no knowledge base at all — the one that asserts the store refuses
/// to create one — takes a fresh database from <see cref="FreshDatabaseAsync" />.
/// </para>
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    /// <summary>The database the store tests write to. Its schema is applied.</summary>
    public string ConnectionString => postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        await ApplySchemaAsync(ConnectionString);
    }

    public Task DisposeAsync() => postgres.StopAsync();

    /// <summary>
    /// A new, empty database on the same container, with no knowledge base in it.
    /// </summary>
    public async Task<string> FreshDatabaseAsync(string name)
    {
        await using var admin = new NpgsqlConnection(ConnectionString);
        await admin.OpenAsync();
        await using var command = admin.CreateCommand();
        // The name is a constant from a test, never input; Postgres takes no parameter here.
        command.CommandText = $"create database \"{name}\"";
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>
    /// What the API does on startup: the <c>pg_trgm</c> extension the trigram index needs, then the
    /// table.
    /// </summary>
    public static async Task ApplySchemaAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var extension = connection.CreateCommand();
        extension.CommandText = "create extension if not exists pg_trgm;";
        await extension.ExecuteNonQueryAsync();

        await using var schema = connection.CreateCommand();
        schema.CommandText = KnowledgeBaseSchema.CreateSql();
        await schema.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// The collection every store test belongs to, so one container is started for all of them rather
/// than one per class.
/// </summary>
[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
