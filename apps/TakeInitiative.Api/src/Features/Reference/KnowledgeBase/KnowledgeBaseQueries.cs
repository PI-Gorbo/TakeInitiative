using Marten;

using Npgsql;

using NpgsqlTypes;

using TakeInitiative.Api.Features.Entries;
using TakeInitiative.Api.Features.Search;
using TakeInitiative.KnowledgeBase.Schema;

namespace TakeInitiative.Api.Features.Reference.KnowledgeBase;

/// <summary>
/// Every read of <c>knowledge_base_item</c>: ⌘K's search (26d₂), one row by id, and the browse
/// page's page and facet counts (26e). Plain SQL on the Marten session's database, the way
/// <c>EntryMatcher</c> and the search providers do it, because none of this is a document query.
/// </summary>
/// <remarks>
/// <para>
/// <b>The ranking is <c>SearchSql.MatchCategory</c>'s, not a second ladder.</b> The Reference
/// section merges every provider's matches on one ordering — rung, then similarity, then the
/// provider's registration order, then the name (<c>ReferenceSearchProvider</c>) — so a provider that
/// scored its rows any other way would reorder the section. The SRD's rows are scored in C# by
/// <c>ReferenceMatcher</c>, which is that same ladder ported; these are scored by the SQL the port was
/// made from. Both fold with <c>lower(unaccent(…))</c>, both give an exact match a similarity of 1,
/// and both break ties by the shorter name and then the name, so SRD still comes first at an equal
/// rung and an equal similarity.
/// </para>
/// <para>
/// <b>What the two indexes can and cannot decide.</b> The <c>WHERE</c> is a disjunction, and it has to
/// be a <i>superset</i> of what the ladder then matches or a row is lost:
/// </para>
/// <list type="number">
///   <item><description>
///   <c>search_vector @@ to_tsquery('simple', …)</c> — the GIN index over the generated
///   <c>tsvector</c>. It answers the words of the query with the last one as a prefix, which is most
///   of rungs 0–2 and the only thing a one- or two-character query can match at all.
///   </description></item>
///   <item><description>
///   <c>lower(name) like '%…%'</c> — the <c>gin_trgm_ops</c> index over <c>lower(name)</c>, which
///   Postgres can serve a <c>LIKE</c> from. Every one of rungs 0–3 means the folded query is a
///   substring of the folded name, so for a name that folds to itself this term alone covers them.
///   </description></item>
///   <item><description>
///   the fuzzy rung, <c>word_similarity(query, folded name) &gt;= @minSimilarity</c>. <b>No index can
///   decide this one.</b> <c>pg_trgm</c>'s own <c>&lt;%</c> operator is index-backed but reads
///   <c>pg_trgm.word_similarity_threshold</c> (0.6 by default) rather than
///   <c>EntryMatchOptions.Default.MinSimilarity</c> (0.5), so it would drop matches the SRD provider
///   finds; and the comparison is against the accent-folded name, which <c>unaccent</c>'s volatility
///   keeps out of every index.
///   </description></item>
///   <item><description>
///   <c>lower(name) &lt;&gt; lower(unaccent(name))</c> — a name the fold changes. For those rows
///   <c>lower(name)</c> is not the text terms 1 and 2 compare against, so "uber" would otherwise miss
///   "Überwald" where the in-memory provider from 21b found it. This term is what makes the statement
///   exactly as good as the list it replaces rather than nearly as good.
///   </description></item>
/// </list>
/// <para>
/// So: a query of one or two characters is served by the indexes, and a longer one pays a scan of the
/// provider's rows for the fuzzy rung — which is what <c>EntryMatcher</c> already does for a
/// campaign's entry names ("at most a few thousand short strings, so there is no index on them") and
/// what the in-memory provider did for all of them. If the corpus ever outgrows that, the fix is a
/// stored <c>lower(unaccent(name))</c> column written by the ingest, indexed with
/// <c>gin_trgm_ops</c>; that makes terms 2–4 collapse into one index scan, and it is a schema change
/// rather than a query change.
/// </para>
/// </remarks>
public class KnowledgeBaseQueries(IQuerySession session)
{
    /// <summary>The fuzzy rung's floor, shared with the SRD provider so both answer the same rows.</summary>
    public static double MinSimilarity => EntryMatchOptions.Default.MinSimilarity;

    private string? qualifiedTable;

    /// <summary>
    /// The table, in the schema Marten is configured with. Resolved on first use rather than in the
    /// constructor, so building the provider costs nothing until something asks it a question — which is
    /// also what lets <c>AddReference()</c> be asserted without a database behind it.
    /// </summary>
    private string Table =>
        qualifiedTable ??= KnowledgeBaseSchema.Qualified(session.DocumentStore.Options.DatabaseSchemaName);

    /// <summary>
    /// The best <paramref name="take" /> rows of <paramref name="provider" /> whose name matches
    /// <paramref name="text" />, ranked on the ladder. Empty for an empty query, an empty table, or a
    /// query that holds nothing a trigram can be made of.
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeBaseMatchRow>> SearchAsync(
        string provider, string text, int take, CancellationToken ct)
    {
        var query = text.Trim();
        if (query.Length == 0 || take <= 0)
        {
            return [];
        }

        var rows = await SearchSql.QueryAsync(
            session,
            SearchSqlText(),
            command =>
            {
                AddText(command, "provider", provider);
                AddText(command, "query", query);
                AddTsQuery(command, query);
                command.Parameters.AddWithValue("minSimilarity", MinSimilarity);
                command.Parameters.AddWithValue("take", take);
            },
            reader => new KnowledgeBaseMatchRow(Read(reader), reader.GetInt32(13), reader.GetDouble(14)),
            ct);

        return rows;
    }

    /// <summary>One row by its provider and id, or null.</summary>
    public async Task<KnowledgeBaseItemRow?> FindAsync(string provider, string id, CancellationToken ct)
    {
        var rows = await SearchSql.QueryAsync(
            session,
            $"select {Columns("k")} from {Table} k where k.provider = @provider and k.id = @id",
            command =>
            {
                AddText(command, "provider", provider);
                AddText(command, "id", id);
            },
            Read,
            ct);

        return rows.SingleOrDefault();
    }

    /// <summary>
    /// The rows named by <paramref name="keys" />, with each one's <c>stale</c> flag, in <b>one</b>
    /// statement whatever the keys are — which is what <see cref="EntryLinkResolver" /> needs and
    /// why this exists beside <see cref="FindAsync" /> rather than being a loop over it. An entry may
    /// carry twenty links, and twenty round trips per entry read is the thing the plan's "one query
    /// per request, never one per link" forbids.
    /// <para>
    /// Keys are <c>(provider, id)</c> pairs because they are the table's primary key and a link may
    /// name any provider. The two halves go as parallel arrays and are zipped back together by
    /// <c>unnest</c>, so the statement's text does not depend on how many there are and Postgres can
    /// plan it once. A key with no row simply has no row in the answer: that is the stale case
    /// (27b), not an error.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeBaseLinkRow>> ByIdsAsync(
        IReadOnlyCollection<(string Provider, string Id)> keys, CancellationToken ct)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        return await SearchSql.QueryAsync(
            session,
            $"""
             select {Columns("k")}, k.stale
             from unnest(@providers, @ids) as w(provider, id)
             join {Table} k on k.provider = w.provider and k.id = w.id
             """,
            command =>
            {
                AddTextArray(command, "providers", [.. keys.Select(key => key.Provider)]);
                AddTextArray(command, "ids", [.. keys.Select(key => key.Id)]);
            },
            reader => new KnowledgeBaseLinkRow(Read(reader), reader.GetBoolean(13)),
            ct);
    }

    /// <summary>
    /// One page of the browse list (26e) and the facet counts beside it, on one connection. The
    /// filters are all optional; <paramref name="query" /> ranks the page on the same ladder ⌘K uses
    /// and, when it is absent, the page is by name.
    /// </summary>
    public async Task<(KnowledgeBasePage Page, KnowledgeBaseFacets Facets)> BrowseAsync(
        KnowledgeBaseBrowse browse, CancellationToken ct)
    {
        var query = string.IsNullOrWhiteSpace(browse.Query) ? null : browse.Query.Trim();

        void Parameters(NpgsqlCommand command)
        {
            AddText(command, "provider", browse.Provider);
            AddText(command, "category", browse.Category?.ToString());
            AddText(command, "book", browse.Book);
            AddText(command, "query", query);
            AddTsQuery(command, query);
            command.Parameters.AddWithValue("minSimilarity", MinSimilarity);
        }

        // One connection for both statements: the second is the same scan as the first, and a
        // connection per statement is a pool round trip for nothing (SearchConnection's own reason).
        await using var connection = new SearchConnection(session);

        var rows = await SearchSql.QueryAsync(
            connection,
            session,
            BrowseSqlText(query is not null),
            command =>
            {
                Parameters(command);
                command.Parameters.AddWithValue("skip", browse.Skip);
                command.Parameters.AddWithValue("take", browse.Take);
            },
            reader => Read(reader),
            ct);

        var facets = await SearchSql.QueryAsync(
            connection,
            session,
            FacetSqlText(query is not null),
            Parameters,
            reader => (
                Kind: reader.GetString(0),
                Facet: new KnowledgeBaseFacet(
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    (int)reader.GetInt64(3))),
            ct);

        var total = facets.Where(f => f.Kind == TotalKind).Select(f => f.Facet.Count).FirstOrDefault();
        return (
            new KnowledgeBasePage(rows, total),
            new KnowledgeBaseFacets(
                total,
                [.. facets.Where(f => f.Kind == CategoryKind).Select(f => f.Facet)],
                [.. facets.Where(f => f.Kind == BookKind).Select(f => f.Facet)]));
    }

    // --- the SQL ---------------------------------------------------------------------------------

    private const string TotalKind = "total";
    private const string CategoryKind = "category";
    private const string BookKind = "book";

    /// <summary>
    /// The folded query, once. Every comparison below reads it, and folding it per row would be
    /// <c>unaccent</c> called once per row for a value that does not change.
    /// </summary>
    private const string FoldedQuery = $"""
        q as (
            select {SearchQueryFold} as folded,
                   '%' || replace(replace(replace({SearchQueryFold}, '\', '\\'), '%', '\%'), '_', '\_') || '%' as pattern
        )
        """;

    private const string SearchQueryFold = "lower(unaccent(@query))";

    /// <summary>The columns every read selects, in the order <see cref="Read" /> takes them.</summary>
    private static string Columns(string alias) =>
        $"{alias}.provider, {alias}.id, {alias}.name, {alias}.category, {alias}.source_book, "
        + $"{alias}.source_title, {alias}.page, {alias}.label, {alias}.url, {alias}.image_url, "
        + $"({alias}.stats ->> 'ac')::int as ac, {alias}.stats ->> 'hp' as hp, "
        + $"({alias}.stats ->> 'initiativeBonus')::int as initiative_bonus";

    /// <summary>
    /// The four-term disjunction the class comment explains. It narrows; it never decides, because
    /// the ladder in the next CTE is what says whether a row matched.
    /// </summary>
    private static string Prefilter(string alias) => $"""
        (
            {alias}.search_vector @@ to_tsquery('{SearchSql.Config}', @tsquery)
            or lower({alias}.name) like q.pattern escape '\'
            or (length(q.folded) >= {ReferenceMatcher.FuzzyMinLength}
                and word_similarity(q.folded, {SearchSql.Folded($"{alias}.name")}) >= @minSimilarity)
            or lower({alias}.name) <> {SearchSql.Folded($"{alias}.name")}
        )
        """;

    /// <summary>
    /// The row, the rung and the similarity. <c>OFFSET 0</c> stops the planner from inlining the
    /// lateral back into the two places that read it, so there is one <c>word_similarity</c> per row
    /// rather than one for the fuzzy rung and one for the tie-break — the trick
    /// <c>EntryMatcher</c> uses for the same reason.
    /// </summary>
    private string Ladder(string extraFilter) => $"""
        select k.provider, k.id, k.name, k.category, k.source_book, k.source_title, k.page,
               k.label, k.url, k.image_url, k.stats,
               {SearchSql.MatchCategory(SearchSql.Folded("k.name"), "q.folded", "w.similarity")} as rung,
               w.similarity as similarity
        from {Table} k
        cross join q
        cross join lateral (select word_similarity(q.folded, {SearchSql.Folded("k.name")}) as similarity offset 0) w
        where {extraFilter}
          and {Prefilter("k")}
        """;

    private string SearchSqlText() => $"""
        with {FoldedQuery},
        matched as (
            {Ladder("k.provider = @provider")}
        )
        select {Columns("m")}, m.rung,
               case when m.rung = 0 then 1::float8 else m.similarity::float8 end as score
        from matched m
        where m.rung is not null
        order by m.rung, score desc, length(m.name), m.name, m.id
        limit @take
        """;

    /// <summary>
    /// The browse scan: every row of the provider, with the ladder applied when there is a query and
    /// no ladder at all when there is not. A browse with no query is a filtered list, not a search,
    /// and running <c>word_similarity</c> against a null query for every row to then ignore it would
    /// be a scan for nothing.
    /// </summary>
    private string Scoped(bool hasQuery) => hasQuery
        ? $"""
          scoped as materialized (
              select * from ({Ladder("(@provider is null or k.provider = @provider)")}) l
              where l.rung is not null
          )
          """
        : $"""
          scoped as materialized (
              select k.provider, k.id, k.name, k.category, k.source_book, k.source_title, k.page,
                     k.label, k.url, k.image_url, k.stats, null::int as rung, 0::float8 as similarity
              from {Table} k
              where (@provider is null or k.provider = @provider)
          )
          """;

    private const string PickedFilters = """
        picked as (
            select * from scoped s
            where (@category is null or s.category = @category)
              and (@book is null or s.source_book = @book)
        )
        """;

    private string BrowseSqlText(bool hasQuery) => $"""
        with {FoldedQuery},
        {Scoped(hasQuery)},
        {PickedFilters}
        select {Columns("p")}
        from picked p
        order by {(hasQuery ? "p.rung, p.similarity desc, length(p.name), p.name" : "p.name")}, p.provider, p.id
        offset @skip
        limit @take
        """;

    /// <summary>
    /// The page's total, then a count per category and a count per book. Each facet drops its <b>own</b>
    /// filter and keeps the other, which is what makes the number beside a chip mean "what choosing
    /// this would show". Counting the filtered page instead would show the total beside the chosen
    /// value and a zero beside every other one.
    /// </summary>
    private string FacetSqlText(bool hasQuery) => $"""
        with {FoldedQuery},
        {Scoped(hasQuery)},
        {PickedFilters}
        select '{TotalKind}' as kind, '' as value, null::text as title, count(*) as n from picked
        union all
        select '{CategoryKind}', s.category, null::text, count(*)
        from scoped s
        where (@book is null or s.source_book = @book)
        group by s.category
        union all
        select '{BookKind}', s.source_book, min(s.source_title), count(*)
        from scoped s
        where (@category is null or s.category = @category)
        group by s.source_book
        order by 1, 2
        """;

    // --- reading ---------------------------------------------------------------------------------

    private static KnowledgeBaseItemRow Read(NpgsqlDataReader reader) => new()
    {
        Provider = reader.GetString(0),
        Id = reader.GetString(1),
        Name = reader.GetString(2),
        Category = Enum.Parse<ReferenceCategory>(reader.GetString(3)),
        SourceBook = reader.GetString(4),
        SourceTitle = reader.IsDBNull(5) || reader.GetString(5).Length == 0 ? null : reader.GetString(5),
        Page = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        Label = reader.IsDBNull(7) ? null : reader.GetString(7),
        Url = reader.GetString(8),
        ImageUrl = reader.IsDBNull(9) ? null : reader.GetString(9),
        Stats = reader.IsDBNull(10) || reader.IsDBNull(11) || reader.IsDBNull(12)
            ? null
            : Stats.Of(ReferenceStats.Initiative(reader.GetInt32(12)), reader.GetString(11), reader.GetInt32(10)),
    };

    /// <summary>
    /// The <c>to_tsquery</c> argument for <paramref name="text" />, built by <see cref="SearchQuery" />
    /// so that ⌘K and this send Postgres the same thing. Null — for text with no letters or digits in
    /// it at all — makes the <c>@@</c> term of the disjunction null, which is not true, so the term
    /// simply does not contribute.
    /// </summary>
    private static void AddTsQuery(NpgsqlCommand command, string? text) =>
        AddText(command, "tsquery", text is null ? null : SearchQuery.Parse(text)?.TsQuery);

    /// <summary>
    /// A <c>text</c> parameter, null where there is no value. The type is stated rather than inferred
    /// because every one of these is compared against a null in <c>@x is null or …</c>, and Postgres
    /// cannot work out the type of a parameter it only ever sees beside <c>is null</c>.
    /// </summary>
    private static void AddText(NpgsqlCommand command, string name, string? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value,
        });

    /// <summary>
    /// A <c>text[]</c> parameter. <see cref="ByIdsAsync" />'s two of them are zipped by
    /// <c>unnest</c>, which needs the element type stated for the same reason <see cref="AddText" />
    /// does: Postgres has nothing else to infer it from.
    /// </summary>
    private static void AddTextArray(NpgsqlCommand command, string name, string[] values) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = values });
}

/// <summary>
/// What the browse endpoint asks for (26e): the filters, all optional, and the page.
/// </summary>
/// <param name="Provider">One corpus, or every one of them.</param>
/// <param name="Category">Monster, Spell or Item, or all three.</param>
/// <param name="Book">One source book's abbreviation, or every book.</param>
/// <param name="Query">The search text, or null for a plain filtered list by name.</param>
/// <param name="Skip">How many rows to step over. Bounded by the endpoint.</param>
/// <param name="Take">How many rows to answer. Capped by the endpoint.</param>
public sealed record KnowledgeBaseBrowse(
    string? Provider,
    ReferenceCategory? Category,
    string? Book,
    string? Query,
    int Skip,
    int Take);
