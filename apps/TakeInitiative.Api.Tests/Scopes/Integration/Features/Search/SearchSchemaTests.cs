using FluentAssertions;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TakeInitiative.Api.Features.Search;

namespace TakeInitiative.Api.Tests.Integration.Features.Search;

/// <summary>
/// The schema ⌘K needs (17a.1 and 17a.3): the two extensions, the article vector column, no churn
/// on either index or the column, and Postgres actually using both indexes.
/// <para>
/// The churn checks are the ones that protect the design. Postgres rewrites an expression when it
/// stores it (<c>'simple'::regconfig</c>, <c>'Text'::text</c>, its own bracketing), so a note index
/// constant written in the wrong form would leave Weasel seeing a difference on every start and
/// dropping and recreating a GIN index over the whole table each time. The article vector is a
/// stored generated column instead, and a column Weasel did not know about would be <b>dropped</b>
/// on every start: <see cref="TheDatabase_MatchesTheConfiguration_AfterStartup"/> applies the
/// configuration a second time, as a restart would, and asserts there is nothing left to do.
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
        // ApplyAllDatabaseChangesOnStartup has already run. If the note index expression were
        // written in a form Weasel cannot match against what Postgres stored, or the article
        // vector column were not part of the document table Weasel compares, this would fail with
        // the DDL strings, and a second start would recreate the index or drop the column.
        await Store.Storage.Database.AssertDatabaseMatchesConfigurationAsync();

        // And the restart itself: applying the configuration again changes nothing, and the
        // database still matches afterwards.
        await Store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        await Store.Storage.Database.AssertDatabaseMatchesConfigurationAsync();
    }

    [Fact]
    public async Task TheArticleVector_IsAStoredGeneratedColumn_OfExactlyTheDeclaredExpression()
    {
        // The whole point of the column: Postgres computes the vector when the entry document is
        // written, so a search reads it. `is_generated = ALWAYS` is what makes it maintained in the
        // same statement as the row (SearchConsistencyTests depends on that), and the expression is
        // compared as Postgres gives it back, so the constant stays in canonical form.
        await using var connection = await Open();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "select data_type, is_generated, generation_expression from information_schema.columns "
            + "where table_schema = @schema and table_name = @table and column_name = @column";
        command.Parameters.AddWithValue("schema", Store.Options.DatabaseSchemaName);
        command.Parameters.AddWithValue("table", SearchSql.EntryTable);
        command.Parameters.AddWithValue("column", SearchSql.EntryArticleVectorColumn);

        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue($"{SearchSql.EntryArticleVectorColumn} is part of the document table");
        reader.GetString(0).Should().Be("tsvector");
        reader.GetString(1).Should().Be("ALWAYS", "the column is maintained by Postgres, not by the API");
        reader.GetString(2).Should().Be(SearchSql.EntryArticleGeneration);
    }

    [Fact]
    public async Task TheArticleIndex_IsAGinIndexOnTheColumn()
    {
        await using var connection = await Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "select pg_get_indexdef(@index::regclass)";
        command.Parameters.AddWithValue(
            "index", SearchSql.Table(Store.Options.DatabaseSchemaName, SearchSql.EntryIndexName));

        (await command.ExecuteScalarAsync()).Should().Be(
            $"CREATE INDEX {SearchSql.EntryIndexName} ON "
            + $"{SearchSql.Table(Store.Options.DatabaseSchemaName, SearchSql.EntryTable)} "
            + $"USING gin ({SearchSql.EntryArticleVectorColumn})");
    }

    [Fact]
    public async Task TheEntryTable_KeepsItsRowsInline_SoTheVectorDoesNotToastTheDocument()
    {
        // Without this, adding the vector pushes `data` out of line and every name match pays for
        // detoasting it (see EntryRowFitsInline). It is set by a schema object of its own, after the
        // table exists.
        await using var connection = await Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "select reloptions from pg_class where oid = @table::regclass";
        command.Parameters.AddWithValue(
            "table", SearchSql.Table(Store.Options.DatabaseSchemaName, SearchSql.EntryTable));

        ((string[]?)await command.ExecuteScalarAsync())
            .Should().Contain($"toast_tuple_target={EntryRowFitsInline.ToastTupleTarget}");
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
