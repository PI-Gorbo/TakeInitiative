using Npgsql;

namespace TakeInitiative.KnowledgeBase.Store;

/// <summary>
/// Which knowledge-base rows a user's entry links to. A prune never deletes one of these; it marks
/// the row <c>stale</c> and leaves it, so the link still resolves and the entry can say "this link's
/// source is no longer in your data".
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a seam, and it is empty on purpose.</b> Entry links to reference material are step 27.
/// There is no table, no column and no writer for them yet, so there is nothing a query could read:
/// the only honest implementation today is <see cref="NoKnowledgeBaseLinks" />, which reports that
/// nothing is linked. Writing speculative SQL against a column that does not exist would be a
/// statement that cannot run and a test that cannot mean anything.
/// </para>
/// <para>
/// <b>What 26c does pin</b> is the shape of the question, which is the part step 27 has to satisfy:
/// a provider and the exact set of ids a prune is about to remove, answered with the subset that is
/// linked. <c>KnowledgeBaseStoreTests</c> asserts that the store asks exactly that — same provider,
/// same candidate ids, no more — and that a row the answer names survives the prune as
/// <c>stale = true</c> while its neighbours are deleted. Both of those are real behaviour of the
/// store, tested against real Postgres; none of it pretends an entry link exists.
/// </para>
/// <para>
/// <b>What step 27 implements.</b> One class in the API, over the <c>Entry</c> document table, of
/// the shape <c>select distinct … from mt_doc_entry where <i>the link container</i> ?| @candidates</c>
/// — the GIN-indexed containment <c>ArticleMentionIds</c> and <c>Aliases</c> already use in
/// <c>Bootstrap</c>. It is passed to <see cref="KnowledgeBaseStore" />'s constructor and nothing else
/// changes: the deletion, the staling and the threshold are all already here and already tested.
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
/// Nothing is linked, because nothing can link yet. The default until step 27 replaces it.
/// </summary>
/// <remarks>
/// It matters that this is the default rather than "everything is protected": protecting everything
/// would make <c>--prune --force</c> silently do nothing, which is a worse failure than deleting a
/// row that could not have been linked in the first place.
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
