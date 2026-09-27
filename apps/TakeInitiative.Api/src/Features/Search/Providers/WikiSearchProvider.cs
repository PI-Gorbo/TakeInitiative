using Marten;
using Microsoft.Extensions.Logging;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// The Entries section (17a.10): entry names and aliases through <see cref="EntryMatcher"/>, plus
/// the entries whose article text matches. Wiki results come first (§11).
/// <para>
/// An article hit is found in two steps. The GIN index over <b>every</b> block of an article,
/// secret ones included, narrows the candidates; then each candidate's blocks are matched one by
/// one, on the blocks the viewer can see. A word that appears only in a 🔒 block therefore yields no
/// hit for anyone outside that block, and the snippet comes from the best-ranked block they can see.
/// </para>
/// <para>
/// The prefilter is a superset in every case but one, which
/// <see cref="SearchSql.EntryArticleText"/> writes out: it holds each block's raw lexemes, so a
/// block that only matches through a lexeme <see cref="SearchSql.PlainText"/> joined together is
/// missed. That is a false negative and never a leak, and the visibility of what <i>is</i> returned
/// does not depend on it.
/// </para>
/// </summary>
public class WikiSearchProvider(EntryMatcher matcher, ILogger<WikiSearchProvider> logger) : ISearchProvider
{
    /// <summary>
    /// How many entries SQL returns before C# sorts them. The final order needs the viewer's
    /// mention counts, which are one query over the notes and articles they can see, so it is done
    /// once for the candidates rather than per entry inside the match.
    /// </summary>
    public const int Candidates = 50;

    /// <summary>The match category of an article-only hit: after exact, prefix, word prefix, substring and fuzzy (17a.8).</summary>
    private const int ArticleCategory = 5;

    public IReadOnlyList<SearchSectionKey> Sections => [SearchSectionKey.Entries];

    public async Task<IReadOnlyList<SearchSection>> SearchAsync(SearchQuery query, SearchContext context, CancellationToken ct)
    {
        if (!context.Wanted.Contains(SearchSectionKey.Entries))
        {
            return [];
        }

        var named = (await matcher.MatchAsync(
                context.Session, context.CampaignId, context.Viewer, [query.Text],
                EntryMatchOptions.Default with { Take = Candidates }, ct, context.Connection))
            [0]
            // The length rule (17a.4): one character searches names by prefix only. Everything
            // else would return most of the wiki, and the second character is one keystroke away.
            .Where(m => !query.SingleCharacter || m.Category <= 1)
            .ToList();

        // Article text is not searched for a one-character query, and there is nothing to match
        // when the query holds no letters or digits. Nor is it searched when the names alone have
        // already filled the section: an article hit is the last rung of the ladder (category 5), so
        // once there are more than `take` name matches no article hit can be shown or change
        // `hasMore`, and the whole query — the one that costs the most of any section — is skipped.
        List<SearchDoc> articles = query.SingleCharacter || query.TsQuery is null || named.Count > context.Take
            ? []
            : await ArticlesAsync(query, context, named, ct);

        var candidates = new Dictionary<Guid, Candidate>();
        foreach (var match in named)
        {
            candidates[match.EntryId] = new Candidate(match.Category, match.IsAlias ? match.MatchedName : null, null, 0, null);
        }
        foreach (var article in articles)
        {
            // A name or alias match is the better one, and only an article hit carries a snippet, so
            // an entry found both ways is shown as the name match it is. The article query already
            // leaves those entries out (@named), so this only guards against a caller that did not.
            if (!candidates.ContainsKey(article.SourceId))
            {
                candidates[article.SourceId] = new Candidate(article.Category, null, article.BlockId, article.Rank, article.Headline);
            }
        }
        if (candidates.Count == 0)
        {
            return [];
        }

        var entries = await Loaded(candidates.Keys, context, ct);
        var counts = await MentionIndex.CountsForEntries(context.Session, context.CampaignId, context.Viewer, entries, ct);

        var ordered = entries
            .Select(entry => (Entry: entry, Candidate: candidates[entry.Id], Count: counts.GetValueOrDefault(entry.Id)?.Count ?? 0))
            .OrderBy(x => x.Candidate.Category)
            .ThenByDescending(x => x.Count)
            .ThenByDescending(x => x.Candidate.Rank)
            .ThenBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return
        [
            new SearchSection
            {
                Key = SearchSectionKey.Entries,
                HasMore = ordered.Count > context.Take,
                Hits =
                [
                    .. ordered.Take(context.Take).Select(x => new SearchHit
                    {
                        Kind = SearchHitKind.Entry,
                        Entry = new SearchEntryHit
                        {
                            Entry = EntrySummaryResponse.From(x.Entry),
                            MentionCount = x.Count,
                            MatchedOn = x.Candidate.Category == ArticleCategory ? SearchMatchedOn.Article
                                : x.Candidate.Alias is not null ? SearchMatchedOn.Alias
                                : SearchMatchedOn.Name,
                            Alias = x.Candidate.Alias,
                            BlockId = x.Candidate.BlockId,
                            Snippet = Snippet.From(x.Candidate.Headline),
                        },
                    }),
                ],
            },
        ];
    }

    private record Candidate(int Category, string? Alias, Guid? BlockId, double Rank, string? Headline);

    /// <summary>
    /// The candidate entries, re-checked in C# before anything is built from them (17a.6). A row that
    /// fails here fails the request: the SQL fragments already filtered it, so a failure means they
    /// and <see cref="EntryVisibility"/> have drifted, and dropping the row would truncate a correct
    /// answer instead (see <see cref="SearchDrift"/>).
    /// </summary>
    private async Task<List<Entry>> Loaded(IEnumerable<Guid> ids, SearchContext context, CancellationToken ct)
    {
        var loaded = await context.Session.LoadManyAsync<Entry>(ct, ids.ToArray());
        foreach (var entry in loaded)
        {
            SearchDrift.Guard(
                entry.MergedIntoId is null && EntryVisibility.CanSee(entry, context.Viewer), logger, "entry",
                entry.Id, context.Viewer.MemberId, nameof(EntryVisibility));
        }
        return [.. loaded];
    }

    private async Task<List<SearchDoc>> ArticlesAsync(
        SearchQuery query, SearchContext context, List<EntryMatch> named, CancellationToken ct)
    {
        var hits = await context.QueryAsync(
            ArticlesSql(context),
            command =>
            {
                command.Parameters.AddWithValue("campaign", context.CampaignId);
                command.Parameters.AddWithValue("me", SearchVisibilitySql.Me(context.Viewer));
                command.Parameters.AddWithValue("isDm", context.Viewer.Role == Role.DM);
                command.Parameters.AddWithValue("q", query.TsQuery!);
                command.Parameters.AddWithValue("named", named.Select(m => m.EntryId).Distinct().ToArray());
                command.Parameters.AddWithValue("limit", context.Take + 1);
                command.Parameters.AddWithValue("headline", Snippet.HeadlineOptions);
            },
            reader => new SearchDoc(
                SearchDocKind.ArticleBlock, reader.GetGuid(0), reader.GetGuid(1), reader.GetDouble(2),
                ArticleCategory, reader.GetString(3)),
            ct);

        // Belt and braces for blocks: the entry and its block are both re-checked against
        // EntryVisibility.CanSeeBlock before the block's text is shown as a snippet. A failure fails
        // the request, because `DISTINCT ON (d.id) … ORDER BY rank DESC` has already picked the
        // winning block: dropping it would not fall back to the next visible one, it would lose the
        // whole entry.
        var entries = (await context.Session.LoadManyAsync<Entry>(ct, hits.Select(h => h.SourceId).Distinct().ToArray()))
            .ToDictionary(e => e.Id);
        foreach (var hit in hits)
        {
            var block = entries.TryGetValue(hit.SourceId, out var entry)
                ? entry.Article.Blocks.FirstOrDefault(b => b.Id == hit.BlockId)
                : null;
            SearchDrift.Guard(
                entry is not null && block is not null && EntryVisibility.CanSeeBlock(entry, block, context.Viewer),
                logger, "block", hit.BlockId!.Value, context.Viewer.MemberId, $"{nameof(EntryVisibility)}.{nameof(EntryVisibility.CanSeeBlock)}");
        }
        return hits;
    }

    /// <summary>
    /// The article query. <c>DISTINCT ON</c> keeps the best-ranked visible block per entry, the
    /// middle level applies <c>LIMIT</c>, and only then is a snippet cut — from that one block, so
    /// there is no fragment boundary where another audience's text could slip in.
    /// <para>
    /// <b>How little it reads.</b> The entries the names already matched are excluded (<c>@named</c>),
    /// because a name match always wins, which makes <c>LIMIT @take + 1</c> exactly enough: an
    /// article hit is the last rung of the ladder, so one more than <c>take</c> is all that can ever
    /// be shown or change <c>hasMore</c>. Each block is then filtered on the <b>raw</b> block vector
    /// — the cheap one, the same expression the entry index is built on — before the
    /// <see cref="SearchSql.PlainText"/> vector is computed for it at all, which is what keeps the
    /// regexps off the blocks that cannot match. The two conjuncts are the same test the index is
    /// (<see cref="SearchSql.EntryArticleText"/>): raw holds every lexeme of the text except the ones
    /// <c>PlainText</c> joins together, so a block whose only match relies on a joined lexeme is
    /// missed here as it is missed by the index — a false negative, never a leak, and the same one
    /// either way.
    /// </para>
    /// </summary>
    private static string ArticlesSql(SearchContext context) => $"""
        SELECT t.entry_id, t.block_id, t.rank,
               ts_headline('{SearchSql.Config}', t.text, {SearchSql.TsQuery}, @headline) AS headline
        FROM (
            SELECT ranked.*
            FROM (
                SELECT DISTINCT ON (d.id)
                       d.id AS entry_id,
                       (b ->> 'Id')::uuid AS block_id,
                       ts_rank_cd(p.vector, {SearchSql.TsQuery}) AS rank,
                       p.text AS text
                FROM {SearchSql.Table(context.Session, SearchSql.EntryTable)} d
                CROSS JOIN LATERAL jsonb_array_elements(d.data -> 'Article' -> 'Blocks') AS b
                -- The block's plain text and its vector, once: the match, the rank and the snippet
                -- all read them, and OFFSET 0 stops the planner inlining the regexps into each.
                CROSS JOIN LATERAL (
                    SELECT {SearchSql.PlainText("b ->> 'Text'")} AS text,
                           to_tsvector('{SearchSql.Config}', {SearchSql.PlainText("b ->> 'Text'")}) AS vector
                    OFFSET 0
                ) p
                WHERE {SearchSql.CampaignFilter}
                  AND {SearchVisibilitySql.ListedEntries(context.Viewer)}
                  AND d.id <> ALL (@named)
                  AND {SearchSql.EntryArticleVector} @@ {SearchSql.TsQuery}
                  AND {SearchVisibilitySql.Block}
                  AND to_tsvector('{SearchSql.Config}', b ->> 'Text') @@ {SearchSql.TsQuery}
                  AND p.vector @@ {SearchSql.TsQuery}
                ORDER BY d.id, rank DESC
            ) ranked
            ORDER BY ranked.rank DESC
            LIMIT @limit
        ) t
        ORDER BY t.rank DESC
        """;
}
