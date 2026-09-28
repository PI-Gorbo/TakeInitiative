using Marten;
using Npgsql;
using NpgsqlTypes;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// Trigram matching of text against entry names and aliases, under the viewer's visibility
/// (glossary: Entry matcher, §11a). ⌘K's Entries section, loose ends (19: "this note says
/// Gundren, link it?") and suggestions (23: "match before creating") share it, so there is one
/// answer to "which entry is this text talking about".
/// <para>
/// Every span is matched in one round trip (<c>unnest(@spans) WITH ORDINALITY</c>), because the
/// callers with many spans have a whole note's worth of them. Names and aliases are scanned per
/// campaign through Marten's <c>(CampaignId, Kind)</c> index: at most a few thousand short
/// strings, so there is no index on them (17a.3). One scan yields both, the name and the aliases
/// together, because two scans of <c>mt_doc_entry</c> cost twice what one does for the same rows.
/// </para>
/// <para>
/// It holds nothing between calls — the session, the campaign and the viewer are all arguments — so
/// it is registered as a singleton.
/// </para>
/// </summary>
public class EntryMatcher
{
    /// <summary>
    /// For each span, in the order they were given, the entries it matched, best first. An entry
    /// that matches both its name and an alias appears once, on its better match.
    /// </summary>
    /// <param name="connection">
    /// The search's connection, when this call is part of one (17a's Entries section). A caller with
    /// none — loose ends (19), suggestions (23) — gets a connection for this statement alone.
    /// </param>
    public virtual async Task<IReadOnlyList<IReadOnlyList<EntryMatch>>> MatchAsync(
        IQuerySession session, Guid campaignId, Member viewer,
        IReadOnlyList<string> spans, EntryMatchOptions options, CancellationToken ct,
        SearchConnection? connection = null)
    {
        // An empty span would match every name at the prefix step (strpos(x, '') is 1), so it is
        // never sent. Its results stay an empty list, and the ordinals still line up.
        var wanted = spans.Select((span, index) => (Span: span, Index: index))
            .Where(s => !string.IsNullOrWhiteSpace(s.Span))
            .ToList();
        var results = spans.Select(_ => (IReadOnlyList<EntryMatch>)Array.Empty<EntryMatch>()).ToArray();
        if (wanted.Count == 0)
        {
            return results;
        }

        var rows = await SearchSql.QueryAsync(
            connection,
            session,
            Sql(session, viewer),
            command =>
            {
                command.Parameters.AddWithValue("campaign", campaignId);
                command.Parameters.AddWithValue("me", SearchVisibilitySql.Me(viewer));
                command.Parameters.AddWithValue("isDm", viewer.Role == Role.DM);
                command.Parameters.Add(new NpgsqlParameter("spans", NpgsqlDbType.Array | NpgsqlDbType.Text)
                {
                    Value = wanted.Select(s => s.Span).ToArray(),
                });
                command.Parameters.AddWithValue("minSimilarity", options.MinSimilarity);
                command.Parameters.AddWithValue("fuzzyOnly", options.FuzzyOnly);
                command.Parameters.AddWithValue("take", options.Take);
            },
            reader => (
                Ordinal: reader.GetInt64(0),
                Match: new EntryMatch(
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetBoolean(3),
                    reader.GetInt32(4),
                    reader.GetDouble(5))),
            ct);

        foreach (var group in rows.GroupBy(r => r.Ordinal))
        {
            // WITH ORDINALITY counts from 1, over the spans actually sent.
            results[wanted[(int)group.Key - 1].Index] = group.Select(r => r.Match).ToList();
        }
        return results;
    }

    private static string Sql(IQuerySession session, Member viewer) => $"""
        WITH spans AS (
            SELECT s.ord AS ord, {SearchSql.Folded("s.span")} AS span
            FROM unnest(@spans) WITH ORDINALITY AS s(span, ord)
        ),
        candidates AS MATERIALIZED (
            -- One scan of the entries, and the name and the aliases come out of it together: the
            -- name is ordinal 1 of "the name, then the aliases", so everything after it is an alias.
            -- MATERIALIZED so the fold happens once per candidate: the match ladder reads it half a
            -- dozen times, and an inlined CTE would fold the same name over again for each of them.
            SELECT d.id AS entry_id, d.data ->> 'Name' AS entry_name, a.candidate AS candidate,
                   a.ord > 1 AS is_alias, {SearchSql.Folded("a.candidate")} AS folded
            FROM {SearchSql.Table(session, SearchSql.EntryTable)} d
            CROSS JOIN LATERAL jsonb_array_elements_text(
                jsonb_build_array(d.data -> 'Name') || coalesce(d.data -> 'Aliases', '[]'::jsonb)
            ) WITH ORDINALITY AS a(candidate, ord)
            WHERE {SearchSql.CampaignFilter} AND {SearchVisibilitySql.ListedEntries(viewer)}
        ),
        matches AS (
            -- One word_similarity per candidate, for the fuzzy rung and for the tie-break both.
            -- OFFSET 0 keeps the planner from inlining it back into the two places that read it.
            SELECT s.ord, c.entry_id, c.entry_name, c.candidate, c.is_alias, w.similarity AS similarity,
                   {SearchSql.MatchCategory("c.folded", "s.span", "w.similarity")} AS category
            FROM candidates c
            CROSS JOIN spans s
            CROSS JOIN LATERAL (SELECT word_similarity(s.span, c.folded) AS similarity OFFSET 0) w
        ),
        best AS (
            SELECT m.*,
                   row_number() OVER (PARTITION BY m.ord, m.entry_id ORDER BY m.category, m.similarity DESC, m.is_alias) AS per_entry
            FROM matches m
            WHERE m.category IS NOT NULL AND (NOT @fuzzyOnly OR m.category = 4)
        )
        SELECT r.ord, r.entry_id, r.candidate, r.is_alias, r.category, r.similarity
        FROM (
            SELECT b.*, row_number() OVER (PARTITION BY b.ord ORDER BY b.category, b.similarity DESC, b.entry_name) AS rank
            FROM best b
            WHERE b.per_entry = 1
        ) r
        WHERE r.rank <= @take
        ORDER BY r.ord, r.rank
        """;
}
