using System.Data.Common;

using TakeInitiative.KnowledgeBase.Schema;

using Weasel.Core;
using Weasel.Core.Migrations;
using Weasel.Postgresql;

namespace TakeInitiative.Api.Features.Reference.KnowledgeBase;

/// <summary>
/// The <c>knowledge_base_item</c> table, its generated <c>tsvector</c> column and its three indexes
/// (26c), as a Weasel schema object so that <c>ApplyAllDatabaseChangesOnStartup</c> creates them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API owns this table and the CLI does not.</b> The ingest CLI writes rows and nothing else;
/// pointed at a database the API has never started against, it stops with "start the API once"
/// rather than running DDL of its own. One owner means an operator can never end up with a table in
/// a shape Marten would then try to migrate. The DDL itself lives in the package
/// (<see cref="KnowledgeBaseSchema" />) because three callers have to agree on the column names: this
/// one creates them, the store writes them, and the CLI checks for them.
/// </para>
/// <para>
/// <b>Why a hand-written <see cref="ISchemaObject" /> and not a Weasel <c>Table</c>.</b> Weasel 7.11
/// models a table well enough to diff it, but not the two things this schema needs: a column's
/// <c>GENERATED ALWAYS AS … STORED</c> clause — which <c>SearchSchema</c> reaches by
/// smuggling it through a <c>ColumnCheck</c>, a trick that works because a check is not part of a
/// column's identity — and an index over an expression with an operator class,
/// <c>using gin (lower(name) gin_trgm_ops)</c>, which <c>IndexDefinition</c> cannot express at all
/// (it has <c>Mask</c> and <c>CustomMethod</c>, and no notion of an opclass). A <c>Table</c> that did
/// not know about the trigram index would also be a standing risk of Weasel deciding to drop it. So
/// this follows <c>EntryRowFitsInline</c>'s precedent instead: the object writes its own
/// idempotent SQL, and answers the schema diff by counting what exists.
/// </para>
/// <para>
/// <b>What that costs.</b> There is no column-level migration: the delta is "all four relations are
/// there" or "create them", and every statement is <c>if not exists</c>. Adding a column to
/// <see cref="KnowledgeBaseSchema" /> later therefore needs an explicit <c>alter table</c> rather
/// than falling out of a diff. That is an acceptable trade for a table nobody edits and that is
/// rebuildable from the source folder in one command — and the alternative, Weasel dropping and
/// recreating a GIN index over every row on every boot, is the failure <c>SearchSchemaTests</c> was
/// written to prevent.
/// </para>
/// <para>
/// <b>It depends on <c>pg_trgm</c></b>, for <c>gin_trgm_ops</c>. <c>Bootstrap</c> registers that
/// extension as an extended schema object before this one and Weasel writes them in the order they
/// were added, so it exists by the time this runs. If that ever stopped being true, startup would
/// fail loudly on "operator class gin_trgm_ops does not exist" rather than quietly leaving the index
/// off — and <c>SchemaOnStartupTests</c> asserts the index is there after a Production start.
/// </para>
/// </remarks>
/// <param name="databaseSchema">
/// The Postgres schema Marten is configured with. A configured constant, never input, which is why
/// it is written into the statements rather than parameterised — Postgres takes no parameter in DDL
/// or in a <c>pg_namespace</c> name here.
/// </param>
public sealed class KnowledgeBaseTable(string databaseSchema) : ISchemaObject
{
    /// <summary>The table, which is what a schema diff names this object by.</summary>
    public DbObjectName Identifier { get; } =
        new PostgresqlObjectName(databaseSchema, KnowledgeBaseSchema.TableName);

    public void WriteCreateStatement(Migrator migrator, TextWriter writer) =>
        writer.WriteLine(KnowledgeBaseSchema.CreateSql(databaseSchema));

    public void WriteDropStatement(Migrator migrator, TextWriter writer) =>
        writer.WriteLine(KnowledgeBaseSchema.DropSql(databaseSchema));

    public void ConfigureQueryCommand(Weasel.Core.DbCommandBuilder builder) =>
        builder.Append(KnowledgeBaseSchema.ExistsSql(databaseSchema));

    /// <summary>
    /// None once every relation is there, Create while any of them is missing. Counting rather than
    /// comparing definitions is what keeps a start against an unchanged database a no-op: there is
    /// nothing here for Weasel to decide has drifted.
    /// </summary>
    public async Task<ISchemaObjectDelta> CreateDeltaAsync(DbDataReader reader, CancellationToken ct = default)
    {
        var present = await reader.ReadAsync(ct) ? reader.GetInt64(0) : 0L;
        return new SchemaObjectDelta(
            this,
            present == KnowledgeBaseSchema.ObjectNames.Count
                ? SchemaPatchDifference.None
                : SchemaPatchDifference.Create);
    }

    /// <summary>
    /// The table alone. The indexes live inside it and are created and dropped with it, so naming
    /// them here would have Weasel looking for objects it is not being asked to manage separately.
    /// </summary>
    public IEnumerable<DbObjectName> AllNames() => [Identifier];
}
