using Npgsql;

using NpgsqlTypes;

using TakeInitiative.KnowledgeBase.Schema;

namespace TakeInitiative.KnowledgeBase.Store;

/// <summary>
/// The real <see cref="IKnowledgeBaseLinks" /> (step 27b): one statement over the API's <c>Entry</c>
/// document table, asking which of the ids a prune is about to remove an entry links to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this lives in the package and not in the API.</b> The only caller of
/// <see cref="KnowledgeBaseStore.PruneAsync" /> is the ingest CLI, which references this package and
/// nothing else — it has no host, no DI container and no way to reach the API's assembly. An
/// implementation in the API would therefore never run on the one code path the protection exists
/// for: <c>ingest --prune --force</c> from an operator's machine. 26c's own remarks say "one class in
/// the API"; that is the part of the plan that is wrong, and this is the correction.
/// </para>
/// <para>
/// <b>What it knows about the API, and what keeps that honest.</b> Two names: the Marten document
/// table <see cref="EntryTable" /> and the field <see cref="ItemKeysField" /> the API's <c>Entry</c>
/// projection maintains. The key format is not duplicated — <see cref="Key" /> is the one writer of
/// it, and the API's projection calls it — and the two names are pinned by a drift test in the API's
/// suite (<c>EntryLinkPruneTests</c>), so renaming the property there fails a test rather than
/// silently unprotecting every link.
/// </para>
/// <para>
/// <b>Why a derived key array rather than the links themselves.</b> <c>Entry.Links</c> is a JSON
/// array of objects, and jsonb's <c>?|</c> — the index-served "has any of these" the API already
/// uses for <c>ArticleMentionIds</c> and <c>Aliases</c> — only addresses top-level <i>strings</i>.
/// So the projection also keeps a flat array of <c>provider:id</c> keys, which this reads and which
/// carries the GIN index. The alternative, a <c>@&gt;</c> containment term per candidate id, is one
/// statement per row the prune is removing.
/// </para>
/// <para>
/// <b>It runs on the prune's connection and transaction</b>, as the interface requires, so it sees
/// the run it is part of rather than a snapshot from before it.
/// </para>
/// </remarks>
/// <param name="databaseSchema">
/// The Postgres schema the API's documents live in — Marten's <c>public</c> unless the deployment
/// says otherwise. The same value the store is built with.
/// </param>
public sealed class EntryKnowledgeBaseLinks(string databaseSchema = KnowledgeBaseSchema.DefaultDatabaseSchema)
    : IKnowledgeBaseLinks
{
    /// <summary>Marten's table for the API's <c>Entry</c> document.</summary>
    public const string EntryTable = "mt_doc_entry";

    /// <summary>
    /// The <c>Entry</c> property holding one <see cref="Key" /> per knowledge-base link, which the
    /// API GIN-indexes. The index is on <c>((data -&gt;&gt; '<see cref="ItemKeysField" />')::jsonb)</c>,
    /// so <see cref="LinkedIdsAsync" /> has to spell the expression that same way for Postgres to
    /// recognise it.
    /// </summary>
    public const string ItemKeysField = "LinkedItemKeys";

    /// <summary>
    /// How a provider and an item id become one indexable string. A provider key is a short
    /// identifier with no colon in it (<c>5etools</c>, <c>srd52</c>), so the first colon splits the
    /// key — and <see cref="LinkedIdsAsync" /> does not split at all: it builds the keys it is asking
    /// about and maps each answer back to the id it came from.
    /// </summary>
    public static string Key(string provider, string itemId) => $"{provider}:{itemId}";

    private readonly string table = $"{databaseSchema}.{EntryTable}";

    public async Task<IReadOnlySet<string>> LinkedIdsAsync(
        string provider,
        IReadOnlyCollection<string> candidateIds,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // The key each candidate would be stored as, and the way back. Distinct ids only: the store
        // reads a key column, but nothing here depends on that.
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var id in candidateIds)
        {
            byKey[Key(provider, id)] = id;
        }

        var linked = new HashSet<string>(StringComparer.Ordinal);
        if (byKey.Count == 0)
        {
            return linked;
        }

        // One index scan for the whole candidate set: `?|` narrows to the entries that link to any of
        // them (the GIN index decides it), and the lateral then reads only those entries' keys. The
        // `= any` is the recheck that drops an entry's other links, which the index knew nothing
        // about. `distinct` because many entries may link to one row.
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
             select distinct k.key
             from {table} d
             cross join lateral jsonb_array_elements_text((d.data ->> '{ItemKeysField}')::jsonb) as k(key)
             where (d.data ->> '{ItemKeysField}')::jsonb ?| @keys
               and k.key = any(@keys)
             """;
        command.Parameters.Add(new NpgsqlParameter("keys", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = byKey.Keys.ToArray(),
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (byKey.TryGetValue(reader.GetString(0), out var id))
            {
                linked.Add(id);
            }
        }

        return linked;
    }
}
