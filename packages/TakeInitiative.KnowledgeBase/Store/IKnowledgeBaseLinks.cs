using Npgsql;

namespace TakeInitiative.KnowledgeBase.Store;

/// <summary>
/// Which knowledge-base rows a user's entry links to. A prune never deletes one of these; it marks
/// the row <c>stale</c> and leaves it, so the link still resolves and the entry can say "this link's
/// source is no longer in your data".
/// </summary>
/// <remarks>
/// <para>
/// <b>This was a seam, and step 27b filled it.</b> 26c shipped with
/// <see cref="NoKnowledgeBaseLinks" /> alone, because there was no column and no writer for an entry
/// link, so nothing a query could read. <see cref="EntryKnowledgeBaseLinks" /> is the real one, and
/// the ingest CLI — the only thing that prunes — now passes it.
/// </para>
/// <para>
/// <b>What 26c pinned</b> is the shape of the question, which is the part step 27 had to satisfy: a
/// provider and the exact set of ids a prune is about to remove, answered with the subset that is
/// linked. <c>KnowledgeBaseStoreTests</c> asserts that the store asks exactly that — same provider,
/// same candidate ids, no more, on the prune's own open connection and transaction — and that a row
/// the answer names survives the prune as <c>stale = true</c> while its neighbours are deleted. The
/// API's <c>EntryLinkPruneTests</c> is the other half: the same prune, against a real entry with a
/// real link, through <see cref="EntryKnowledgeBaseLinks" />.
/// </para>
/// <para>
/// <b>One correction to 26c's plan.</b> It said the implementation would be "one class in the API".
/// It cannot be: the only caller of <see cref="KnowledgeBaseStore.PruneAsync" /> is the ingest CLI,
/// which references this package and has no way to reach the API's assembly, so an implementation in
/// the API would never run on the one path the protection exists for. It lives beside this interface
/// instead; see <see cref="EntryKnowledgeBaseLinks" /> for what it knows about the API and what keeps
/// that honest.
/// </para>
/// </remarks>
public interface IKnowledgeBaseLinks
{
    /// <summary>
    /// The subset of <paramref name="candidateIds" /> that at least one entry links to.
    /// </summary>
    /// <param name="provider">The corpus the ids belong to.</param>
    /// <param name="candidateIds">
    /// The ids the prune is about to remove. Never empty — the store does not ask about nothing.
    /// </param>
    /// <param name="connection">
    /// The prune's own open connection, so an implementation reading the same database sees the
    /// prune's transaction and cannot answer from a snapshot taken before it.
    /// </param>
    /// <param name="transaction">The prune's transaction.</param>
    /// <param name="cancellationToken">The run's token.</param>
    Task<IReadOnlySet<string>> LinkedIdsAsync(
        string provider,
        IReadOnlyCollection<string> candidateIds,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Nothing is linked. Still the constructor default, for a caller with no entry table to read — the
/// store's own tests, and the API's test fixtures, which seed a corpus and never prune.
/// </summary>
/// <remarks>
/// <para>
/// It matters that this is the default rather than "everything is protected": protecting everything
/// would make <c>--prune --force</c> silently do nothing, which is a worse failure than deleting a
/// row that could not have been linked in the first place.
/// </para>
/// <para>
/// Nothing that prunes uses it any more. The ingest CLI passes
/// <see cref="EntryKnowledgeBaseLinks" />, which is where the protection actually comes from.
/// </para>
/// </remarks>
public sealed class NoKnowledgeBaseLinks : IKnowledgeBaseLinks
{
    /// <summary>The one instance; it holds nothing.</summary>
    public static NoKnowledgeBaseLinks Instance { get; } = new();

    private static readonly IReadOnlySet<string> None = new HashSet<string>(StringComparer.Ordinal);

    public Task<IReadOnlySet<string>> LinkedIdsAsync(
        string provider,
        IReadOnlyCollection<string> candidateIds,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default) => Task.FromResult(None);
}
