namespace TakeInitiative.KnowledgeBase.Schema;

/// <summary>
/// The <c>knowledge_base_item</c> table, as SQL and as names. One table for the whole knowledge
/// base rather than one per provider, because the browse page (26e) and ⌘K both want to query
/// across providers.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is only the text.</b> Creating it is the API's job: the statements below are wrapped in
/// a Weasel <c>ISchemaObject</c> and registered as a <c>Storage.ExtendedSchemaObjects</c> entry, so
/// <c>ApplyAllDatabaseChangesOnStartup</c> creates the table on start in every environment (26c
/// step 1). The ingest CLI never runs DDL — it checks that the table is there
/// (<see cref="TableExistsSql" />) and stops with a message if it is not. One owner for the schema.
/// </para>
/// <para>
/// The constants live in this package rather than in the API because three callers need to agree on
/// them: the API creates the table, the store writes to it, and the CLI checks for it. Duplicating a
/// column name across those three is exactly the kind of drift that shows up as a runtime error on
/// an operator's machine.
/// </para>
/// <para>
/// <b>The primary key is <c>(provider, id)</c> with the parser's own id</b>, not a generated one.
/// <c>monster_beholder_mm</c> is formed from category, name and source and is stable across a
/// re-ingest, which is what makes a user's link (step 27) durable. A generated key would move every
/// row on every ingest and break every link with it.
/// </para>
/// <para>
/// <b>Search</b> gets the two-pronged treatment step 17 uses for entries: a stored generated
/// <c>tsvector</c> over the name for word matching, and a <c>gin_trgm_ops</c> index over
/// <c>lower(name)</c> for the fuzzy matching <c>ReferenceMatcher</c> does. The vector is
/// <c>simple</c>, not <c>english</c>, for the reason <c>SearchSql</c> gives: the English stemmer
/// turns "Rockseeker" into <c>rockseek</c> and breaks prefix-as-you-type, and a bestiary is mostly
/// invented words.
/// </para>
/// <para>
/// <b>The trigram index needs <c>pg_trgm</c></b>, which <c>Bootstrap</c> registers as an extended
/// schema object before this one. Weasel writes extended objects in the order they were added, so
/// the extension exists by the time this runs; if that ever changed, the <c>create index</c> would
/// fail loudly at startup with "operator class gin_trgm_ops does not exist" rather than quietly
/// leaving the index off.
/// </para>
/// <para>
/// <b>There is no <c>detail</c> column.</b> The plan's schema sketch listed both <c>label</c>
/// ('CR 13') and <c>detail</c> ('CR 13 · Large Aberration'), but the parser that landed in 26b emits
/// one string — <see cref="KnowledgeBaseItem.Label" /> — and it is the full muted line, so a
/// <c>detail not null</c> column would have had nothing to hold but a copy of <c>label</c>.
/// </para>
/// </remarks>
public static class KnowledgeBaseSchema
{
    /// <summary>The schema Marten is configured with by default, and the CLI's default.</summary>
    public const string DefaultDatabaseSchema = "public";

    /// <summary>The table, unqualified.</summary>
    public const string TableName = "knowledge_base_item";

    /// <summary>The text search configuration, matching <c>SearchSql.Config</c>.</summary>
    public const string SearchConfig = "simple";

    /// <summary>The stored generated <c>tsvector</c> column over <c>name</c>.</summary>
    public const string SearchVectorColumn = "search_vector";

    /// <summary>What <see cref="SearchVectorColumn" /> is generated from.</summary>
    public const string SearchVectorGeneration = $"to_tsvector('{SearchConfig}'::regconfig, name)";

    /// <summary>The GIN index over <see cref="SearchVectorColumn" />.</summary>
    public const string SearchIndexName = "knowledge_base_item_idx_search";

    /// <summary>The <c>gin_trgm_ops</c> index over <c>lower(name)</c>.</summary>
    public const string NameTrigramIndexName = "knowledge_base_item_idx_name_trgm";

    /// <summary>The browse page's filter index, <c>(provider, category, source_book)</c>.</summary>
    public const string FilterIndexName = "knowledge_base_item_idx_filter";

    /// <summary>
    /// Every relation <see cref="CreateSql" /> creates, which is what <see cref="ExistsSql" />
    /// counts. An index is a <c>pg_class</c> entry like a table, so one query covers all four.
    /// </summary>
    public static IReadOnlyList<string> ObjectNames { get; } =
        [TableName, SearchIndexName, NameTrigramIndexName, FilterIndexName];

    /// <summary>
    /// The whole schema, idempotent. Every statement is <c>if not exists</c>, so re-running it
    /// against a database that already has the table is a no-op rather than an error — which is what
    /// lets the API apply it on every start without a migration history.
    /// </summary>
    /// <param name="databaseSchema">
    /// The Postgres schema Marten is configured with. It is a configured constant, never input.
    /// </param>
    public static string CreateSql(string databaseSchema = DefaultDatabaseSchema)
    {
        var table = Qualified(databaseSchema);

        return $"""
                create table if not exists {table} (
                    provider      text        not null,
                    id            text        not null,
                    name          text        not null,
                    category      text        not null,
                    source_book   text        not null,
                    source_title  text,
                    page          int,
                    label         text,
                    url           text        not null,
                    image_url     text,
                    stats         jsonb,
                    content_hash  text        not null,
                    batch         uuid        not null,
                    stale         boolean     not null default false,
                    ingested_at   timestamptz not null,
                    {SearchVectorColumn} tsvector generated always as ({SearchVectorGeneration}) stored,
                    primary key (provider, id)
                );
                create index if not exists {SearchIndexName} on {table} using gin ({SearchVectorColumn});
                create index if not exists {NameTrigramIndexName} on {table} using gin (lower(name) gin_trgm_ops);
                create index if not exists {FilterIndexName} on {table} (provider, category, source_book);
                """;
    }

    /// <summary>Drops the table, and with it its indexes.</summary>
    public static string DropSql(string databaseSchema = DefaultDatabaseSchema) =>
        $"drop table if exists {Qualified(databaseSchema)} cascade;";

    /// <summary>
    /// How many of <see cref="ObjectNames" /> exist. Weasel's delta reads this and asks for a
    /// create unless every one of them is there, so a table created before an index was added to
    /// this file still gets the index on the next start.
    /// </summary>
    public static string ExistsSql(string databaseSchema = DefaultDatabaseSchema) =>
        "select count(*) from pg_class c join pg_namespace n on n.oid = c.relnamespace "
        + $"where n.nspname = '{databaseSchema}' "
        + $"and c.relname in ({string.Join(", ", ObjectNames.Select(name => $"'{name}'"))});";

    /// <summary>
    /// Whether the table itself is there. This is the CLI's check: it cares that it has somewhere to
    /// write, not that every index has been built.
    /// </summary>
    public static string TableExistsSql(string databaseSchema = DefaultDatabaseSchema) =>
        $"select to_regclass('{databaseSchema}.{TableName}') is not null;";

    /// <summary>The table, schema-qualified and quoted.</summary>
    public static string Qualified(string databaseSchema = DefaultDatabaseSchema) =>
        $"\"{databaseSchema}\".\"{TableName}\"";
}
