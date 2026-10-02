# 30 — Marten 7 → 9

## Goal

The API runs on **Marten 9** with Marten 9's defaults, on a database created by Marten 9, with
`0 Warning(s), 0 Error(s)` in `Release` and all tests green. Nothing else moves: still
FastEndpoints, still the same endpoints, same OpenAPI document, same `schema.d.ts`.

This step exists **before the review and before step 29** for one reason: there is no production
database yet. Marten 9 migrates `mt_version` from `integer` to `bigint` and moves event sequences
to `bigint`. Against empty tables that is a schema definition. Against a friend's campaign it is a
data migration. The window where this is free closes at 29g and never reopens.

"Running" at the end: `pnpm dev` from a **destroyed and recreated** database → sign up → create a
campaign → create an entry → rename it → `@mention` it in a session note → ⌘K finds it by a fuzzy
match → start a combat → roll initiative. Then restart the API and `SearchSchemaTests` still
asserts that a second start has nothing left to do.

The step ships as three PRs stacked on `dev`, starting with this file's docs PR. Each PR leaves the
repo runnable.

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 30a | `v2/30a-marten-9-plan` | This plan | Docs only || [ ] |
| 30b | `v2/30b-marten-9` | Marten 9.45.0, Weasel 9.37.0, Npgsql 9.0.4, Marten 9's defaults, fresh database | The API builds and all tests pass on Marten 9. `schema.d.ts` is byte-identical || [ ] |
| 30c | `v2/30c-marten-9-pins` | The `TakeInitiative.KnowledgeBase` Npgsql pin and the audit-suppression comments | No pin or suppression survives whose stated reason has expired || [ ] |

## Depends on

- **28**, which is done. Nothing in 26–28 is at risk from this beyond the KB table (below).
- Not blocked by anything. This is the first thing after 28.

**There is no backwards-compatibility obligation.** No stored events, documents or dev databases
are preserved. The roadmap already took this stance for stored roll expressions in Stage 2; it
applies in full here. Anything that wants to break, breaks now, because after 29g it cannot.

## Files touched

| Path | Why |
|---|---|
| `apps/TakeInitiative.Api/TakeInitiative.Api.csproj` | `Marten` 7.31.1 → 9.x; the audit-suppression comment names 7.31.1 |
| `packages/TakeInitiative.KnowledgeBase/TakeInitiative.KnowledgeBase.csproj` | `Npgsql` 8.0.3 → 9.x, and the comment that justifies the pin by naming Marten 7.31.1 |
| `apps/TakeInitiative.Api/src/boostrap/Bootstrap.cs` | `AddMartenDB`: defaults, `UseLightweightSessions`, `ApplyAllDatabaseChangesOnStartup` |
| `apps/TakeInitiative.Api/src/Features/Search/Sql/SearchSchema.cs` | `ExtendedSchemaObjects` / `IFeatureSchema` against the new Weasel |
| `apps/TakeInitiative.Api/src/Features/Reference/KnowledgeBase/KnowledgeBaseTable.cs` | Same, and it writes its own SQL |
| `apps/TakeInitiative.Api/src/Features/*/Models/{Campaign,Session,SessionNote,Entry,Combat}.cs` | `partial` for the source generator, if required |
| `apps/TakeInitiative.Api.Tests/**` | Any sync data access, and the session helpers in `Scopes/Integration` |

## Steps

### 30b — the upgrade (`v2/30b-marten-9`)

The package bump and everything that must move with it. These cannot be separated: `SearchSchema`
and `KnowledgeBaseTable` are compile-time dependencies on Weasel, so the branch does not compile
until they are done.

1. `Marten` 7.31.1 → **9.45.0** in the API csproj. It pulls `JasperFx` / `JasperFx.Events` 2.79.1
   and `Weasel.Postgresql` / `Weasel.Storage` 9.37.0, and `Npgsql` **9.0.4** arrives transitively
   through Weasel rather than from Marten directly. Add **`Marten.SourceGenerator` 9.45.0**: it is a
   separate package and Marten 9.45.0 does not depend on it.
2. Build and work the compiler. Expected, from the migration guide:
   - **Runtime codegen is gone**, replaced by source generators. Mark the five snapshotted
     aggregates `partial` if the generator asks for it. Source-generator diagnostics are build
     failures here — CI passes `-p:TreatWarningsAsErrors=True`.
   - **`OperationRole` and `BulkInsertMode` moved to `Weasel.Core`.**
   - **Renames**: `ProjectionName` → `Name`, `LastModifiedBy` → `CurrentUserName`,
     `EventSlice<T>.Aggregate` → `Snapshot`.
   - **Sync data access throws `NotSupportedException`**, and sync LINQ operators are gone. A grep
     found no `Query<T>().ToList()`/`.First()`, but 49 sites match `session.Load<`/`session.Query<`
     without `Async` and were not read individually. Confirm, don't assume.
3. **Take Marten 9's defaults.** Do *not* call `RestoreV8Defaults()`. The staged-defaults path is
   for an upgrade that has data to protect; this one does not. Three of the defaults are already
   the configuration, so they are no-ops:
   - `UseSystemTextJsonForSerialization(EnumStorage.AsString)` — 9 makes STJ the default anyway
   - `UseLightweightSessions()` — 9 makes the injected session lightweight anyway
   - `Apply`/`Create` are already `public` methods on the aggregate, not registration lambdas
4. `ExtendedSchemaObjects` against the new Weasel: the two `Extension` objects (`pg_trgm`,
   `unaccent`), `KnowledgeBaseTable`, and `SearchSchema`. **Order still matters** — the KB table's
   trigram index needs `gin_trgm_ops`, and Weasel writes extended schema objects in the order they
   were added, so `pg_trgm` stays first.
5. **Destroy the database rather than migrate it**:
   `docker compose -f compose.dev.yml down -v`, then `pnpm dev`. `CreateOrUpdate` never drops a
   table, so a database created under Marten 7 keeps the `mt_streams.snapshot` columns that Marten
   9 omits from new databases, and `mt_version` stays `integer`. A fresh database gets the clean
   9 shape and there is nothing to reconcile.
   > This also destroys `takeminio-data`, so local images go with it. Nothing else does.
6. Fix the tests. 666 of them across 123 files is the safety net that replaces a staged rollout.

### 30c — dependency hygiene (`v2/30c-marten-9-pins`)

Separate because it is comment-and-pin work with no behaviour in it, and it is easier to review
when the upgrade is already green.

1. `packages/TakeInitiative.KnowledgeBase`: `Npgsql` 8.0.3 → 9.x, and rewrite the comment. It
   currently justifies the pin as "the version Marten 7.31.1 already pulls into the API" — the
   version is wrong after 30a and the sentence is the whole point of the pin.
2. The API csproj's audit-suppression comment names `Marten 7.31.1 dragged in OpenTelemetry.Api
   1.8.0 (GHSA-g94r-2vxg-569j)`. **Marten 9.45.0 declares no `OpenTelemetry.Api` dependency at
   all** (checked against the published nuspec), so that half of the pin has no reason left:
   delete the `OpenTelemetry.Api` pin and the sentence that justified it. The `JwtBearer` half is
   unrelated to this step and stays. CLAUDE.md requires a suppression to carry the reasoning that
   justifies it, and a stale reason is worse than none.
3. `dotnet restore` with the audit on, and confirm no new advisories.

## Verify

```bash
# From the repo root, with the database destroyed first
docker compose -f compose.dev.yml down -v

dotnet restore
dotnet build --configuration Release --no-restore -p:TreatWarningsAsErrors=True   # 0 Warning(s), 0 Error(s)
dotnet test -- --verbosity normal

# **.cs changed, so the web pipeline runs too
pnpm install --frozen-lockfile
pnpm turbo run gen:api --filter=@ti/web
git diff --exit-code -- apps/TakeInitiative.Web/utils/api/schema.d.ts   # must be clean: no API surface changed
pnpm --filter @ti/web exec nuxi typecheck
pnpm --filter @ti/web test
```

Then by hand, on the fresh database: sign up, create a campaign, create an entry, rename it,
`@mention` it in a session note, find it in ⌘K by a misspelling, start a combat, roll initiative.

Two specific assertions beyond the suites:

- **`SearchSchemaTests` must still pass on a second start.** It asserts that a restart has no DDL
  left to do, and it is what keeps a GIN index over every note from being rebuilt on every boot.
  If Marten 9's generated schema does not converge to what `CreateOrUpdate` produces, this is the
  test that says so. Treat a failure here as the headline finding of the step, not a flaky test.
- **`psql` the new database**: `mt_version` is `bigint`, and the event sequence is `bigint`.

## Notes / gotchas

- **Postgres is fine.** Marten 8 requires PG 13+ and drops 12; `compose.dev.yml` runs
  `postgres:15-alpine`. No image change, and the comment pinning 15 so dev and test share a major
  still holds.
- **`net10.0` is fine.** Marten 9 targets .NET 9 and 10. Marten 8 dropped .NET 6/7, which this
  repo left in step 03.
- **`AppendMode` default flips `Rich` → `QuickWithServerTimestamps`.** This is the change most
  likely to produce a subtle wrong answer rather than a compile error. Every inline projection
  reads event metadata directly — `Apply(IEvent<EntryRenamed> e) => this with { UpdatedAt = e.Timestamp }`
  — and the README already records a flaky test that "compared Postgres microseconds with .NET
  ticks". The default change moves where that timestamp comes from, on precisely that seam. If
  `UpdatedAt`, `CreatedAt` or session-note ordering drifts, look here first.
- **Other default flips to know about**: `EnableAdvancedAsyncTracking` false → true,
  `UseIdentityMapForAggregates` false → true, `DisableNpgsqlLogging` false → true, and
  `EnableBigIntEvents` on by default.
- **Provenance must survive.** `MetadataConfig.CorrelationIdEnabled`, `CausationIdEnabled` and
  `HeadersEnabled` are invariant 9, filled in by `CorrelationMiddleware`. Confirm the three are
  still populated after the upgrade; they are configuration, not code, so nothing will fail to
  compile if a name moved.
- **`partial` on the aggregates is an inference**, from "runtime code generation removed, replaced
  with source generators". The migration guide states it for projection classes marked `partial`;
  this repo owns no projection classes, only self-aggregating snapshots. The compiler settles it.
- **What this step deliberately does not do**: touch FastEndpoints, add Wolverine, change any
  route, request, response or the OpenAPI document. `schema.d.ts` must come back byte-identical.
  That is what makes this step's diff readable, and it is why step 31 is a separate step.
