using Marten;
using Npgsql;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// The one Postgres connection a search reuses. An all-sections search runs four or five
/// statements, and a connection per statement is a pool round trip each for nothing: the
/// statements run one after another on one session (a Marten session is not thread-safe), so one
/// connection is enough.
/// <para>
/// It is opened lazily, so a search that asks Postgres nothing (a query of punctuation alone)
/// opens nothing, and <see cref="SearchService"/> disposes it when the search is over. A search
/// only reads rows that are already committed, so this connection is separate from the session's
/// own and takes part in no transaction.
/// </para>
/// </summary>
public sealed class SearchConnection(IQuerySession session) : IAsyncDisposable
{
    private NpgsqlConnection? _connection;

    /// <summary>The open connection, opening it on first use.</summary>
    public async ValueTask<NpgsqlConnection> Opened(CancellationToken ct)
    {
        if (_connection is null)
        {
            var connection = (NpgsqlConnection)session.Database.CreateConnection();
            try
            {
                await connection.OpenAsync(ct);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
            _connection = connection;
        }
        return _connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
