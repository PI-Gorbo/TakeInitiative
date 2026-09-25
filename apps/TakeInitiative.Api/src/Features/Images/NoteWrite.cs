using Marten;
using Npgsql;

namespace TakeInitiative.Api.Features.Images;

/// <summary>
/// One database transaction for a note write that changes images (16b): <c>POST</c>,
/// <c>PUT</c> and <c>DELETE notes</c>. The image documents are saved in a first batch and the
/// note's events in a second, and both commit together or not at all, sharing the request's
/// correlation id.
/// <para>
/// Why two batches: Marten 7.31.1 reports a false <c>ConcurrencyException</c> for an
/// optimistic-concurrency document (the <see cref="Image"/>) saved in the same batch as a new
/// event stream (a note's <c>StartStream</c>, or a new entry's), even when the version matches
/// (16b's Notes). In a batch of their own the image updates get Marten's real check, and the
/// row locks they take are held until the commit, so a racing attach waits and then fails.
/// </para>
/// </summary>
public sealed class NoteWrite : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;

    /// <summary>The session for every write of the request. Its saves do not commit; <see cref="CommitAsync"/> does.</summary>
    public IDocumentSession Session { get; }

    private NoteWrite(NpgsqlConnection connection, NpgsqlTransaction transaction, IDocumentSession session)
    {
        _connection = connection;
        _transaction = transaction;
        Session = session;
    }

    /// <summary>Opens the transaction, with <paramref name="request"/>'s correlation id and <c>request</c> header on its session.</summary>
    public static async Task<NoteWrite> Begin(IDocumentSession request, CancellationToken ct)
    {
        var store = request.DocumentStore;
        var connection = store.Storage.Database.CreateConnection();
        await connection.OpenAsync(ct);
        var transaction = await connection.BeginTransactionAsync(ct);
        var session = store.LightweightSession(Marten.Services.SessionOptions.ForTransaction(transaction));
        session.CorrelationId = request.CorrelationId;
        session.CausationId = request.CausationId;
        if (request.GetHeader(CorrelationMiddleware.RequestHeaderKey) is { } header)
        {
            session.SetHeader(CorrelationMiddleware.RequestHeaderKey, header);
        }
        return new NoteWrite(connection, transaction, session);
    }

    public Task CommitAsync(CancellationToken ct) => _transaction.CommitAsync(ct);

    /// <summary>Rolls back anything not committed.</summary>
    public async ValueTask DisposeAsync()
    {
        await Session.DisposeAsync();
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
