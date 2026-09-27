using Marten;
using Npgsql;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// One search, for one viewer. <see cref="Session"/> is the caller's Marten session: a session
/// is not thread-safe, so <see cref="SearchService"/> runs the providers one after another on it
/// rather than in parallel.
/// </summary>
/// <param name="Session">The session every query and document load goes through.</param>
/// <param name="CampaignId">The campaign being searched. Every query filters by it.</param>
/// <param name="Viewer">The caller's member in that campaign, resolved by <c>RequireMember</c>.</param>
/// <param name="Wanted">The sections asked for. A provider fills only the ones it is asked for.</param>
/// <param name="Take">How many hits each section returns, 1 to 20.</param>
public record SearchContext(
    IQuerySession Session,
    Guid CampaignId,
    Member Viewer,
    IReadOnlySet<SearchSectionKey> Wanted,
    int Take)
{
    /// <summary>
    /// The one connection this search's statements share. <see cref="SearchService"/> opens it and
    /// disposes it; a context built without one — a test, or a caller running a provider on its own —
    /// gets a connection per statement instead.
    /// </summary>
    public SearchConnection? Connection { get; init; }

    /// <summary>Runs one statement of this search.</summary>
    public Task<List<T>> QueryAsync<T>(
        string sql, Action<NpgsqlCommand> parameters, Func<NpgsqlDataReader, T> read, CancellationToken ct)
        => SearchSql.QueryAsync(Connection, Session, sql, parameters, read, ct);
}

/// <summary>
/// A source of ⌘K results (glossary: Search provider, §11). Each provider fills one or more
/// sections. Step 18 adds a combat provider and steps 20 and 21 reference providers, as more
/// registrations: nothing else changes.
/// </summary>
public interface ISearchProvider
{
    /// <summary>The sections this provider can fill.</summary>
    IReadOnlyList<SearchSectionKey> Sections { get; }

    /// <summary>
    /// The sections this provider found something in. A section with no hits may be left out; the
    /// service drops empty ones anyway.
    /// </summary>
    Task<IReadOnlyList<SearchSection>> SearchAsync(SearchQuery query, SearchContext context, CancellationToken ct);
}
