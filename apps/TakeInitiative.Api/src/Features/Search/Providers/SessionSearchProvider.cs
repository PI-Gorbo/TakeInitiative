using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// Notes, Images and Sessions (17a.10). Notes and Images are the same query split by
/// <c>HasImages</c>, so an image note appears under Images and never twice. Sessions are matched
/// by number or title.
/// </summary>
public class SessionSearchProvider(ILogger<SessionSearchProvider> logger) : ISearchProvider
{
    public IReadOnlyList<SearchSectionKey> Sections =>
        [SearchSectionKey.Notes, SearchSectionKey.Images, SearchSectionKey.Sessions];

    public async Task<IReadOnlyList<SearchSection>> SearchAsync(SearchQuery query, SearchContext context, CancellationToken ct)
    {
        var sections = new List<SearchSection>();

        // A single character searches session numbers only (the length rule, 17a.4), and a query
        // with no letters or digits has no tsquery at all, so there is nothing to match text with.
        var searchesText = query.TsQuery is not null && !query.SingleCharacter;

        if (searchesText && context.Wanted.Contains(SearchSectionKey.Notes))
        {
            sections.Add(await NotesAsync(query, context, SearchSectionKey.Notes, ct));
        }
        if (searchesText && context.Wanted.Contains(SearchSectionKey.Images))
        {
            sections.Add(await NotesAsync(query, context, SearchSectionKey.Images, ct));
        }
        if (context.Wanted.Contains(SearchSectionKey.Sessions))
        {
            sections.Add(await SessionsAsync(query, context, searchesText, ct));
        }
        return sections;
    }

    private async Task<SearchSection> NotesAsync(SearchQuery query, SearchContext context, SearchSectionKey key, CancellationToken ct)
    {
        var images = key == SearchSectionKey.Images;
        var rows = await context.QueryAsync(
            NotesSql(context),
            command =>
            {
                command.Parameters.AddWithValue("campaign", context.CampaignId);
                command.Parameters.AddWithValue("me", SearchVisibilitySql.Me(context.Viewer));
                command.Parameters.AddWithValue("q", query.TsQuery!);
                command.Parameters.AddWithValue("images", images);
                command.Parameters.AddWithValue("limit", context.Take + 1);
                command.Parameters.AddWithValue("headline", Snippet.HeadlineOptions);
            },
            reader => new SearchDoc(
                SearchDocKind.SessionNote, reader.GetGuid(0), null, reader.GetDouble(1), 0,
                reader.IsDBNull(2) ? null : reader.GetString(2)),
            ct);

        var notes = (await context.Session.LoadManyAsync<SessionNote>(ct, rows.Select(r => r.SourceId).ToArray()))
            .ToDictionary(n => n.Id);
        var sessionNumbers = await SessionNumbers(context.Session, notes.Values, ct);

        var hits = new List<SearchHit>();
        foreach (var row in rows)
        {
            if (!notes.TryGetValue(row.SourceId, out var note))
            {
                // The note was deleted between the match and the load. That is a race and not drift:
                // there is nothing to show and nothing wrong, so the row goes quietly.
                continue;
            }
            // Belt and braces (17a.6): the SQL fragment already filtered this row, so a failure here
            // means the fragment and SessionNoteVisibility have drifted. That is a code bug, and it
            // fails the request rather than quietly shortening the answer (see SearchDrift).
            SearchDrift.Guard(
                SessionNoteVisibility.CanSee(note, context.Viewer), logger, "note", note.Id,
                context.Viewer.MemberId, nameof(SessionNoteVisibility));

            // A headline with nothing to highlight is a different thing entirely, and not drift: the
            // note matched, so the hit is shown with whatever words there are (Snippet.OrEmpty).
            hits.Add(new SearchHit
            {
                Kind = SearchHitKind.Note,
                Note = new SearchNoteHit
                {
                    Id = note.Id,
                    SessionId = note.SessionId,
                    SessionNumber = sessionNumbers.GetValueOrDefault(note.SessionId),
                    AuthorMemberId = note.AuthorMemberId,
                    PostedAt = note.PostedAt,
                    Visibility = note.Visibility,
                    IsRecap = note.IsRecap,
                    Images = images ? [.. note.Images.Take(4).Select(NoteImageResponse.From)] : [],
                    Snippet = Snippet.OrEmpty(row.Headline),
                },
            });
        }

        return new SearchSection
        {
            Key = key,
            HasMore = hits.Count > context.Take,
            Hits = [.. hits.Take(context.Take)],
        };
    }

    /// <summary>
    /// The note query. The campaign filter, the visibility fragment and the match are in one
    /// <c>WHERE</c>, so rank and text are computed only for rows that passed it, and the snippet
    /// only for the rows that survived <c>LIMIT</c>.
    /// </summary>
    private static string NotesSql(SearchContext context) => $"""
        SELECT t.id, t.rank, ts_headline('{SearchSql.Config}', t.text, {SearchSql.TsQuery}, @headline) AS headline
        FROM (
            SELECT d.id AS id,
                   ts_rank_cd({SearchSql.NoteVector}, {SearchSql.TsQuery}) AS rank,
                   {SearchSql.PlainText("d.data ->> 'Text'")} AS text,
                   (d.data ->> 'PostedAt')::timestamptz AS posted_at
            FROM {SearchSql.Table(context.Session, SearchSql.NoteTable)} d
            WHERE {SearchSql.CampaignFilter}
              AND {SearchVisibilitySql.Notes(context.Viewer)}
              AND coalesce((d.data ->> 'HasImages')::boolean, false) = @images
              AND {SearchSql.NoteVector} @@ {SearchSql.TsQuery}
            ORDER BY rank DESC, posted_at DESC
            LIMIT @limit
        ) t
        ORDER BY t.rank DESC, t.posted_at DESC
        """;

    private async Task<SearchSection> SessionsAsync(SearchQuery query, SearchContext context, bool matchTitle, CancellationToken ct)
    {
        var rows = await context.QueryAsync(
            SessionsSql(context),
            command =>
            {
                command.Parameters.AddWithValue("campaign", context.CampaignId);
                // Declared rather than inferred: a null with no type leaves Postgres unable to
                // work out what `@number IS NOT NULL` compares (42P08).
                command.Parameters.Add(new NpgsqlParameter("number", NpgsqlTypes.NpgsqlDbType.Integer)
                {
                    Value = (object?)query.SessionNumber ?? DBNull.Value,
                });
                command.Parameters.AddWithValue("matchTitle", matchTitle);
                command.Parameters.AddWithValue("text", query.Text);
                command.Parameters.AddWithValue("q", query.TsQuery ?? string.Empty);
                command.Parameters.AddWithValue("minSimilarity", EntryMatchOptions.Default.MinSimilarity);
                command.Parameters.AddWithValue("limit", context.Take + 1);
                command.Parameters.AddWithValue("headline", Snippet.HeadlineOptions);
            },
            reader => new SearchDoc(
                SearchDocKind.Session, reader.GetGuid(0), null, 0, reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)),
            ct);

        var sessions = (await context.Session.LoadManyAsync<Session>(ct, rows.Select(r => r.SourceId).ToArray()))
            .ToDictionary(s => s.Id);
        var current = await context.Session.CurrentSession(context.CampaignId, ct);

        var hits = rows
            .Where(row => sessions.ContainsKey(row.SourceId))
            .Select(row => new SearchHit
            {
                Kind = SearchHitKind.Session,
                Session = new SearchSessionHit
                {
                    Session = SessionResponse.From(sessions[row.SourceId], current?.Id),
                    // Null for a number match, which has no headline. A title matched down the
                    // trigram ladder has one with no highlights in it: the words are still what
                    // matched, so they are shown (Snippet.OrEmpty).
                    Snippet = row.Headline is null ? null : Snippet.OrEmpty(row.Headline),
                },
            })
            .ToList();

        return new SearchSection
        {
            Key = SearchSectionKey.Sessions,
            HasMore = hits.Count > context.Take,
            Hits = [.. hits.Take(context.Take)],
        };
    }

    /// <summary>
    /// Sessions (17a.8): a number match first, then the title down the same ladder names use, then
    /// — within a rung of that ladder — <c>word_similarity</c>, and only then <c>Number</c>
    /// descending. Without the similarity step two fuzzy title matches would come back in session
    /// order, which says nothing about which title the viewer typed.
    /// <para>
    /// Every member sees every session, so there is no visibility fragment here — that is the whole
    /// rule, not an omission. A number-only match has no snippet, so the title is not echoed back
    /// as one.
    /// </para>
    /// </summary>
    private static string SessionsSql(SearchContext context) => $"""
        SELECT x.id, x.category,
               CASE WHEN x.category = 0 OR x.title IS NULL THEN NULL
                    ELSE ts_headline('{SearchSql.Config}', x.title, {SearchSql.TsQuery}, @headline) END AS headline
        FROM (
            SELECT d.id AS id, (d.data ->> 'Number')::int AS number, d.data ->> 'Title' AS title,
                   c.category AS category, w.similarity AS similarity
            FROM {SearchSql.Table(context.Session, SearchSql.SessionTable)} d
            -- The title's similarity once, for the fuzzy rung of the ladder and for the tie-break
            -- inside a rung both (OFFSET 0 keeps it from being inlined into each).
            CROSS JOIN LATERAL (
                SELECT CASE WHEN @matchTitle AND d.data ->> 'Title' IS NOT NULL
                            THEN word_similarity({SearchSql.Folded("@text")}, {SearchSql.Folded("d.data ->> 'Title'")})
                            ELSE 0 END AS similarity
                OFFSET 0
            ) w
            CROSS JOIN LATERAL (
                SELECT CASE
                    WHEN @number IS NOT NULL AND (d.data ->> 'Number')::int = @number THEN 0
                    WHEN @matchTitle AND d.data ->> 'Title' IS NOT NULL
                        THEN 1 + {SearchSql.MatchCategory(
                            SearchSql.Folded("d.data ->> 'Title'"), SearchSql.Folded("@text"), "w.similarity")}
                    ELSE NULL
                END
            ) AS c(category)
            WHERE {SearchSql.CampaignFilter} AND c.category IS NOT NULL
            ORDER BY c.category, w.similarity DESC, number DESC
            LIMIT @limit
        ) x
        ORDER BY x.category, x.similarity DESC, x.number DESC
        """;

    private static async Task<Dictionary<Guid, int>> SessionNumbers(
        IQuerySession session, IEnumerable<SessionNote> notes, CancellationToken ct)
    {
        var ids = notes.Select(n => n.SessionId).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }
        var sessions = await session.LoadManyAsync<Session>(ct, ids);
        return sessions.ToDictionary(s => s.Id, s => s.Number);
    }
}
