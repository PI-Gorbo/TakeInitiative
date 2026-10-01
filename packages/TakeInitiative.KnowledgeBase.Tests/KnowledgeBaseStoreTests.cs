using Npgsql;

using TakeInitiative.KnowledgeBase.Store;

namespace TakeInitiative.KnowledgeBase.Tests;

/// <summary>
/// The store's diff, upsert and prune, against real Postgres.
/// </summary>
/// <remarks>
/// <para>
/// These are the tests the sub-step exists for. The parser is held still by 26b's golden file; what
/// 26c adds is a write path with rules about when it may delete, and those rules are the difference
/// between "some rows are stale" and "a user's links broke". Each one is asserted against the
/// database rather than against the report, because the report is what an operator reads and the rows
/// are what they keep.
/// </para>
/// <para>
/// Tests isolate themselves by <b>provider</b>, which is the first half of the primary key and the
/// unit the prune threshold is measured in, so there is one container and no per-test schema
/// rebuild.
/// </para>
/// </remarks>
[Collection(PostgresCollection.Name)]
public class KnowledgeBaseStoreTests(PostgresFixture postgres)
{
    // --- insert, re-run, change one field --------------------------------------------------------

    /// <summary>A first run is all new, and every column arrives.</summary>
    [Fact]
    public async Task A_first_run_inserts_every_row()
    {
        var provider = Provider();
        var store = Store();
        var rows = KnowledgeBaseRow.From(provider, Fixture5eTools.Build().Index);

        var report = await store.UpsertAsync(provider, rows, Guid.NewGuid());

        Assert.Equal(rows.Count, report.New);
        Assert.Equal(0, report.Updated);
        Assert.Equal(0, report.Unchanged);
        Assert.Equal(0, report.Missing);
        Assert.Equal(0, report.StoredBefore);

        var stored = await store.ReadAllAsync(provider);
        Assert.Equal(rows.Count, stored.Count);

        var beholder = rows.First(row => row.Category == "Monster" && row.Stats is not null);
        var written = stored.Single(row => row.Id == beholder.Id);
        Assert.Equal(beholder.Name, written.Name);
        Assert.Equal(beholder.Category, written.Category);
        Assert.Equal(beholder.SourceBook, written.SourceBook);
        Assert.Equal(beholder.Page, written.Page);
        Assert.Equal(beholder.Label, written.Label);
        Assert.Equal(beholder.Url, written.Url);
        Assert.Equal(beholder.ContentHash, written.ContentHash);
        Assert.False(written.Stale);
        // 26g is the step that fills this in; until then the column is there and empty.
        Assert.Null(written.ImageUrl);
    }

    /// <summary>
    /// A monster's numbers arrive as <c>jsonb</c>, with the three keys the encounter builder reads.
    /// </summary>
    [Fact]
    public async Task A_monsters_stats_arrive_as_json()
    {
        var provider = Provider();
        var store = Store();
        var row = Row("monster_beholder_mm", stats: new KnowledgeBaseItemStats(18, "18d10+36", 2));

        await store.UpsertAsync(provider, [row], Guid.NewGuid());

        var stored = Assert.Single(await store.ReadAllAsync(provider));
        Assert.NotNull(stored.Stats);
        Assert.Contains("\"ac\": 18", stored.Stats);
        Assert.Contains("\"hp\": \"18d10+36\"", stored.Stats);
        Assert.Contains("\"initiativeBonus\": 2", stored.Stats);
    }

    /// <summary>Every row carries its book's title, which the parser reads from the header.</summary>
    [Fact]
    public async Task Every_row_carries_its_books_title()
    {
        var provider = Provider();
        var store = Store();
        var index = Fixture5eTools.Build().Index;

        await store.UpsertAsync(provider, KnowledgeBaseRow.From(provider, index), Guid.NewGuid());

        var stored = await store.ReadAllAsync(provider);
        Assert.All(stored, row => Assert.Equal(index.Sources[row.SourceBook], row.SourceTitle));
    }

    /// <summary>
    /// The second run of an unchanged source reports all-unchanged and writes nothing at all — not
    /// even a new batch stamp. "Writes nothing" is a fact about the rows, not about the report.
    /// </summary>
    [Fact]
    public async Task Re_running_an_unchanged_source_writes_nothing()
    {
        var provider = Provider();
        var store = Store();
        var rows = KnowledgeBaseRow.From(provider, Fixture5eTools.Build().Index);
        var first = Guid.NewGuid();

        await store.UpsertAsync(provider, rows, first);
        var before = await store.ReadAllAsync(provider);

        var report = await store.UpsertAsync(provider, rows, Guid.NewGuid());

        Assert.Equal(0, report.New);
        Assert.Equal(0, report.Updated);
        Assert.Equal(rows.Count, report.Unchanged);
        Assert.False(report.HasWrites);

        var after = await store.ReadAllAsync(provider);
        Assert.Equal(before, after);
        Assert.All(after, row => Assert.Equal(first, row.Batch));
    }

    /// <summary>
    /// One changed field is one updated row, and only that row is rewritten: every other row keeps
    /// the batch it arrived with.
    /// </summary>
    [Fact]
    public async Task Changing_one_field_updates_one_row()
    {
        var provider = Provider();
        var store = Store();
        var index = Fixture5eTools.Build().Index;
        var rows = KnowledgeBaseRow.From(provider, index);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await store.UpsertAsync(provider, rows, first);

        // A page number moved, which is exactly the kind of small edit 5eTools makes between
        // releases and exactly what `updated` has to be able to mean.
        var moved = index.Items[0];
        var changed = rows
            .Select(row => row.Id == moved.Id
                ? KnowledgeBaseRow.From(provider, moved with { Page = (moved.Page ?? 0) + 1 }, row.SourceTitle)
                : row)
            .ToList();

        var report = await store.UpsertAsync(provider, changed, second);

        Assert.Equal(0, report.New);
        Assert.Equal(1, report.Updated);
        Assert.Equal(rows.Count - 1, report.Unchanged);

        var stored = await store.ReadAllAsync(provider);
        var rewritten = stored.Single(row => row.Id == moved.Id);
        Assert.Equal((moved.Page ?? 0) + 1, rewritten.Page);
        Assert.Equal(second, rewritten.Batch);
        Assert.All(stored.Where(row => row.Id != moved.Id), row => Assert.Equal(first, row.Batch));
    }

    /// <summary>A dry run reads the corpus, reports the diff and writes nothing.</summary>
    [Fact]
    public async Task A_dry_run_writes_nothing()
    {
        var provider = Provider();
        var store = Store();
        var rows = KnowledgeBaseRow.From(provider, Fixture5eTools.Build().Index);

        var outcome = await store.RunAsync(new IngestRun
        {
            Provider = provider,
            Rows = rows,
            DryRun = true,
        });

        Assert.Equal(rows.Count, outcome.Report.New);
        Assert.Null(outcome.Prune);
        Assert.Empty(await store.ReadAllAsync(provider));
    }

    // --- a missing row, with and without --prune -------------------------------------------------

    /// <summary>
    /// The important one. A source that has lost rows is reported, and a plain run deletes none of
    /// them. Point the CLI at one bestiary file instead of thirty and the worst outcome is rows that
    /// are out of date, not links that broke.
    /// </summary>
    [Fact]
    public async Task A_plain_run_never_deletes_a_missing_row()
    {
        var provider = Provider();
        var store = Store();
        var everything = KnowledgeBaseRow.From(provider, Fixture5eTools.Build().Index);
        await store.UpsertAsync(provider, everything, Guid.NewGuid());

        // One row instead of the whole corpus: a half-finished download.
        var partial = everything.Take(1).ToList();

        var outcome = await store.RunAsync(new IngestRun { Provider = provider, Rows = partial });

        Assert.Equal(everything.Count - 1, outcome.Report.Missing);
        Assert.Null(outcome.Prune);
        Assert.Equal(everything.Count, (await store.ReadAllAsync(provider)).Count);
        Assert.All(await store.ReadAllAsync(provider), row => Assert.False(row.Stale));
    }

    /// <summary>
    /// <c>--prune</c> under the threshold deletes. Ten rows, two of them gone, is exactly 20% — the
    /// limit is "more than", so this goes through.
    /// </summary>
    [Fact]
    public async Task Prune_deletes_a_missing_row_under_the_threshold()
    {
        var provider = Provider();
        var store = Store();
        var ten = Rows(provider, 10);
        await store.UpsertAsync(provider, ten, Guid.NewGuid());

        var eight = ten.Take(8).ToList();
        var outcome = await store.RunAsync(new IngestRun { Provider = provider, Rows = eight, Prune = true });

        Assert.NotNull(outcome.Prune);
        Assert.False(outcome.Prune.Refused);
        Assert.Equal(2, outcome.Prune.Considered);
        Assert.Equal(2, outcome.Prune.Deleted);
        Assert.Equal(0, outcome.Prune.MarkedStale);

        var stored = await store.ReadAllAsync(provider);
        Assert.Equal(8, stored.Count);
        Assert.DoesNotContain(ten[8].Id, stored.Select(row => row.Id));
        Assert.DoesNotContain(ten[9].Id, stored.Select(row => row.Id));
    }

    /// <summary>
    /// Past the threshold a prune refuses, and the whole run is rolled back — not the deletions
    /// alone. An operator who pointed at the wrong folder gets their corpus back exactly as it was,
    /// rather than a corpus with the wrong folder's rows inserted into it.
    /// </summary>
    [Fact]
    public async Task Prune_refuses_over_the_threshold_and_writes_nothing()
    {
        var provider = Provider();
        var store = Store();
        var ten = Rows(provider, 10);
        await store.UpsertAsync(provider, ten, Guid.NewGuid());
        var before = await store.ReadAllAsync(provider);

        // Three of ten is 30%, and one row is new, so there is something to roll back as well as
        // something to refuse.
        var partial = ten.Take(7).Append(Row("id-new", provider: provider)).ToList();

        var outcome = await store.RunAsync(new IngestRun { Provider = provider, Rows = partial, Prune = true });

        Assert.True(outcome.PruneRefused);
        Assert.NotNull(outcome.Prune);
        Assert.Equal(3, outcome.Prune.Considered);
        Assert.Equal(0, outcome.Prune.Deleted);
        Assert.Equal(0, outcome.Prune.MarkedStale);
        // Eleven, not ten: the prune runs after the upsert inside the same transaction, so it
        // measures the corpus as it would stand — the source's new row included. 3 of 11 is 27%,
        // over the limit.
        Assert.Equal(11, outcome.Prune.StoredBefore);
        Assert.Equal(0.273, outcome.Prune.Share, 3);

        // Nothing at all: the three are still there and the new row never arrived.
        Assert.Equal(before, await store.ReadAllAsync(provider));
    }

    /// <summary><c>--force</c> is the second keystroke, and it goes through.</summary>
    [Fact]
    public async Task Prune_with_force_deletes_over_the_threshold()
    {
        var provider = Provider();
        var store = Store();
        var ten = Rows(provider, 10);
        await store.UpsertAsync(provider, ten, Guid.NewGuid());

        var one = ten.Take(1).ToList();
        var outcome = await store.RunAsync(new IngestRun
        {
            Provider = provider,
            Rows = one,
            Prune = true,
            Force = true,
        });

        Assert.NotNull(outcome.Prune);
        Assert.False(outcome.Prune.Refused);
        Assert.Equal(9, outcome.Prune.Deleted);

        var stored = Assert.Single(await store.ReadAllAsync(provider));
        Assert.Equal(one[0].Id, stored.Id);
    }

    /// <summary>A prune of a provider whose source has everything does nothing.</summary>
    [Fact]
    public async Task Prune_with_nothing_missing_does_nothing()
    {
        var provider = Provider();
        var store = Store();
        var ten = Rows(provider, 10);
        await store.UpsertAsync(provider, ten, Guid.NewGuid());

        var prune = await store.PruneAsync(provider, [.. ten.Select(row => row.Id)], force: false);

        Assert.Equal(0, prune.Considered);
        Assert.Equal(0, prune.Deleted);
        Assert.False(prune.Refused);
        Assert.Equal(10, (await store.ReadAllAsync(provider)).Count);
    }

    /// <summary>
    /// A prune is per provider. Another corpus's rows are neither counted towards the threshold nor
    /// touched, which is what makes one table for the whole knowledge base safe.
    /// </summary>
    [Fact]
    public async Task Prune_leaves_another_provider_alone()
    {
        var mine = Provider();
        var theirs = Provider();
        var store = Store();
        await store.UpsertAsync(mine, Rows(mine, 10), Guid.NewGuid());
        await store.UpsertAsync(theirs, Rows(theirs, 10), Guid.NewGuid());

        await store.PruneAsync(mine, [], force: true);

        Assert.Empty(await store.ReadAllAsync(mine));
        Assert.Equal(10, (await store.ReadAllAsync(theirs)).Count);
    }

    // --- the link protection ---------------------------------------------------------------------

    /// <summary>
    /// A row an entry links to is never deleted, even with <c>--force</c>. It is marked <c>stale</c>,
    /// so the link still resolves and step 27's renderer can say the source is gone. A link is
    /// something a user made; the ingest does not get to erase it.
    /// </summary>
    [Fact]
    public async Task A_linked_row_is_marked_stale_instead_of_deleted()
    {
        var provider = Provider();
        var ten = Rows(provider, 10);
        var linked = new StubLinks(ten[8].Id);
        var store = Store(linked);
        await store.UpsertAsync(provider, ten, Guid.NewGuid());

        var prune = await store.PruneAsync(provider, [.. ten.Take(8).Select(row => row.Id)], force: true);

        Assert.Equal(2, prune.Considered);
        Assert.Equal(1, prune.Deleted);
        Assert.Equal(1, prune.MarkedStale);

        var stored = await store.ReadAllAsync(provider);
        Assert.Equal(9, stored.Count);
        Assert.True(stored.Single(row => row.Id == ten[8].Id).Stale);
        Assert.DoesNotContain(ten[9].Id, stored.Select(row => row.Id));
        Assert.All(stored.Where(row => row.Id != ten[8].Id), row => Assert.False(row.Stale));
    }

    /// <summary>
    /// The query's shape, which is the part step 27 has to satisfy: the store asks about its own
    /// provider and about exactly the ids it is going to remove — not the whole corpus, and not the
    /// ids it is keeping.
    /// </summary>
    /// <remarks>
    /// This is what the plan means by "assert the query, not an entry". There is no entry link to
    /// assert against until step 27 adds one, and inventing a table to query would be a test of the
    /// invention. What is real and testable now is the question: who is asked, about what.
    /// </remarks>
    [Fact]
    public async Task Prune_asks_the_link_source_about_exactly_the_candidates()
    {
        var provider = Provider();
        var ten = Rows(provider, 10);
        var links = new StubLinks();
        var store = Store(links);
        await store.UpsertAsync(provider, ten, Guid.NewGuid());

        await store.PruneAsync(provider, [.. ten.Take(8).Select(row => row.Id)], force: false);

        Assert.Equal(1, links.Asked);
        Assert.Equal(provider, links.AskedProvider);
        Assert.Equal(
            new[] { ten[8].Id, ten[9].Id }.Order(StringComparer.Ordinal),
            links.AskedIds.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A refused prune does not even ask. The threshold comes first, so an implementation of the seam
    /// that is slow, or that reads the whole entry table, is not paid for on the run that was a
    /// mistake.
    /// </summary>
    [Fact]
    public async Task A_refused_prune_never_asks_about_links()
    {
        var provider = Provider();
        var ten = Rows(provider, 10);
        var links = new StubLinks();
        var store = Store(links);
        await store.UpsertAsync(provider, ten, Guid.NewGuid());

        var prune = await store.PruneAsync(provider, [.. ten.Take(5).Select(row => row.Id)], force: false);

        Assert.True(prune.Refused);
        Assert.Equal(0, links.Asked);
    }

    /// <summary>
    /// A prune with nothing to remove does not ask either — the store does not ask about nothing,
    /// which is what lets an implementation assume a non-empty candidate set.
    /// </summary>
    [Fact]
    public async Task A_prune_with_nothing_missing_never_asks_about_links()
    {
        var provider = Provider();
        var ten = Rows(provider, 10);
        var links = new StubLinks();
        var store = Store(links);
        await store.UpsertAsync(provider, ten, Guid.NewGuid());

        await store.PruneAsync(provider, [.. ten.Select(row => row.Id)], force: false);

        Assert.Equal(0, links.Asked);
    }

    /// <summary>
    /// The default protects nothing, and that is deliberate: until step 27 there is no way for a row
    /// to be linked, and a default that protected everything would make <c>--prune --force</c>
    /// silently do nothing — a worse failure than deleting a row nothing could have pointed at.
    /// </summary>
    [Fact]
    public async Task The_default_link_source_protects_nothing()
    {
        var provider = Provider();
        var store = Store();
        var ten = Rows(provider, 10);
        await store.UpsertAsync(provider, ten, Guid.NewGuid());

        var prune = await store.PruneAsync(provider, [], force: true);

        Assert.Equal(10, prune.Deleted);
        Assert.Equal(0, prune.MarkedStale);
        Assert.Empty(await store.ReadAllAsync(provider));
    }

    /// <summary>
    /// An implementation that answered with an id outside the set it was asked about cannot stale a
    /// row this prune was never looking at.
    /// </summary>
    [Fact]
    public async Task A_link_source_cannot_protect_a_row_outside_the_candidates()
    {
        var provider = Provider();
        var ten = Rows(provider, 10);
        // It names a row the source still has, which is not a prune candidate at all.
        var store = Store(new StubLinks(ten[0].Id));
        await store.UpsertAsync(provider, ten, Guid.NewGuid());

        var prune = await store.PruneAsync(provider, [.. ten.Take(8).Select(row => row.Id)], force: true);

        Assert.Equal(2, prune.Deleted);
        Assert.Equal(0, prune.MarkedStale);
        Assert.All(await store.ReadAllAsync(provider), row => Assert.False(row.Stale));
    }

    // --- stale comes off again -------------------------------------------------------------------

    /// <summary>
    /// A row a prune flagged, which the source has back, is <c>updated</c> even though its hash never
    /// moved — and the flag comes off. Counting it as unchanged would leave <c>stale</c> on for good
    /// and have an entry's link claim the source is gone when it is not.
    /// </summary>
    [Fact]
    public async Task A_stale_row_the_source_has_back_is_updated_and_unstaled()
    {
        var provider = Provider();
        var ten = Rows(provider, 10);
        var store = Store(new StubLinks(ten[9].Id));
        var first = Guid.NewGuid();
        await store.UpsertAsync(provider, ten, first);
        await store.PruneAsync(provider, [.. ten.Take(9).Select(row => row.Id)], force: true);
        Assert.True((await store.ReadAllAsync(provider)).Single(row => row.Id == ten[9].Id).Stale);

        var second = Guid.NewGuid();
        var report = await store.UpsertAsync(provider, ten, second);

        Assert.Equal(0, report.New);
        Assert.Equal(1, report.Updated);
        Assert.Equal(9, report.Unchanged);

        var stored = await store.ReadAllAsync(provider);
        var revived = stored.Single(row => row.Id == ten[9].Id);
        Assert.False(revived.Stale);
        Assert.Equal(second, revived.Batch);
        Assert.All(stored.Where(row => row.Id != ten[9].Id), row => Assert.Equal(first, row.Batch));
    }

    // --- the schema is the API's ----------------------------------------------------------------

    /// <summary>
    /// Against a database the API has never started on, the store refuses rather than creating the
    /// table. One owner for the schema: a CLI that ran DDL could leave an operator with a table
    /// Marten would then try to migrate into a different shape.
    /// </summary>
    [Fact]
    public async Task The_store_refuses_a_database_with_no_table()
    {
        var connectionString = await postgres.FreshDatabaseAsync("kb_no_schema");
        var store = new KnowledgeBaseStore(connectionString);

        var error = await Assert.ThrowsAsync<KnowledgeBaseSchemaMissingException>(
            () => store.DiffAsync("5etools", [Row("id-1")]));

        Assert.Contains("knowledge_base_item", error.Message, StringComparison.Ordinal);
        Assert.Contains("does not exist", error.Message, StringComparison.Ordinal);

        // And still nothing: it did not fall back to creating one.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from pg_tables where schemaname = 'public'";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    // --- helpers ---------------------------------------------------------------------------------

    /// <summary>A provider nothing else in the suite writes to.</summary>
    private static string Provider() => $"test-{Guid.NewGuid():N}";

    private KnowledgeBaseStore Store(IKnowledgeBaseLinks? links = null) =>
        new(postgres.ConnectionString, links);

    /// <summary><paramref name="count" /> rows with ids that sort in the order they were made.</summary>
    private static IReadOnlyList<KnowledgeBaseRow> Rows(string provider, int count) =>
        [.. Enumerable.Range(0, count).Select(i => Row($"monster_test-{i:D3}_tst", $"Test {i:D3}", provider: provider))];

    private static KnowledgeBaseRow Row(
        string id,
        string name = "Beholder",
        string provider = "5etools",
        KnowledgeBaseItemStats? stats = null) =>
        KnowledgeBaseRow.From(
            provider,
            new KnowledgeBaseItem
            {
                Id = id,
                Name = name,
                Category = "Monster",
                Source = "TST",
                Page = 28,
                Url = $"https://5e.tools/bestiary.html#{id}",
                Label = "CR 13 · Large Aberration",
                Stats = stats,
            },
            "The Test Manual");

    /// <summary>
    /// A stand-in for step 27's link reader: it answers with the ids it was told to, and records what
    /// it was asked. See <see cref="IKnowledgeBaseLinks" /> for why the real one does not exist yet.
    /// </summary>
    private sealed class StubLinks(params string[] linked) : IKnowledgeBaseLinks
    {
        public int Asked { get; private set; }

        public string? AskedProvider { get; private set; }

        public IReadOnlyCollection<string> AskedIds { get; private set; } = [];

        public Task<IReadOnlySet<string>> LinkedIdsAsync(
            string provider,
            IReadOnlyCollection<string> candidateIds,
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            Asked++;
            AskedProvider = provider;
            AskedIds = [.. candidateIds];

            // The prune's own connection and transaction, which is what lets step 27's reader see the
            // run it is part of rather than a snapshot from before it.
            Assert.Equal(System.Data.ConnectionState.Open, connection.State);
            Assert.NotNull(transaction);
            Assert.NotEmpty(candidateIds);

            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(linked, StringComparer.Ordinal));
        }
    }
}
