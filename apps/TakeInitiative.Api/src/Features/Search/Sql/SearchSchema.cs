using System.Data.Common;
using System.Reflection;
using Marten;
using Marten.Schema;
using Weasel.Core;
using Weasel.Core.Migrations;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace TakeInitiative.Api.Features.Search;

/// <summary>
/// A <c>GENERATED ALWAYS AS (…) STORED</c> clause on a Weasel column. Weasel has no notion of a
/// generated column, but it does have <see cref="ColumnCheck"/>: a column's declaration is
/// <c>name type</c> followed by its checks, which is exactly where the clause belongs, and
/// Weasel writes the column the same way whether it is creating the table or adding the column to
/// an existing one.
/// <para>
/// A check is deliberately <b>not</b> part of a column's identity: Weasel reads the existing
/// columns from <c>information_schema.columns</c>, which gives it a name and a type and nothing
/// else, so it compares <c>search_vector tsvector</c> with <c>search_vector tsvector</c> and sees
/// no difference. That is what keeps a stored column out of the schema diff, where a column Weasel
/// did not know about would be dropped on every start (<c>SearchSchemaTests</c> proves it).
/// </para>
/// </summary>
public sealed class GeneratedAlwaysStored(string expression) : ColumnCheck
{
    public override string Declaration() => $"GENERATED ALWAYS AS ({expression}) STORED";
}

/// <summary>
/// The schema ⌘K's article prefilter needs (17a.3): a stored generated <c>tsvector</c> column on
/// <c>mt_doc_entry</c> and a GIN index on it.
/// <para>
/// <b>Why a column and not an expression index.</b> The planner does not choose a GIN index for a
/// prefix <c>tsquery</c> at a campaign's size — a partial-match scan of the whole index costs more
/// than the sequential scan it has to do anyway — so an expression index left Postgres computing
/// <c>to_tsvector(jsonb_path_query_array(…))</c> for every entry in the table on every search, which
/// measured 30 ms of the 33 ms the article query took on the large seed. A stored column is computed
/// once, by Postgres, in the same statement that writes the entry document, so the filter is one
/// column read per row whichever plan is chosen, and the GIN index on it is a plain column index.
/// Nothing about visibility, ranking or snippets changes: the column holds the same vector the
/// expression did (<see cref="SearchSql.EntryArticleText"/>), and the per-block re-match is untouched.
/// </para>
/// <para>
/// <b>Why it goes on Marten's own table.</b> Weasel compares the table it is configured with against
/// the table in the database and drops any column it does not know about, so a column added beside
/// Marten — a <c>Storage.ExtendedSchemaObjects</c> entry, a migration by hand — leaves
/// <c>mt_doc_entry</c> permanently "Update" in the diff: dropped and recreated on every
/// <c>ApplyAllDatabaseChangesOnStartup</c>, and
/// <c>AssertDatabaseMatchesConfigurationAsync()</c> never clean again. So the column is added to the
/// document table Marten builds from the mapping, which is the table Weasel then creates, alters and
/// compares. Marten 9.45 still has no public API for a column on a document table (only
/// <c>DuplicateField</c>, which a generated column cannot be: Marten writes a duplicated column
/// itself, and a generated column is Postgres's to write), so the table is reached through the
/// mapping's own schema. It is cached, so there is one
/// instance and the column is on it before any migration is computed.
/// </para>
/// <para>
/// <b>The note index is left as it was.</b> It is an expression index, it is exact, and the note
/// query measures about a millisecond: there is nothing for a column to buy there.
/// </para>
/// </summary>
public static class SearchSchema
{
    /// <summary>
    /// Adds the article vector column and its GIN index to <c>Entry</c>'s document table. Called from
    /// <c>Bootstrap</c> straight after the rest of the <c>Entry</c> mapping, so the mapping is
    /// complete when the table is built from it.
    /// </summary>
    public static void AddEntryArticleVector(StoreOptions options)
    {
        // IReadOnlyStoreOptions is how the mapping is reachable without Marten's internals; the
        // Entry mapping already exists, because Schema.For<Entry>() above built it.
        var mapping = (DocumentMapping)((IReadOnlyStoreOptions)options).FindOrResolveDocumentType(typeof(Entry));

        // The index first: the document table takes a copy of the mapping's indexes when it is
        // built, and asking the mapping for its schema is what builds it.
        var index = mapping.AddIndex(SearchSql.EntryArticleVectorColumn);
        index.Name = SearchSql.EntryIndexName;
        index.Method = IndexMethod.gin;

        // DocumentMapping.Schema is internal, so it has to be read reflectively; what comes back is
        // an internal type that implements Weasel's public IFeatureSchema, and the
        // document table is one of its objects. Marten caches the schema behind a Lazy, so this is
        // the one table instance every migration and every assertion is computed from. If a Marten
        // upgrade ever moves it, startup fails here with this message rather than quietly searching
        // an expression again.
        var property = typeof(DocumentMapping).GetProperty(
                "Schema", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Marten's DocumentMapping no longer exposes Schema, so ⌘K's article vector column "
                + $"({SearchSql.EntryArticleVectorColumn} on {SearchSql.EntryTable}) cannot be added "
                + "to the document table. See SearchSchema.");
        var table = ((IFeatureSchema)property.GetValue(mapping)!).Objects.OfType<Table>()
            .Single(t => t.Identifier.Name == SearchSql.EntryTable);

        var column = new TableColumn(SearchSql.EntryArticleVectorColumn, "tsvector");
        column.ColumnChecks.Add(new GeneratedAlwaysStored(SearchSql.EntryArticleGeneration));
        table.AddColumn(column);

        // And the storage parameter that keeps the entry document in the row with it. See
        // EntryRowFitsInline.
        options.Storage.Add(new EntryRowFitsInline(table.Identifier));
    }
}

/// <summary>
/// <c>toast_tuple_target</c> on <c>mt_doc_entry</c>, so an entry's row holds both its document and
/// its article vector.
/// <para>
/// <b>Why it is needed.</b> Postgres moves the biggest column of an oversized row out of line, and
/// an entry's <c>data</c> (about 1.4 KB compressed on the large seed) plus the article vector (about
/// 1.2 KB) is over the default 2 KB target, so adding the vector pushed <c>data</c> into the TOAST
/// table. Every name match reads <c>data</c> for every entry in the campaign, and it measured
/// 4.9 ms → 17.4 ms: 200 buffers became 9,179, and the entry matcher lost more than the prefilter
/// won. With the target raised nothing is pushed out, and the scan measures 2.0 ms — better than the
/// 4.9 ms it started at, because a row written under the raised target is not compressed either, so
/// a name match no longer decompresses every row it reads. It costs disk: <c>mt_doc_entry</c> is
/// 4.0 MB for 1,000 entries where it was 1.6 MB compressed.
/// </para>
/// <para>
/// <b>Why it is a schema object of its own.</b> A storage parameter is not part of a table's
/// definition as Weasel models one — it writes no <c>WITH (…)</c> and reads no <c>reloptions</c> — so
/// it can neither be declared on the document table nor cause churn there. It is a schema object
/// instead, in a feature that <see cref="DependentTypes"/> puts after <c>Entry</c>'s, so the
/// <c>ALTER TABLE</c> runs once the table exists.
/// </para>
/// <para>
/// It only affects rows written after it. On a fresh database that is every row. On a database that
/// already has entries, Weasel's <c>ADD COLUMN</c> rewrites the table just before this runs, so
/// those rows keep the old storage until they are next written — each entry settles itself on its
/// next edit, and <c>VACUUM FULL</c> on <c>mt_doc_entry</c> settles them all at once.
/// </para>
/// </summary>
public sealed class EntryRowFitsInline(DbObjectName table) : ISchemaObject, IFeatureSchema
{
    /// <summary>The largest Postgres allows: a row up to just under one page stays in the page.</summary>
    public const int ToastTupleTarget = 8160;

    private const string Parameter = "toast_tuple_target";

    /// <summary>
    /// Not a database object of its own, so it is named after what it configures.
    /// <para>
    /// <c>General</c>, not <c>Function</c>: Weasel 9 made the usage explicit because a function's
    /// name has to be quoted differently from a relation's, and the two-argument constructor it
    /// replaced is <c>[Obsolete]</c>. <c>General</c> is what that constructor did, so the name this
    /// produces is byte-for-byte what it produced before.
    /// </para>
    /// </summary>
    public DbObjectName Identifier { get; } =
        new PostgresqlObjectName(table.Schema, $"{table.Name}_{Parameter}", SchemaUtils.IdentifierUsage.General);

    public void WriteCreateStatement(Migrator migrator, TextWriter writer)
        => writer.WriteLine($"ALTER TABLE {table.QualifiedName} SET ({Parameter} = {ToastTupleTarget});");

    public void WriteDropStatement(Migrator migrator, TextWriter writer)
        => writer.WriteLine($"ALTER TABLE {table.QualifiedName} RESET ({Parameter});");

    public void ConfigureQueryCommand(Weasel.Core.DbCommandBuilder builder)
        => builder.Append(
            "select count(*) from pg_class c "
            + $"where c.oid = to_regclass('{table.QualifiedName}') "
            + $"and c.reloptions @> array['{Parameter}={ToastTupleTarget}'];");

    /// <summary>
    /// None once the parameter is set, and Create while it is not — which includes the first run
    /// against a database where the table does not exist yet, because
    /// <see cref="DependentTypes"/> has the <c>ALTER TABLE</c> written after the <c>CREATE TABLE</c>.
    /// </summary>
    public async Task<ISchemaObjectDelta> CreateDeltaAsync(DbDataReader reader, CancellationToken ct = default)
    {
        var set = await reader.ReadAsync(ct) && reader.GetInt64(0) == 1;
        return new SchemaObjectDelta(this, set ? SchemaPatchDifference.None : SchemaPatchDifference.Create);
    }

    public IEnumerable<DbObjectName> AllNames() => [Identifier];

    ISchemaObject[] IFeatureSchema.Objects => [this];

    string IFeatureSchema.Identifier => Identifier.QualifiedName;

    Migrator IFeatureSchema.Migrator => new PostgresqlMigrator();

    Type IFeatureSchema.StorageType => typeof(EntryRowFitsInline);

    void IFeatureSchema.WritePermissions(Migrator migrator, TextWriter writer)
    {
    }

    /// <summary>The entry table has to exist before its storage parameter can be set.</summary>
    public IEnumerable<Type> DependentTypes() => [typeof(Entry)];
}
