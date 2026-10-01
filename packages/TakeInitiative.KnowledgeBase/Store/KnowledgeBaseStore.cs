using Npgsql;

using NpgsqlTypes;

using TakeInitiative.KnowledgeBase.Schema;

namespace TakeInitiative.KnowledgeBase.Store;

/// <summary>
/// Reads and writes <c>knowledge_base_item</c>. Plain Npgsql: no Marten, no ASP.NET, no host — the
/// ingest is a command-line tool and the store has to run from one with nothing built around it.
/// </summary>
/// <remarks>
/// <para>
/// <b>It never creates the table.</b> The API owns the schema
/// (<see cref="KnowledgeBaseSchema" />); this class checks that it is there
/// (<see cref="AssertSchemaAsync" />) and refuses to work if it is not. One owner for the schema
/// means an operator can never end up with a table the API would then try to migrate into a
/// different shape.
/// </para>
/// <para>
/// <b>The safety rules are the point of this class</b>, and they are all here rather than in the
/// CLI, so they are testable without a process and cannot be bypassed by a second caller:
/// </para>
/// <list type="bullet">
///   <item><description>
///   <see cref="UpsertAsync" /> inserts and updates. It has no delete. There is no flag that makes it
///   delete. Point the CLI at half a download and the worst outcome is rows that are out of date,
///   not links that broke.
///   </description></item>
///   <item><description>
///   <see cref="PruneAsync" /> is the only thing that deletes, it takes the ids to keep rather than
///   the ids to remove, and it refuses outright when what it was asked to remove is more than
///   <see cref="PruneThreshold" /> of the provider's rows.
///   </description></item>
///   <item><description>
///   A row an entry links to is never deleted, whatever was passed. It is marked <c>stale</c>. See
///   <see cref="IKnowledgeBaseLinks" />.
///   </description></item>
/// </list>
/// <para>
/// <b>One transaction per run.</b> <see cref="RunAsync" /> does the diff, the upsert and the prune on
/// one connection inside one transaction, so a run that fails halfway leaves the corpus exactly as it
/// was — there is no state in which the inserts landed and the prune did not. The three operations
/// are also exposed on their own, each atomic, because a test is clearer when it does one of them.
/// </para>
/// </remarks>
/// <param name="connectionString">Where to write. The CLI's <c>--connection</c>.</param>
/// <param name="links">
/// Which rows a user's entry links to. The ingest CLI — the only caller that prunes — passes
/// <see cref="EntryKnowledgeBaseLinks" /> (27b). It defaults to <see cref="NoKnowledgeBaseLinks" />
/// for a caller with no entry table to read, such as a test that only upserts.
/// </param>
/// <param name="databaseSchema">The Postgres schema the table lives in. Marten's default is public.</param>
/// <param name="clock">
/// What <c>ingested_at</c> is stamped from. A parameter so a test can pin it; it is not in the
/// content hash, so it can never make a row look changed.
/// </param>
public sealed class KnowledgeBaseStore(
    string connectionString,
    IKnowledgeBaseLinks? links = null,
    string databaseSchema = KnowledgeBaseSchema.DefaultDatabaseSchema,
    TimeProvider? clock = null)
{
    /// <summary>
    /// The share of a provider's rows a prune may remove without <c>--force</c>. Twenty percent: a
    /// real re-ingest of a corpus that has not shrunk removes nothing, so anything approaching this
    /// is either a source that dropped a book or, far more likely, the wrong folder — and that is
    /// exactly the moment to need a second keystroke.
    /// </summary>
    public const double PruneThreshold = 0.20;

    private readonly IKnowledgeBaseLinks entryLinks = links ?? NoKnowledgeBaseLinks.Instance;
    private readonly TimeProvider ingestClock = clock ?? TimeProvider.System;
    private readonly string table = KnowledgeBaseSchema.Qualified(databaseSchema);

    /// <summary>The Postgres schema this store writes to.</summary>
    public string DatabaseSchema { get; } = databaseSchema;

    /// <summary>
    /// Fails unless the table exists.
    /// </summary>
    /// <exception cref="KnowledgeBaseSchemaMissingException">
    /// The API has never started against this database, so there is nowhere to write.
    /// </exception>
    public async Task AssertSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await AssertSchemaAsync(connection, null, cancellationToken);
    }

    /// <summary>
    /// What a run would change, reading nothing but <c>id</c>, <c>content_hash</c> and <c>stale</c>.
    /// Writes nothing, so this is exactly what <c>--dry-run</c> reports.
    /// </summary>
    public async Task<IngestReport> DiffAsync(
        string provider,
        IReadOnlyList<KnowledgeBaseRow> rows,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await AssertSchemaAsync(connection, null, cancellationToken);
        return (await CompareAsync(connection, null, provider, rows, cancellationToken)).Report;
    }

    /// <summary>
    /// Inserts what is new and updates what changed, in one transaction. Rows already stored with
    /// the same hash are not written at all, which is what makes a second run of an unchanged source
    /// touch nothing.
    /// </summary>
    /// <returns>The same report <see cref="DiffAsync" /> would have given.</returns>
    public async Task<IngestReport> UpsertAsync(
        string provider,
        IReadOnlyList<KnowledgeBaseRow> rows,
        Guid batch,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await AssertSchemaAsync(connection, null, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var (report, toWrite) = await CompareAsync(connection, transaction, provider, rows, cancellationToken);
        await WriteAsync(connection, transaction, provider, rows, toWrite, batch, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return report;
    }

    /// <summary>
    /// Deletes the provider's rows that are not in <paramref name="keep" /> — except the ones an
    /// entry links to, which are marked <c>stale</c> instead, and except when there are too many of
    /// them.
    /// </summary>
    /// <param name="provider">The corpus to prune.</param>
    /// <param name="keep">
    /// Every id the source has. Everything else is a candidate. Taking the ids to keep rather than
    /// the ids to remove is deliberate: a caller that computed the wrong set removes too little, not
    /// too much.
    /// </param>
    /// <param name="force">
    /// Go ahead even past <see cref="PruneThreshold" />. It does not override the link protection,
    /// which has no override at all.
    /// </param>
    /// <param name="cancellationToken">The run's token.</param>
    public async Task<PruneReport> PruneAsync(
        string provider,
        IReadOnlyCollection<string> keep,
        bool force,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await AssertSchemaAsync(connection, null, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var report = await PruneAsync(connection, transaction, provider, keep, force, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return report;
    }

    /// <summary>
    /// A whole ingest: the diff, then — unless it is a dry run — the upsert and, if asked for, the
    /// prune, all on one connection inside one transaction.
    /// </summary>
    /// <remarks>
    /// A dry run opens the transaction and never commits, so it reads the corpus the same way a real
    /// run does and provably writes nothing.
    /// </remarks>
    public async Task<IngestOutcome> RunAsync(IngestRun run, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await AssertSchemaAsync(connection, null, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var (report, toWrite) = await CompareAsync(connection, transaction, run.Provider, run.Rows, cancellationToken);

        if (run.DryRun)
        {
            // Rolled back rather than committed. Nothing above wrote, and this is what says so.
            await transaction.RollbackAsync(cancellationToken);
            return new IngestOutcome(report, null);
        }

        await WriteAsync(connection, transaction, run.Provider, run.Rows, toWrite, run.Batch, cancellationToken);

        PruneReport? prune = null;
        if (run.Prune)
        {
            prune = await PruneAsync(
                connection,
                transaction,
                run.Provider,
                [.. run.Rows.Select(row => row.Id)],
                run.Force,
                cancellationToken);
        }

        // A refused prune rolls the whole run back. The alternative — commit the inserts, report the
        // refusal — would leave the operator with a corpus that is half of what they asked for and an
        // exit code that says nothing happened.
        if (prune is { Refused: true })
        {
            await transaction.RollbackAsync(cancellationToken);
            return new IngestOutcome(report, prune);
        }

        await transaction.CommitAsync(cancellationToken);
        return new IngestOutcome(report, prune);
    }

    /// <summary>
    /// Every stored row for a provider, for the tests and for 26e's reader to grow from. Ordered by
    /// id so a comparison is not at the mercy of the plan.
    /// </summary>
    public async Task<IReadOnlyList<StoredKnowledgeBaseRow>> ReadAllAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "select id, name, category, source_book, source_title, page, label, url, image_url, "
            + $"stats::text, content_hash, batch, stale, ingested_at from {table} "
            + "where provider = @provider order by id";
        command.Parameters.AddWithValue("provider", provider);

        var rows = new List<StoredKnowledgeBaseRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new StoredKnowledgeBaseRow
            {
                Provider = provider,
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Category = reader.GetString(2),
                SourceBook = reader.GetString(3),
                SourceTitle = reader.IsDBNull(4) ? null : reader.GetString(4),
                Page = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                Label = reader.IsDBNull(6) ? null : reader.GetString(6),
                Url = reader.GetString(7),
                ImageUrl = reader.IsDBNull(8) ? null : reader.GetString(8),
                Stats = reader.IsDBNull(9) ? null : reader.GetString(9),
                ContentHash = reader.GetString(10),
                Batch = reader.GetGuid(11),
                Stale = reader.GetBoolean(12),
                IngestedAt = reader.GetFieldValue<DateTimeOffset>(13),
            });
        }

        return rows;
    }

    // --- the work, on a caller's connection ------------------------------------------------------

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return connection;
    }

    private async Task AssertSchemaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = KnowledgeBaseSchema.TableExistsSql(DatabaseSchema);

        if (await command.ExecuteScalarAsync(cancellationToken) is true) return;

        throw new KnowledgeBaseSchemaMissingException(
            $"{KnowledgeBaseSchema.Qualified(DatabaseSchema)} does not exist in this database.");
    }

    /// <summary>
    /// The diff, plus the ids that need writing — the same walk answers both, and deriving the
    /// second from the first anywhere else would be a second copy of the "unchanged means unchanged
    /// and not stale" rule.
    /// </summary>
    private async Task<(IngestReport Report, HashSet<string> ToWrite)> CompareAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string provider,
        IReadOnlyList<KnowledgeBaseRow> rows,
        CancellationToken cancellationToken)
    {
        var stored = new Dictionary<string, (string Hash, bool Stale)>(StringComparer.Ordinal);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"select id, content_hash, stale from {table} where provider = @provider";
            command.Parameters.AddWithValue("provider", provider);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                stored[reader.GetString(0)] = (reader.GetString(1), reader.GetBoolean(2));
            }
        }

        var added = 0;
        var updated = 0;
        var unchanged = 0;
        var present = new HashSet<string>(StringComparer.Ordinal);
        var toWrite = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            present.Add(row.Id);

            if (!stored.TryGetValue(row.Id, out var existing))
            {
                added++;
                toWrite.Add(row.Id);
            }
            // A row whose hash matches but which a previous prune flagged still needs writing: the
            // source has it back, so `stale` has to come off. Counting it as unchanged would leave
            // the flag on for good and make an entry's link claim the source is gone when it is not.
            else if (existing.Hash != row.ContentHash || existing.Stale)
            {
                updated++;
                toWrite.Add(row.Id);
            }
            else
            {
                unchanged++;
            }
        }

        var missing = stored.Keys.Where(id => !present.Contains(id)).Order(StringComparer.Ordinal).ToList();

        return (new IngestReport(provider, rows.Count, added, updated, unchanged, missing, stored.Count), toWrite);
    }

    /// <summary>
    /// Writes the rows the diff counted as new or updated, and nothing else. An unchanged row is not
    /// sent at all, so a second run of an unchanged source issues no statement and leaves every
    /// <c>batch</c> and <c>ingested_at</c> where it was — which is what makes "the second run writes
    /// nothing" a fact about the database rather than about the report.
    /// </summary>
    private async Task WriteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string provider,
        IReadOnlyList<KnowledgeBaseRow> rows,
        HashSet<string> toWrite,
        Guid batch,
        CancellationToken cancellationToken)
    {
        if (toWrite.Count == 0) return;

        var ingestedAt = ingestClock.GetUtcNow();

        // One prepared statement, reused. `stale = false` on the update is not bookkeeping: it is how
        // a row a previous prune flagged comes back when the source has it again.
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
             insert into {table} (
                 provider, id, name, category, source_book, source_title, page, label, url,
                 image_url, stats, content_hash, batch, stale, ingested_at)
             values (
                 @provider, @id, @name, @category, @source_book, @source_title, @page, @label, @url,
                 @image_url, @stats, @content_hash, @batch, false, @ingested_at)
             on conflict (provider, id) do update set
                 name         = excluded.name,
                 category     = excluded.category,
                 source_book  = excluded.source_book,
                 source_title = excluded.source_title,
                 page         = excluded.page,
                 label        = excluded.label,
                 url          = excluded.url,
                 image_url    = excluded.image_url,
                 stats        = excluded.stats,
                 content_hash = excluded.content_hash,
                 batch        = excluded.batch,
                 stale        = false,
                 ingested_at  = excluded.ingested_at
             where {table}.content_hash <> excluded.content_hash or {table}.stale
             """;

        var providerParameter = command.Parameters.Add("provider", NpgsqlDbType.Text);
        var idParameter = command.Parameters.Add("id", NpgsqlDbType.Text);
        var nameParameter = command.Parameters.Add("name", NpgsqlDbType.Text);
        var categoryParameter = command.Parameters.Add("category", NpgsqlDbType.Text);
        var sourceBookParameter = command.Parameters.Add("source_book", NpgsqlDbType.Text);
        var sourceTitleParameter = command.Parameters.Add("source_title", NpgsqlDbType.Text);
        var pageParameter = command.Parameters.Add("page", NpgsqlDbType.Integer);
        var labelParameter = command.Parameters.Add("label", NpgsqlDbType.Text);
        var urlParameter = command.Parameters.Add("url", NpgsqlDbType.Text);
        var imageUrlParameter = command.Parameters.Add("image_url", NpgsqlDbType.Text);
        var statsParameter = command.Parameters.Add("stats", NpgsqlDbType.Jsonb);
        var hashParameter = command.Parameters.Add("content_hash", NpgsqlDbType.Text);
        var batchParameter = command.Parameters.Add("batch", NpgsqlDbType.Uuid);
        var ingestedAtParameter = command.Parameters.Add("ingested_at", NpgsqlDbType.TimestampTz);

        await command.PrepareAsync(cancellationToken);

        batchParameter.Value = batch;
        ingestedAtParameter.Value = ingestedAt;
        providerParameter.Value = provider;

        foreach (var row in rows)
        {
            if (!toWrite.Contains(row.Id)) continue;

            idParameter.Value = row.Id;
            nameParameter.Value = row.Name;
            categoryParameter.Value = row.Category;
            sourceBookParameter.Value = row.SourceBook;
            sourceTitleParameter.Value = (object?)row.SourceTitle ?? DBNull.Value;
            pageParameter.Value = (object?)row.Page ?? DBNull.Value;
            labelParameter.Value = (object?)row.Label ?? DBNull.Value;
            urlParameter.Value = row.Url;
            imageUrlParameter.Value = (object?)row.ImageUrl ?? DBNull.Value;
            statsParameter.Value = (object?)row.Stats ?? DBNull.Value;
            hashParameter.Value = row.ContentHash;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<PruneReport> PruneAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string provider,
        IReadOnlyCollection<string> keep,
        bool force,
        CancellationToken cancellationToken)
    {
        var keepSet = new HashSet<string>(keep, StringComparer.Ordinal);
        var candidates = new List<string>();
        var storedBefore = 0;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"select id from {table} where provider = @provider";
            command.Parameters.AddWithValue("provider", provider);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                storedBefore++;
                var id = reader.GetString(0);
                if (!keepSet.Contains(id)) candidates.Add(id);
            }
        }

        if (candidates.Count == 0) return PruneReport.Nothing(provider, storedBefore);

        // The threshold, before anything is read about links and before anything is written. Strictly
        // greater than: removing exactly a fifth of a small corpus is allowed, and the check is about
        // the shape of a mistake, not about arithmetic on the boundary.
        if (!force && candidates.Count > storedBefore * PruneThreshold)
        {
            return new PruneReport(provider, storedBefore, candidates.Count, 0, 0, Refused: true);
        }

        var linked = await entryLinks.LinkedIdsAsync(provider, candidates, connection, transaction, cancellationToken);

        // Intersected with the candidates rather than trusted: an implementation that answered with
        // an id outside the set it was asked about must not be able to stale a row this prune was
        // never looking at.
        var protect = candidates.Where(linked.Contains).ToList();
        var remove = candidates.Where(id => !linked.Contains(id)).ToList();

        var marked = 0;
        if (protect.Count > 0)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                $"update {table} set stale = true where provider = @provider and id = any(@ids) and not stale";
            command.Parameters.AddWithValue("provider", provider);
            command.Parameters.AddWithValue("ids", protect.ToArray());
            marked = await command.ExecuteNonQueryAsync(cancellationToken);
        }

        var deleted = 0;
        if (remove.Count > 0)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"delete from {table} where provider = @provider and id = any(@ids)";
            command.Parameters.AddWithValue("provider", provider);
            command.Parameters.AddWithValue("ids", remove.ToArray());
            deleted = await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new PruneReport(provider, storedBefore, candidates.Count, deleted, marked, Refused: false);
    }
}

/// <summary>What <see cref="KnowledgeBaseStore.RunAsync" /> is asked to do.</summary>
public sealed record IngestRun
{
    /// <summary>The corpus: <c>5etools</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>Every row the source folder yielded.</summary>
    public required IReadOnlyList<KnowledgeBaseRow> Rows { get; init; }

    /// <summary>
    /// Which run last wrote a row. A new one per run, so a row's provenance is a single lookup rather
    /// than a timestamp comparison.
    /// </summary>
    public Guid Batch { get; init; } = Guid.NewGuid();

    /// <summary>Report and write nothing.</summary>
    public bool DryRun { get; init; }

    /// <summary>Delete the rows this source does not have. Off by default; see the class remarks.</summary>
    public bool Prune { get; init; }

    /// <summary>Prune past <see cref="KnowledgeBaseStore.PruneThreshold" />.</summary>
    public bool Force { get; init; }
}

/// <summary>A row as it is stored, including the columns the ingest stamps.</summary>
public sealed record StoredKnowledgeBaseRow
{
    public required string Provider { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string SourceBook { get; init; }
    public string? SourceTitle { get; init; }
    public int? Page { get; init; }
    public string? Label { get; init; }
    public required string Url { get; init; }
    public string? ImageUrl { get; init; }
    public string? Stats { get; init; }
    public required string ContentHash { get; init; }
    public required Guid Batch { get; init; }
    public required bool Stale { get; init; }
    public required DateTimeOffset IngestedAt { get; init; }
}

/// <summary>
/// The table is not there, so there is nowhere to write. The CLI turns this into a message telling
/// the operator to start the API once, and writes nothing.
/// </summary>
public sealed class KnowledgeBaseSchemaMissingException(string message) : Exception(message);
