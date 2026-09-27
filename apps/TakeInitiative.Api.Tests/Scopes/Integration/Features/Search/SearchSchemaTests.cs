using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TakeInitiative.Api.Features.Search;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// The schema ⌘K needs (17a.1 and 17a.3): the two extensions, no churn on the two expression
/// indexes, and Postgres actually using them.
/// <para>
/// The index check is the one that protects the design. Postgres rewrites an expression when it
/// stores it (<c>'simple'::regconfig</c>, <c>'Text'::text</c>, its own bracketing), so a constant
/// written in the wrong form would leave Weasel seeing a difference on every start and dropping
/// and recreating a GIN index over the whole table each time.
/// </para>
/// </summary>
public class SearchSchemaTests(WebAppWithDatabaseFixture fixture) : IClassFixture<WebAppWithDatabaseFixture>
{
    private IDocumentStore Store => fixture.AlbaHost.Services.GetRequiredService<IDocumentStore>();

    private async Task<NpgsqlConnection> Open()
    {
        var connection = (NpgsqlConnection)Store.Storage.Database.CreateConnection();
        await connection.OpenAsync();
        return connection;
    }

    [Theory]
    [InlineData("pg_trgm")]
    [InlineData("unaccent")]
    public async Task TheExtension_IsInstalled(string name)
    {
        await using var connection = await Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from pg_extension where extname = @name";
        command.Parameters.AddWithValue("name", name);
        (await command.ExecuteScalarAsync()).Should().Be(1L, $"the API creates {name} on startup");
    }

    [Fact]
    public async Task TheDatabase_MatchesTheConfiguration_AfterStartup()
    {
        // ApplyAllDatabaseChangesOnStartup has already run. If either index expression were
        // written in a form Weasel cannot match against what Postgres stored, this would fail
        // with the two DDL strings, and a second start would recreate the index.
        await Store.Storage.Database.AssertDatabaseMatchesConfigurationAsync();
    }

    [Theory]
    [InlineData(SearchSql.NoteTable, SearchSql.NoteVector, SearchSql.NoteIndexName)]
    [InlineData(SearchSql.EntryTable, SearchSql.EntryArticleVector, SearchSql.EntryIndexName)]
    public async Task ThePlan_UsesTheSearchIndex_ForTheExpressionTheQueryMatchesWith(string table, string vector, string index)
    {
        // enable_seqscan = off makes the planner say which index it *can* use, whatever the row
        // count is: on an empty or tiny table a sequential scan is cheapest and would hide a
        // mismatch between the index expression and the query's. SET LOCAL, so the session is
        // left alone.
        await using var connection = await Open();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var seqscan = connection.CreateCommand())
        {
            seqscan.CommandText = "SET LOCAL enable_seqscan = off";
            await seqscan.ExecuteNonQueryAsync();
        }

        await using var explain = connection.CreateCommand();
        explain.CommandText =
            $"EXPLAIN SELECT d.id FROM {SearchSql.Table(Store.Options.DatabaseSchemaName, table)} d "
            + $"WHERE {vector} @@ {SearchSql.TsQuery}";
        explain.Parameters.AddWithValue("q", "'gund':*");

        var plan = new List<string>();
        await using (var reader = await explain.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                plan.Add(reader.GetString(0));
            }
        }
        await transaction.RollbackAsync();

        string.Join("\n", plan).Should().Contain($"Bitmap Index Scan on {index}",
            "the query's expression must be the one the index was built on, or Postgres cannot use it");
    }
}
