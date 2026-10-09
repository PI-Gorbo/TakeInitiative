using Marten;
using Npgsql;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// The SQL the search is built from: the two indexed vectors, the text expressions the
/// queries share with them, the match ladder, and one small runner.
/// <para>
/// The note vector is an <b>index expression</b>: it is a constant here and the note query uses
/// the same constant, which is what lets Postgres recognise the expression and use the index.
/// Postgres rewrites an expression when it stores it (<c>'simple'::regconfig</c>,
/// <c>'Text'::text</c>, its own bracketing), so the constants are written in the form
/// <c>pg_get_indexdef</c> gives back, and <c>SearchSchemaTests</c> fails if Weasel ever sees a
/// difference. The article vector is a <b>stored generated column</b>
/// (<see cref="EntryArticleVectorColumn"/>) instead, so the article query names the column and
/// there is no expression to match: Postgres computes it once per write rather than once per row
/// per search.
/// </para>
/// <para>
/// <b>simple, not english.</b> Fantasy names dominate the text. The English stemmer turns
/// "Rockseeker" into <c>rockseek</c>, which breaks prefix-as-you-type, and drops stop words
/// people do type ("The Triboar Trail"). <c>simple</c> lowercases and splits, and the last
/// token of a query is a prefix.
/// </para>
/// </summary>
public static class SearchSql
{
    /// <summary>The text search configuration, in every <c>to_tsvector</c>, <c>to_tsquery</c> and <c>ts_headline</c>.</summary>
    public const string Config = "simple";

    public const string NoteTable = "mt_doc_sessionnote";
    public const string EntryTable = "mt_doc_entry";
    public const string SessionTable = "mt_doc_session";
    public const string CombatTable = "mt_doc_combat";

    public const string NoteIndexName = "mt_doc_sessionnote_idx_search";
    public const string EntryIndexName = "mt_doc_entry_idx_search";

    /// <summary>
    /// A session note's text as the note index and the note query read it: <see cref="PlainText"/>,
    /// so the index and the match agree exactly (17a.5, and the Notes' "the result set equals the
    /// one without the index"). A stored mention (<c>@[text](entry:&lt;id&gt;)</c>) keeps its display
    /// text and loses its destination, so a query can never match an entry id and a note is found by
    /// the words a reader sees (Notes, "A mention's display text is searched").
    /// </summary>
    public const string NoteText = $"{PlainTextPrefix}data ->> 'Text'{PlainTextSuffix}";

    /// <summary>The indexed vector, and the expression the note query matches and ranks with.</summary>
    public const string NoteVector = $"to_tsvector('{Config}', {NoteText})";

    /// <summary>
    /// Every block of an entry's article as one value, secret ones included. It is only a
    /// <b>prefilter</b>: it narrows the candidate entries, and each candidate is then re-matched
    /// block by block on the blocks the viewer can see, so a hit on a secret block alone yields
    /// nothing.
    /// <para>
    /// <b>It is a superset, with one exception.</b> <c>to_tsvector(regconfig, jsonb)</c> vectorises
    /// the string <i>values</i> of the document, so the vector holds each block's <b>raw</b> text
    /// while a block is matched on <see cref="PlainText"/> of it. Every lexeme of the raw text is
    /// therefore there, but <see cref="PlainText"/> can <b>join</b> two raw lexemes into one by
    /// taking a separator out of the middle of a word (<c>wo\rd</c> reads as <c>word</c>, and so do
    /// a deleted marker and a rewritten mention). That joined lexeme is in no vector, so such a block
    /// is missed: a false negative, never a leak. It cannot be fixed on the index side, because
    /// <c>to_tsvector(regconfig, jsonb)</c> gives no hook to transform each string value, and a
    /// regexp over the array's JSON encoding is not <see cref="PlainText"/> (JSON doubles a
    /// backslash and writes a newline as <c>\n</c>). Storing the vector in a column rather than
    /// computing it per row changes nothing about that: it is the same expression, evaluated once
    /// when the entry document is written. The note index has no such gap: <c>-&gt;&gt;</c> hands
    /// <see cref="PlainText"/> the string itself, so <see cref="NoteText"/> is exact.
    /// </para>
    /// </summary>
    public const string EntryArticleText =
        @"jsonb_path_query_array(data, '$.""Article"".""Blocks""[*].""Text""'::jsonpath)";

    /// <summary>
    /// The stored generated column the article prefilter reads (17a.3). Postgres computes
    /// <see cref="EntryArticleGeneration"/> once, in the same statement that writes the entry
    /// document, so a search never pays for it: an entry edit maintains it exactly as it maintains
    /// the row, with nothing to backfill and no lag (<c>SearchConsistencyTests</c>).
    /// </summary>
    public const string EntryArticleVectorColumn = "search_vector";

    /// <summary>
    /// What the column stores, written in the form <c>information_schema.columns</c>
    /// (<c>generation_expression</c>) gives back, so Weasel compares like with like and
    /// <c>SearchSchemaTests</c> sees no churn across restarts.
    /// </summary>
    public const string EntryArticleGeneration = $"to_tsvector('{Config}'::regconfig, {EntryArticleText})";

    /// <summary>
    /// The prefilter the article query uses: the column, not the expression, so the GIN index on it
    /// is usable and a sequential scan costs one column read per row instead of one
    /// <c>to_tsvector(jsonb_path_query_array(…))</c>. The alias is the one every search statement
    /// gives the document table.
    /// </summary>
    public const string EntryArticleVector = $"d.{EntryArticleVectorColumn}";

    /// <summary>The parsed query, from the <c>@q</c> parameter. <c>to_tsquery</c> is stable, so Postgres can still use the GIN index.</summary>
    public const string TsQuery = $"to_tsquery('{Config}', @q)";

    /// <summary>
    /// What a reader sees, from what is stored (17a.5). The four steps run in the order they have to:
    /// <list type="number">
    /// <item>backslash escapes are removed first, so an escaped <c>\(entry:…\)</c> cannot hide from
    /// the two steps that follow;</item>
    /// <item>a mention becomes its display text, so its destination is gone and no query can match
    /// an entry id;</item>
    /// <item>any <b>bare</b> <c>entry:&lt;id&gt;</c> still there is taken out, with or without its
    /// brackets. Note and block text is length-validated and nothing more, so a member can type a
    /// destination by hand or leave the tail of a malformed mention behind, and an unclosed
    /// <c>(entry:&lt;id&gt;</c> would otherwise reach a snippet: 17a.14 case 15 is about what a
    /// member can type, not about what the composer writes;</item>
    /// <item>the two private-use characters the snippet markers use are deleted, so no member can
    /// forge a highlight.</item>
    /// </list>
    /// <para>
    /// It is one line, and written in the form <c>pg_get_indexdef</c> gives back, because
    /// <see cref="NoteText"/> is an index expression: every function in it is immutable, and
    /// <c>SearchSchemaTests</c> fails if Weasel ever sees a difference between the two.
    /// </para>
    /// </summary>
    public static string PlainText(string textExpression) => PlainTextPrefix + textExpression + PlainTextSuffix;

    private const string PlainTextPrefix = "translate(regexp_replace(regexp_replace(regexp_replace(";

    private const string PlainTextSuffix =
        @", '\\(.)', '\1', 'g'), '@\[([^]]*)\]\(entry:[0-9a-fA-F-]{36}\)', '\1', 'g'), "
        + @"'\(?entry:[0-9a-fA-F-]{36}\)?', ' ', 'g'), chr(57344) || chr(57345), '')";

    /// <summary>
    /// The match ladder for a short string (a name, an alias or a session title), as the web's
    /// <c>matchRank</c> (15d) ranks it, extended with a fuzzy step: 0 exact, 1 prefix, 2 word
    /// prefix, 3 substring, 4 fuzzy, and NULL for no match. Both sides are already folded by the
    /// caller (<see cref="Folded"/>), so "gundren" matches "Gündren" as the <c>@</c> picker does.
    /// Fuzzy needs three characters: below that <c>word_similarity</c> matches nearly everything.
    /// <para>
    /// <paramref name="similarity"/> is the trigram similarity of the two, already computed: nearly
    /// every candidate falls through to the fuzzy rung, and the callers need the number again to
    /// break ties inside a rung, so computing it once and passing it in here is half the
    /// <c>word_similarity</c> calls of a campaign's worth of names. Null computes it in place.
    /// </para>
    /// </summary>
    public static string MatchCategory(
        string candidate, string query, string? similarity = null, string minSimilarity = "@minSimilarity") => $$"""
        CASE
            WHEN {{candidate}} = {{query}} THEN 0
            WHEN strpos({{candidate}}, {{query}}) = 1 THEN 1
            WHEN strpos({{candidate}}, {{query}}) > 1
                 AND substring({{candidate}} from strpos({{candidate}}, {{query}}) - 1 for 1) ~ '[\s\-''"(]' THEN 2
            WHEN strpos({{candidate}}, {{query}}) > 1 THEN 3
            WHEN length({{query}}) >= 3
                 AND {{similarity ?? $"word_similarity({query}, {candidate})"}} >= {{minSimilarity}} THEN 4
            ELSE NULL
        END
        """;

    /// <summary>
    /// Lower case with accents folded, the SQL twin of the web's <c>foldForMatch</c>. Both the
    /// candidate and the query go through it in SQL rather than one of them in C#, so the two can
    /// never disagree about what a fold is. <c>unaccent</c> is not immutable, which is why no
    /// index uses this (Notes, "Accents").
    /// </summary>
    public static string Folded(string expression) => $"lower(unaccent({expression}))";

    /// <summary>The campaign filter, in the form Marten's own <c>(CampaignId, …)</c> indexes use, so they serve it.</summary>
    public const string CampaignFilter = "(d.data ->> 'CampaignId')::uuid = @campaign";

    /// <summary>
    /// Runs one search statement on <paramref name="connection"/>, the one connection the request
    /// reuses (<see cref="SearchConnection"/>). A null connection opens one for this statement alone,
    /// which is what a caller outside a search (the entry matcher from step 19 or 23, a test) gets.
    /// </summary>
    public static async Task<List<T>> QueryAsync<T>(
        SearchConnection? connection, IQuerySession session, string sql,
        Action<NpgsqlCommand> parameters, Func<NpgsqlDataReader, T> read, CancellationToken ct)
    {
        if (connection is null)
        {
            await using var own = new SearchConnection(session);
            return await QueryAsync(own, sql, parameters, read, ct);
        }
        return await QueryAsync(connection, sql, parameters, read, ct);
    }

    /// <summary>
    /// Runs one search statement, opening a connection of its own for it. Search reads only, and it
    /// reads rows that are already committed, so a connection from the session's database is as good
    /// as the session's own: a lightweight session closes its connection between commands.
    /// </summary>
    public static Task<List<T>> QueryAsync<T>(
        IQuerySession session, string sql, Action<NpgsqlCommand> parameters, Func<NpgsqlDataReader, T> read, CancellationToken ct)
        => QueryAsync(null, session, sql, parameters, read, ct);

    private static async Task<List<T>> QueryAsync<T>(
        SearchConnection connection, string sql, Action<NpgsqlCommand> parameters, Func<NpgsqlDataReader, T> read, CancellationToken ct)
    {
        await using var command = (await connection.Opened(ct)).CreateCommand();
        command.CommandText = sql;
        parameters(command);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var rows = new List<T>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(read(reader));
        }
        return rows;
    }

    /// <summary>The table, in the schema this store keeps its documents in.</summary>
    public static string Table(IQuerySession session, string table)
        => Table(session.DocumentStore.Options.DatabaseSchemaName, table);

    /// <summary>The table, in a named schema: the form a test has, which holds the store and not a session.</summary>
    public static string Table(string schema, string table) => $"{schema}.{table}";
}
