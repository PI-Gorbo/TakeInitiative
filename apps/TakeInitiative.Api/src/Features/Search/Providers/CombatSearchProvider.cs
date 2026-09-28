using Microsoft.Extensions.Logging;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// Combats (18f). A combat matches by its own name or by the name of a combatant the viewer can
/// see, down the same ladder names use (exact, prefix, word prefix, substring, fuzzy). Hits come
/// back by that rung, then live before drafts before finished, then newest.
/// <para>
/// Visibility is in the SQL, as for every other section (17a.6): a player never matches a Draft,
/// and a hidden combatant's name matches nothing for them, so it adds no hit, no rank and no
/// <c>hasMore</c>. Each hit is the viewer's own <see cref="CombatCard"/>, built from their
/// redacted view.
/// </para>
/// </summary>
public class CombatSearchProvider(ILogger<CombatSearchProvider> logger) : ISearchProvider
{
    public IReadOnlyList<SearchSectionKey> Sections => [SearchSectionKey.Combats];

    public async Task<IReadOnlyList<SearchSection>> SearchAsync(SearchQuery query, SearchContext context, CancellationToken ct)
    {
        // The length rule (17a.4) keeps a single character to entry names and session numbers, and
        // a query of punctuation alone has no words to match a name with.
        if (!context.Wanted.Contains(SearchSectionKey.Combats) || query.TsQuery is null || query.SingleCharacter)
        {
            return [];
        }

        var rows = await context.QueryAsync(
            Sql(context),
            command =>
            {
                command.Parameters.AddWithValue("campaign", context.CampaignId);
                command.Parameters.AddWithValue("me", SearchVisibilitySql.Me(context.Viewer));
                command.Parameters.AddWithValue("text", query.Text);
                command.Parameters.AddWithValue("minSimilarity", EntryMatchOptions.Default.MinSimilarity);
                command.Parameters.AddWithValue("limit", context.Take + 1);
            },
            reader => (Id: reader.GetGuid(0), Matched: reader.IsDBNull(1) ? null : reader.GetString(1)),
            ct);
        if (rows.Count == 0)
        {
            return [];
        }

        var combats = (await context.Session.LoadManyAsync<Combat>(ct, rows.Select(r => r.Id).ToArray()))
            .ToDictionary(c => c.Id);
        var found = new List<(Combat Combat, string? Matched)>();
        foreach (var row in rows)
        {
            if (!combats.TryGetValue(row.Id, out var combat))
            {
                continue;
            }
            // Belt and braces (17a.6): the SQL already filtered both the combat and the combatant
            // whose name matched, so a failure here is drift, and fails the request (SearchDrift).
            SearchDrift.Guard(
                combat.CampaignId == context.CampaignId && CombatView.CanSee(combat, context.Viewer),
                logger, "combat", combat.Id, context.Viewer.MemberId, nameof(CombatView));
            if (row.Matched is not null)
            {
                SearchDrift.Guard(
                    CombatView.VisibleCombatants(combat, context.Viewer).Any(c => c.Name == row.Matched),
                    logger, "combatant of combat", combat.Id, context.Viewer.MemberId, nameof(CombatView));
            }
            found.Add((combat, row.Matched));
        }

        var cards = (await CombatCard.For(context.Session, [.. found.Select(f => f.Combat)], context.Viewer, ct))
            .ToDictionary(c => c.Id);
        var sessionIds = found.Select(f => f.Combat.SessionId).Distinct().ToArray();
        var numbers = sessionIds.Length == 0
            ? []
            : (await context.Session.LoadManyAsync<Session>(ct, sessionIds)).ToDictionary(s => s.Id, s => s.Number);

        var hits = found
            .Where(f => cards.ContainsKey(f.Combat.Id))
            .Select(f => new SearchHit
            {
                Kind = SearchHitKind.Combat,
                Combat = new SearchCombatHit
                {
                    Combat = cards[f.Combat.Id],
                    SessionNumber = numbers.GetValueOrDefault(f.Combat.SessionId),
                    MatchedCombatant = f.Matched,
                },
            })
            .ToList();

        return
        [
            new SearchSection
            {
                Key = SearchSectionKey.Combats,
                HasMore = hits.Count > context.Take,
                Hits = [.. hits.Take(context.Take)],
            },
        ];
    }

    /// <summary>
    /// For each combat the viewer can see, its best match: the combat's name or a visible
    /// combatant's, the name first on a tie, then the closer, then the first listed. A combat with
    /// no match has no row in the lateral, so the cross join drops it. The combatant fragment sits
    /// inside the lateral, so a hidden combatant's name is never a candidate for a player.
    /// <para>
    /// The matched name comes back only when it is a combatant's: the row says "Goblin 2" under
    /// "Goblin Ambush", and nothing under a combat found by its own name.
    /// </para>
    /// </summary>
    private static string Sql(SearchContext context) => $"""
        SELECT x.id, x.matched
        FROM (
            SELECT d.id AS id,
                   m.category AS category,
                   CASE WHEN m.is_combatant THEN m.candidate END AS matched,
                   CASE d.data ->> 'Status' WHEN 'Active' THEN 0 WHEN 'Draft' THEN 1 ELSE 2 END AS status_rank,
                   coalesce(d.data ->> 'StartedAt', d.data ->> 'CreatedAt')::timestamptz AS at
            FROM {SearchSql.Table(context.Session, SearchSql.CombatTable)} d
            CROSS JOIN LATERAL (
                SELECT n.candidate, n.is_combatant, k.category
                FROM (
                    SELECT d.data ->> 'Name' AS candidate, false AS is_combatant, 0::bigint AS ord
                    UNION ALL
                    SELECT o.c ->> 'Name', true, o.ord
                    FROM jsonb_array_elements(d.data -> 'Combatants') WITH ORDINALITY AS o(c, ord)
                    WHERE {SearchVisibilitySql.Combatants(context.Viewer)}
                ) n
                -- The fold and the similarity once per candidate, for the fuzzy rung and the
                -- tie-break both (OFFSET 0 keeps each from being inlined into the places that read it).
                CROSS JOIN LATERAL (SELECT {SearchSql.Folded("n.candidate")} AS folded OFFSET 0) f
                CROSS JOIN LATERAL (SELECT word_similarity({SearchSql.Folded("@text")}, f.folded) AS similarity OFFSET 0) w
                CROSS JOIN LATERAL (
                    SELECT {SearchSql.MatchCategory("f.folded", SearchSql.Folded("@text"), "w.similarity")} AS category
                ) k
                WHERE n.candidate IS NOT NULL AND k.category IS NOT NULL
                ORDER BY k.category, n.is_combatant, w.similarity DESC, n.ord
                LIMIT 1
            ) m
            WHERE {SearchSql.CampaignFilter} AND {SearchVisibilitySql.Combats(context.Viewer)}
            ORDER BY m.category, status_rank, at DESC, d.id
            LIMIT @limit
        ) x
        ORDER BY x.category, x.status_rank, x.at DESC, x.id
        """;
}
