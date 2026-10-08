# 26 — Knowledge base

## Goal

The reference material gets a **place in the app**. A **Knowledge base** page lists what
has been ingested — monsters, spells and items — filterable by category and source book,
searchable, paginated, and every row links out to 5etools. ⌘K's REFERENCE section stops
being the only way to reach it, and gains a "Browse all" row that lands here.

Underneath, the 5eTools index moves **out of a file read at startup and into Postgres**,
with a `tsvector` for search and a stable primary key that survives a re-ingest. It gets
there through a new **.NET CLI**: one command that reads a 5eTools data folder, parses it,
diffs it against what is already stored, and upserts. The Node preprocessing script from
21a is ported into it and deleted.

**The app still never displays 5eTools' content.** A row shows name, category, source book
and page, a short label (CR, spell level, item rarity), and — new in 26g — the item's
artwork by URL from their CDN, never copied into our own store. There is no rules text, no
description and no stat block. The row's whole purpose is to be *findable*, *linkable*
(step 27) and to link out.

"Running" at the end: on a dev machine, `dotnet run --project apps/TakeInitiative.KnowledgeBase.Cli
-- ingest --from ~/5etools-src/data` prints the report below — `new 2,847 · updated 0 ·
unchanged 0 · missing 0` — and writes them to Postgres. A DM opens **Wiki ▸ Knowledge base** on a phone, filters to
Monsters, types "behol", taps the Beholder row and lands on 5etools in a new tab. ⌘K for
"beholder" behaves exactly as it did after step 21, but the rows now come from the
database. Running the ingest a second time reports every row unchanged and writes nothing. Pointing it at a folder with one bestiary file in it prints a warning that
2,800 rows are missing from the source and **deletes none of them**.

The step ships as seven PRs stacked with `gh stack` on top of this file's docs PR. Each PR
leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 26a | `v2/26a-knowledge-base-plan` | This plan | Docs only | [x] |
| 26b | `v2/26b-kb-parser` | `packages/TakeInitiative.KnowledgeBase`: the ported parser and the row model | 25's app unchanged, and nothing references the package yet. Its tests reproduce the Node script's output from the same fixtures, byte for byte | [x] built; 66 tests |
| 26c | `v2/26c-kb-cli` | The schema, the upsert and `apps/TakeInitiative.KnowledgeBase.Cli` | The same. `ingest --dry-run` reports a diff; `ingest` upserts into Postgres. Nothing reads the table yet | [x] built; 90 tests |
| 26d₁ | `v2/26d1-reference-async` | `IReferenceProvider` goes async | No behaviour change at all. The SRD provider wraps its in-memory lookups; every call site awaits. Independent of 26b, so it can land first | [x] built |
| 26d₂ | `v2/26d2-kb-provider` | The 5eTools provider reads Postgres | ⌘K behaves as it did after 21c, with its 5eTools rows served from the database. `FiveEToolsIndex`, its options and `scripts/5etools/` are gone | [x] built |
| 26e | `v2/26e-kb-api` | The browse API | `GET knowledge-base` lists, filters and pages. **No second per-item route**: `GET reference/{provider}/{itemId}` has answered that since step 20 and already returns the summary with `statBlock: null` for a search-only provider | [x] built |
| 26f | `v2/26f-kb-page` | The Knowledge base page | Wiki ▸ Knowledge base browses, filters and searches on a phone and on a desktop. ⌘K gains "Browse all". The step's Verify passes | [x] built |
| 26g | `v2/26g-kb-images` | The artwork slot | A row shows the item's 5eTools artwork where one exists, with a glyph fallback, loaded from their CDN and never stored | [x] built |

Step 27 turns `Entry.Links` into a real feature and links entries to these rows. Step 28
suggests those links. Neither is in 26.

## Depends on

- **20**: `IReferenceProvider`, `ReferenceCatalog`, `ReferenceSummary`, `ReferenceMatch`,
  `ReferenceCategory`, `ReferenceMatcher`, `ReferenceSearchProvider`, and + Wiki
  (`PostEntryFromReference`). 26d changes the interface; the SRD provider must keep working
  unchanged in behaviour.
- **21**: `scripts/5etools/build-5etools-index.mjs` (438 lines) and its tests (231 lines)
  are the **specification** for 26b's parser, and `scripts/5etools/fixture/` is its test
  corpus. `FiveEToolsCatalog`, `FiveEToolsIndex`, `FiveEToolsOptions` and
  `FiveEToolsReferenceProvider` are replaced.
- **17**: `SearchSql` and `SearchSchema` — how this repo already builds `tsvector` columns,
  generated columns and GIN indexes through Marten's `ExtendedSchemaObjects`. 26c follows
  that pattern rather than inventing one.
- **09**: `packages/TakeInitiative.Dice` and `packages/TakeInitiative.Dice.Tests` are the
  layout precedent for a package plus its own test project, and for how a package is
  registered in `TakeInitiative.sln`.

[12-v2-design-session.md](12-v2-design-session.md) §11 is binding, with one clarification
this step records: §11 said the index "is built locally per deployment and is not
committed", which stays true — it is now built locally and *ingested*, not read from a file
at runtime. §11's "The app never displays 5eTools content" is unchanged and binding.

## Why the index moves into Postgres

Not for speed. The in-memory index from 21b is fast enough, and `ReferenceMatcher` already
ports `pg_trgm`'s `word_similarity` into C# specifically to avoid needing the database.

It moves because of what step 21 got wrong: **reference rows had no location**. They existed
only inside ⌘K's REFERENCE section, which means:

- You cannot see what is in the corpus, or whether an ingest worked, without guessing search
  terms at it.
- Step 27 is about to let a user attach a link to one of these rows. A link that points at
  something the app has no page for is a dead end.
- `IndexPath` being unset made the whole provider disappear silently. There was no way for
  the app to say "nothing has been ingested yet" because nothing in the UI was ever
  responsible for the corpus.

Once the corpus needs a browsable, filterable, paginated page, holding it in a C# list and
paging that list in memory is the wrong tool, and `SearchSql` already establishes how this
repo does text search. So the database follows from the surface, not the other way round.

### It is deliberately not event-sourced

Every other read model in this app is an inline Marten projection off an aggregate stream.
The knowledge base is **a flat table, written by a CLI, with no events and no aggregate**.
That is intentional, and an architecture review should read it as a decision rather than an
inconsistency:

- Nobody edits these rows. There is no user intent to record, so there is nothing for an
  event to mean. `Actor` would be "the person who ran the ingest", which the batch stamp
  already records.
- It is **global**, not per-campaign. It has no `CampaignId` and no visibility rules,
  because reference content is the same for every campaign and every member — which
  `IReferenceProvider` already states ("nothing here takes a viewer").
- It is **rebuildable from the source folder**, so it needs no history of its own. The
  authoritative copy is the 5eTools data on the operator's machine.
- Links *to* it (step 27) are event-sourced, on the Entry stream, where the user intent
  actually lives.

## The CLI

One command, in `apps/TakeInitiative.KnowledgeBase.Cli`, using `System.CommandLine`.

```
dotnet run --project apps/TakeInitiative.KnowledgeBase.Cli -- ingest \
    --from ~/5etools-src/data \
    [--connection "Host=…"] [--dry-run] [--prune] [--force] [--provider 5etools]
```

Configuration comes from `appsettings.json` plus environment plus flags, flags winning, so
a plain `ingest` works on a dev machine with no arguments:

| Setting | Env | Flag | Default |
|---|---|---|---|
| `KnowledgeBase:Source:Path` | `KnowledgeBase__Source__Path` | `--from` | none; required |
| `KnowledgeBase:ConnectionString` | `KnowledgeBase__ConnectionString` | `--connection` | the same dev connection string the API uses |
| `KnowledgeBase:Prune` | — | `--prune` | `false` |

**Production is the same command with `--connection`**, run from the operator's machine
against the production database over an SSH tunnel. The operator is the person who has the
5eTools data; they push it. No image carries the corpus, nothing is fetched at runtime, and
step 29 does not need the CLI in the deployment at all.

### The diff, and why prune is opt-in

Every run reports, before it writes:

```
5etools · ~/5etools-src/data
  parsed   2,847 items  (2,103 monsters · 618 spells · 126 items)
  new         12
  updated      3
  unchanged 2,832
  missing      0   (in the database, absent from this source)
```

- **`--dry-run`** prints that and writes nothing.
- **A plain run is additive**: it inserts and updates, and **never deletes**. This is the
  important one. Point the CLI at a partial folder — one bestiary file instead of thirty,
  or a half-finished download — and a delete-missing pass would silently remove rows that
  users have linked entries to. Additive-by-default makes the worst outcome "some rows are
  stale", not "links broke".
- **`--prune`** is what deletes, and even then:
  - a row that **any entry links to** (step 27) is never deleted. It is marked
    `stale = true`, and the entry's link renders with "this link's source is no longer in
    your data". A link is something a user made; the ingest does not get to erase it.
  - a prune that would remove **more than 20% of a provider's rows** refuses without
    `--force`. Mass deletion is the signature of pointing the tool at the wrong folder, and
    that is exactly the moment to need a second keystroke.
- Exit codes: `0` nothing to do or done, `1` a parse failure (nothing written), `2` a prune
  refused by the threshold.

### Determinism matters more than it looks

Rows are compared by a content hash so `updated` means something. That only works if the
parser's output is byte-stable: fixed property order, invariant number formatting, sorted
arrays, `\n` newlines. If it is not, every run reports every row as updated, the diff
becomes noise, and the safety rails above stop being readable. 26b's golden-file test is
what holds this.

## The schema

One table per the whole knowledge base, not one per provider, because the browse page and
⌘K both want to query across providers.

```sql
create table knowledge_base_item (
    provider      text    not null,           -- '5etools'
    id            text    not null,           -- 'monster_beholder_mm', from the parser
    name          text    not null,
    category      text    not null,           -- Monster | Spell | Item
    source_book   text    not null,           -- 'MM'
    source_title  text,                       -- 'Monster Manual (2014)'
    page          int,
    label         text,                       -- the muted row line: 'CR 13 · Large Aberration'
    url           text    not null,           -- the deep link out to 5etools
    image_url     text,                       -- 26g; null where the item has no artwork
    stats         jsonb,                      -- monsters only: { hp, ac, initiativeBonus }
    content_hash  text    not null,
    batch         uuid    not null,           -- which ingest run last wrote this row
    stale         boolean not null default false,
    ingested_at   timestamptz not null,
    primary key (provider, id)
);
```

- **There is no separate `detail` column.** An earlier draft of this table had both a short
  `label` ("CR 13") and a longer `detail` ("CR 13 · Large Aberration"). The parser emits
  exactly one string — `KnowledgeBaseItem.Label`, nullable — and it already *is* the muted
  row line. There is no second, shorter string anywhere in the allowlist, so `detail` could
  only ever have held a copy. 26e and 26f read `label` for the row's muted line.
- **The primary key is the parser's id**, not a generated one. `build-5etools-index.mjs:90`
  already forms `monster_beholder_mm` from category, name and source, and fails on a
  duplicate at line 327. That is what makes a link durable across re-ingests, and it is the
  single most important thing the port must preserve.
- **Search** gets a stored generated `tsvector` column over `name`, plus a `gin_trgm_ops`
  index on `lower(name)`. **The two indexes cannot be the only filter**, which an earlier draft
  of this step assumed:
  - `pg_trgm`'s index-backed `<%` reads `pg_trgm.word_similarity_threshold` (0.6), not the
    app's `EntryMatchOptions.Default.MinSimilarity` (0.5), so relying on it would silently drop
    matches the SRD provider finds. The fuzzy rung's floor is a property of the app, not of a
    database session.
  - The index is on `lower(name)`; the ladder folds with `lower(unaccent(name))`, and
    `unaccent` is not immutable, so no index can hold it. Prefiltering on `lower(name)` alone
    loses "uber" → "Überwald", which 21b's in-memory provider handled.

  So the query is a four-term disjunction that is a **superset** of what the ladder then
  matches: the tsvector index, the trigram index, the fuzzy rung above the app's own floor, and
  any name the fold changes. The first two serve the common cases; rung 4 and the accent case
  are scored over the rows that come back, exactly as `EntryMatcher` already does for entry
  names.

  **Follow-up, deliberately not done here:** a stored `lower(unaccent(name))` column written by
  the ingest and indexed with `gin_trgm_ops` collapses those four terms into one index scan. It
  is a change to `KnowledgeBaseSchema` and `KnowledgeBaseStore`, which 26c settled, and the
  corpus is small enough that it does not matter yet. It is the right fix if browse or ⌘K ever
  feels slow on a full 5eTools ingest.
- **Indexes**: the `tsvector`, the trigram index, and a plain `(provider, category,
  source_book)` for the browse page's filters.
- Created through the same `ExtendedSchemaObjects` path the search schema uses, so the API
  owns it and the CLI does not own migrations. That path did not run in production; 26c fixed
  it — see "The schema is not applied in production" below.
- **It is a bespoke `ISchemaObject`, following `EntryRowFitsInline` rather than
  `AddEntryArticleVector`.** `SearchSchema` holds two different patterns and only the second
  applies here. `AddEntryArticleVector` smuggles a `GENERATED ALWAYS AS … STORED` clause
  through a `ColumnCheck` on a table *Weasel already builds from a document mapping*;
  `knowledge_base_item` is not a document table. It also needs
  `using gin (lower(name) gin_trgm_ops)`, and Weasel.Postgresql 7.11.7's `IndexDefinition`
  has no notion of an operator class (`NgramIndex` is not available at this Marten version).
  A Weasel `Table` that did not know about the trigram index would also stand a chance of
  dropping it. So the table is written as idempotent `create … if not exists` SQL whose delta
  is answered by counting its four relations. The cost, documented on the class: no
  column-level migration, so a later column needs an explicit `alter table` rather than
  falling out of a diff. In exchange, a start against an unchanged database is a provable
  no-op, which `SearchSchemaTests` already asserts.

### The schema is not applied in production

Found while planning step 29, and verified: `Bootstrap.cs:136` guards
`ApplyAllDatabaseChangesOnStartup()` at line 143 with `if (IsDevelopment)`, and the API's
`dockerfile` sets no `ASPNETCORE_ENVIRONMENT`, so a container runs as Production and never
applies it. `pg_trgm` and `unaccent` are registered as `Storage.ExtendedSchemaObjects`
(lines 39–40), which hang off no document type, so Marten's lazy per-document auto-creation
has no reason to create them either.

Two consequences, one of them already live:

1. **⌘K's fuzzy matching would fail in production today**, because `word_similarity()` needs
   `pg_trgm`. This is a pre-existing bug in step 17, not something 26 introduces. Nobody has
   hit it because nothing is deployed yet.
2. **This step's table would not exist in production**, so the ingest CLI would fail there
   forever while working perfectly in dev.

So **26c fixes it**, rather than leaving it to step 29. A step that adds a schema object is
the right place to make sure schema objects are actually created, and it keeps "the API owns
the schema, the CLI does not" true in every environment instead of only in dev:

- A `Marten:ApplySchemaOnStartup` setting, defaulting to `true`, replaces the
  `IsDevelopment` check.
- Keep the existing comment and extend it with the production reasoning **and the
  single-replica constraint**: `AddAsyncDaemon(DaemonMode.Solo)` already means the API must
  never run more than one replica, which is also what makes DDL on startup safe.
- Tests: the flag off leaves the schema alone, the flag on applies it, and the integration
  fixtures are unaffected.
- **Measure before and after.** Run the API with `ASPNETCORE_ENVIRONMENT=Production`
  against a freshly reset database and record `select extname from pg_extension` in the PR.
  It is worth having the evidence written down, because this is the kind of bug that gets
  "fixed" twice.

Step 29 keeps the rest of its production-readiness work (persisted data-protection keys,
forwarded headers) and drops the schema half.

## `IReferenceProvider` becomes async

`Search`, `Get` and `Find` are synchronous today, and the interface's own comment says "In
memory." A database-backed provider cannot honour that. 26d changes the three members to
return `Task<…>` and takes a `CancellationToken`. The call sites are few and known:

- `ReferenceCatalog.GetItem` / `FindItem`
- `ReferenceSearchProvider` (line 32, inside a `SelectMany` that has to become a gather)
- `PostEntryFromReference` (lines 66–67, 95)
- `GetEntry/EntryResponse` (lines 89–90)
- `SrdReferenceProvider` wraps its in-memory list in completed tasks and keeps behaving
  identically.

`ReferenceSearchProvider`'s current `SelectMany` over providers, ordered by registration,
becomes an awaited gather that preserves that order — SRD before 5eTools, as step 20
established.

## The Knowledge base page

**It is not a fourth bottom tab.** The tabs are Campaign · Wiki · Combat (design §3a) and
a fourth one costs a third of the wiki's touch target for something used rarely. It lives
at `/app/campaigns/{id}/knowledge-base`, reached from:

- the Wiki home's toolbar, next to search;
- ⌘K's REFERENCE heading, which gains a final **"Browse all reference material →"** row;
- an entry's knowledge-base link (step 27).

### Phone

```
┌──────────────────────────────┐
│ ← Knowledge base             │
│ ┌──────────────────────────┐ │
│ │ 🔍 behol                 │ │
│ └──────────────────────────┘ │
│ ‹ All  Monsters  Spells  It… │  ← category chips, scroll sideways
│ ‹ All books  MM  XGE  TCE  … │  ← source chips
│                              │
│ 2,847 items · 5eTools        │
│ ┌──────────────────────────┐ │
│ │ 🖼  Beholder           ↗ │ │
│ │    Monster · CR 13 · MM  │ │
│ │    p. 28                 │ │
│ └──────────────────────────┘ │
│ ┌──────────────────────────┐ │
│ │ 🖼  Beholder Zombie    ↗ │ │
│ │    Monster · CR 5 · MM   │ │
│ └──────────────────────────┘ │
│        [ Load more ]         │
└──────────────────────────────┘
```

A tap on a row **opens 5etools in a new tab** — the row is the whole interaction. There is
no in-app detail page for a 5eTools item, because there is nothing we are allowed to put on
it. (An SRD item, which we *may* show, keeps step 20c's stat-block card.)

### Empty, and not-ingested

Two different states, and the difference is the point of the page:

- **Nothing ingested**: "No reference material yet. An operator ingests it with the
  knowledge-base CLI." Not an error, and no mention of a missing file path.
- **Nothing matches the filters**: "No items match. Clear filters."

This is what 21's silent `IndexPath` could never say.

### Desktop (`lg+`)

The same list in the capped main column, with the category and source filters as a sticky
left rail instead of chip rows, and a count per filter value.

## Files touched

New:

- `packages/TakeInitiative.KnowledgeBase/` — `TakeInitiative.KnowledgeBase.csproj`,
  `FiveETools/` (the ported parser: `FiveEToolsParser.cs`, `Bestiary.cs`, `Spells.cs`,
  `Items.cs`, `Books.cs`, `Copy.cs` for `_copy` inheritance, `Ids.cs`, `Labels.cs`),
  `KnowledgeBaseItem.cs`, `Schema/KnowledgeBaseSchema.cs`, `Store/KnowledgeBaseStore.cs`
  (upsert, diff, prune), `Store/IngestReport.cs`
- `packages/TakeInitiative.KnowledgeBase.Tests/` — the golden-file test, the ported cases
  from `build-5etools-index.test.mjs`, determinism tests, and store tests against
  Testcontainers Postgres (match `apps/TakeInitiative.Api.Tests/Scopes/Integration/*Fixture.cs`
  for the pinned image)
- `apps/TakeInitiative.KnowledgeBase.Cli/` — `Program.cs`, `IngestCommand.cs`,
  `appsettings.json`
- `apps/TakeInitiative.Api/src/Features/Reference/KnowledgeBase/` —
  `KnowledgeBaseReferenceProvider.cs`, `KnowledgeBaseQueries.cs`
- `apps/TakeInitiative.Api/src/Features/Reference/Api/GetKnowledgeBase/` — the browse
  endpoint, its request and response
- `apps/TakeInitiative.Web/pages/app/campaigns/[campaignId]/knowledge-base.vue`
- `apps/TakeInitiative.Web/components/KnowledgeBase/` — `KnowledgeBaseList.vue`,
  `KnowledgeBaseRow.vue`, `KnowledgeBaseFilters.vue`, `KnowledgeBaseEmpty.vue`
- `apps/TakeInitiative.Web/composables/useKnowledgeBase.ts`

Modified:

- `TakeInitiative.sln` — two new projects, under `packages` and `apps`
- `apps/TakeInitiative.Api/src/Features/Reference/IReferenceProvider.cs` — async
- `…/Reference/ReferenceCatalog.cs`, `…/Search/Providers/ReferenceSearchProvider.cs`,
  `…/Entries/Api/PostEntryFromReference/PostEntryFromReference.cs`,
  `…/Entries/Api/GetEntry/EntryResponse.cs`, `…/Reference/Srd/SrdReferenceProvider.cs` — the
  async call sites
- `apps/TakeInitiative.Api/src/boostrap/Bootstrap.cs` — register the new provider and the
  schema objects; drop `FiveEToolsOptions`
- `apps/TakeInitiative.Web/components/Search/*` — the "Browse all" row under REFERENCE
- `apps/TakeInitiative.Web/pages/app/campaigns/[campaignId]/wiki/index.vue` — the toolbar link
- `apps/TakeInitiative.Web/utils/api/schema.d.ts` — regenerated, not hand-edited
- `README.md` / `docs/roadmap/README.md` — the CLI's usage and the step's status

Deleted:

- `scripts/5etools/` — the whole folder, in 26d₂, once 26b's golden test proves the port.
  Safe because 26b vendored `fixture/` into the test project
- `apps/TakeInitiative.Api/src/Features/Reference/FiveETools/FiveEToolsIndex.cs`,
  `FiveEToolsOptions.cs`, `FiveEToolsCatalog.cs`, `FiveEToolsReferenceProvider.cs`
- ~~`apps/TakeInitiative.Api.Tests/Fixtures/5etools-index.json`~~ — **kept.** It is the only
  corpus in the repository holding a name that collides with an SRD monster (`Goblin Boss`),
  and that collision is the entire basis of step 20's "SRD ranks first at the same rung"
  assertion. The parser's corpus has none, so deleting this would have meant giving that
  assertion up. It is now read as plain data and written through the real `KnowledgeBaseStore`

## Steps

### 0. Start the stack (this PR)

1. Write this file. Update `docs/roadmap/README.md`: step 26, the moved MVP line, and 21
   marked `superseded`.
2. `gh stack` on top of `dev`.

### 26b. The parser

1. Capture the golden file **before** writing any C#: run the existing Node script against
   `scripts/5etools/fixture/data` and commit its output as
   `packages/TakeInitiative.KnowledgeBase.Tests/Golden/fixture-index.json`. Record the exact
   command in the test's comments so it can be re-derived.
2. Create the package and its test project; add both to `TakeInitiative.sln`. Target
   `net10.0`, `<Nullable>enable</Nullable>`, and nothing but `System.Text.Json`.
3. Port the parser module by module, using `build-5etools-index.mjs` as the specification.
   The parts that carry real knowledge and must not be paraphrased: `_copy` inheritance,
   CR formatting (fractions), HP dice extraction, the AC array shapes, source-book
   abbreviation to title, the id formation at line 90 and its validation at line 91, and
   the duplicate check at line 327.
4. Port the 231 lines of `build-5etools-index.test.mjs` as xUnit cases — they cover the
   failure paths a golden file cannot.
5. The golden test: parse the same fixtures, serialise with the same settings, assert
   equality with the committed golden file. A determinism test parses twice and asserts the
   two serialisations are identical.
6. `dotnet build -p:TreatWarningsAsErrors=True` and `dotnet test` clean. Do not touch the
   API or the web.

### 26c. The schema, the store and the CLI

1. **Fix the schema application first** — `Marten:ApplySchemaOnStartup`, per "The schema is
   not applied in production" above, with the before-and-after measurement. Do this before
   adding a new schema object, so the new object is created everywhere from its first day.
2. `KnowledgeBaseSchema`: the table, the generated `tsvector`, the trigram index and the
   filter index, as `ISchemaObject`s following `SearchSchema`.
3. `KnowledgeBaseStore`: `Diff(items)` → `IngestReport`, `Upsert(items)`,
   `Prune(provider, keep, force)` with the link check and the 20% threshold. One
   transaction per run.
4. The CLI: `ingest` with the flags above, configuration binding, a progress line per data
   file, the report, and the exit codes.
5. Store tests against Testcontainers Postgres: insert, re-run unchanged, change one field,
   a missing row with and without `--prune`, the threshold refusal, and the link-protection
   path (stubbed until 27 exists — assert the query, not an entry).
6. `dotnet run … -- ingest --dry-run --from packages/TakeInitiative.KnowledgeBase.Tests/Fixture/data --min-monsters 1`
   prints a sane report against a dev database.

   Two things an earlier draft of this step got wrong here. The fixture corpus lives in the
   test project (26b vendored it; `scripts/5etools/` is deleted in 26d₂), not under
   `scripts/`. And `FiveEToolsParserOptions.MinMonsters` defaults to 1000 while the fixtures
   hold 8, so without a way to lower it the command could only ever exit 1 — hence
   `--min-monsters` on the CLI, and `KnowledgeBase:Source:MinMonsters` beside it. The guard
   is worth keeping for real runs: a low count is how "you pointed at the wrong folder"
   shows up.

### 26d. The provider

1. `IReferenceProvider` → async; update the five call sites and `SrdReferenceProvider`.
   No behaviour changes anywhere.
2. `KnowledgeBaseReferenceProvider`: `Key = "5etools"`, `HasStatBlocks = false`, `Search`
   over the `tsvector` plus trigram, ranked on the same 0–4 ladder as
   `SearchSql.MatchCategory` so REFERENCE still ranks the way step 20 set out. `Get`
   returns null; `Find` reads one row.
3. Delete the four `FiveETools*` files, `FiveEToolsOptions` from configuration, and
   `scripts/5etools/`. Move the provider's tests over.

   **The test corpus was already vendored in 26b.** `packages/TakeInitiative.KnowledgeBase.Tests/Fixture/`
   is a byte copy of `scripts/5etools/fixture/`, read from `AppContext.BaseDirectory`, so the
   golden test has no notion of a repository root and survives this deletion. Do not go
   looking for the fixtures — deleting the folder is safe. (An earlier draft of this plan said
   to delete "the whole folder" without noticing it held the golden test's only input; 26b
   caught it and copied the corpus rather than following the plan into a broken commit.)
4. ⌘K for "beholder" against an ingested dev database returns what 21c returned.

### 26e. The browse API

1. `GET campaigns/{campaignId}/knowledge-base?q=&category=&book=&provider=&skip=&take=`,
   answering rows, the total, and the facet counts the filters need. Campaign-scoped in the
   route for membership authorisation only — the content does not vary by campaign or
   viewer.
2. `take` capped (50), `skip` bounded, `q` optional and using the same matcher as 26d.
3. Alba tests: paging, each filter, an empty database, and a non-member getting 403.
4. Regenerate `schema.d.ts` via `pnpm turbo run gen:api --filter=@ti/web` and commit it.

### 26f. The page

1. The page, the composable (TanStack Query, keyed on the filters), the list, the row, the
   filters and the two empty states.
2. Filters live in the URL (`?category=&book=&q=`) so a filtered view is shareable and
   survives reload, following 14e's `router.replace` convention so filtering adds no
   history entries.
3. The Wiki toolbar link and ⌘K's "Browse all reference material →" row.
4. Mobile first: 44px touch targets, sideways-scrolling chip rows, `Load more` rather than
   infinite scroll, and nothing covered by the keyboard (invariant 11).
5. `vitest` cases for the filter/URL round trip, the row's link-out, and the two empty
   states.

### 26g. The artwork

1. Parse the item's artwork path out of the 5eTools data into an absolute CDN URL, and
   store it in `image_url`. **Verify the base URL against real data before committing to
   it** — the fixtures may not carry a representative `href`, and this is the one part of
   the parse the golden file cannot validate.
2. Show it on the row and nowhere else, `loading="lazy"`, `referrerpolicy="no-referrer"`,
   fixed dimensions to avoid layout shift, and a category glyph as the fallback when it is
   null, fails, or is blocked.
3. Nothing is fetched or stored server-side. The browser loads it from their CDN or it does
   not load at all.

## Verify

1. Green CI on every PR in the stack, `0 Warning(s), 0 Error(s)`, with two new `.csproj`
   files in the solution — which per `CLAUDE.md` triggers **both** workflows, so
   `nuxi typecheck`, `schema.d.ts` and `pnpm --filter @ti/web test` must be clean too.
2. `dotnet test` covers: the golden file, determinism, the ported failure cases, the store's
   six diff paths, the browse endpoint's paging and filters, and the provider's ranking.
3. Against a real 5eTools folder on the user's machine:
   - `ingest --dry-run` reports a plausible count and writes nothing.
   - `ingest` writes them; the second run reports all-unchanged and writes nothing.
   - Edit one row in the database by hand; a third run reports `updated 1` and fixes it.
   - Point `--from` at a folder holding one bestiary file: the report shows thousands
     missing and **nothing is deleted**. With `--prune` it refuses on the threshold. With
     `--prune --force` it deletes, and a row an entry links to survives as `stale`.
4. ⌘K for "beholder", "fireball" and a monster that is in both the SRD and 5eTools ranks as
   it did after 21c, with SRD first.
5. On an empty database, the Knowledge base page says nothing has been ingested, and ⌘K
   shows SRD rows only.
6. At 390 × 844: filters, search, `Load more`, and a row opening 5etools in a new tab.
7. At 1440 × 900: the left filter rail, the counts, and a shared filtered URL reopening the
   same view.

## Notes / gotchas

- **The port is the risk in this step, and the golden file is the mitigation.** The 438
  lines of `build-5etools-index.mjs` encode 5eTools' JSON idioms — `_copy` inheritance
  especially — which took real effort to work out and are not obvious from their data.
  Capturing the Node output first turns "reimplement a parser for a format we half-remember"
  into "match a known-good output". Do step 26b.1 before writing any C#, and do not delete
  `scripts/5etools/` until the golden test is green.
- **Two new `.csproj` files trigger the web pipeline too**, per `CLAUDE.md`'s path filters.
  A pure-backend PR in this stack can still be failed by `schema.d.ts` drift.
- **`ReferenceMatcher` stays.** Its ported `word_similarity` still serves the SRD provider,
  which remains in memory. Do not delete it with the 5eTools files.
- **The CLI must not own migrations.** The schema is created by the API's
  `ApplyAllDatabaseChangesOnStartup` path through `ExtendedSchemaObjects`. If the CLI runs
  against a database the API has never started against, it should fail with "the schema is
  not there yet; start the API once", not create tables itself. One owner for the schema.
- **`stale` is a row's state, not a link's.** Step 27's link renderer reads it. Until 27
  exists there is no way for a row to be link-protected, so 26c's test asserts the query
  shape and 27 adds the real case.
- **Postgres 16** is what dev and the test fixtures pin (`compose.dev.yml` notes
  `postgres:latest` is now 18+ and moved its data directory). Nothing in this schema needs
  anything newer.

### Two things 26f found that this plan had wrong

- **The two empty states are not distinguishable from a filtered answer.** A filtered query
  over an empty corpus and one over a full corpus both return `total: 0`. So a shared
  `?category=monster` link, opened against a database nobody has ingested into, would have said
  "No items match. Clear filters." — blaming the reader's filters for exactly the condition this
  page exists to report. When a filtered page comes back empty the composable asks once more,
  unfiltered, for one row, and renders nothing until that answers rather than flashing the wrong
  message.
- **The phone sketch's row line contradicts "The schema".** The sketch shows
  `Monster · CR 13 · MM`, implying a short label; the schema section says `label` is the whole
  muted line and there is no shorter string. The prose is right: line 2 is `category · label`,
  line 3 is `book · p. N`.

### Behaviour worth knowing before 26e and 27

- **`source_title` is outside the content hash.** The hash is the parser's own serialisation
  of a row, and book titles are read once into the index header rather than onto each row. So
  a 5eTools release that renamed only a book reports every row unchanged and keeps the old
  caption. The fix if it ever matters is a one-line `update`; widening the hash would mean a
  second string to keep byte-stable, which is what this step is trying to avoid.
- **`stale` clears itself.** A row whose hash matches but which a previous prune flagged
  counts as *updated*, so the flag comes off when the source has it back. Otherwise step 27's
  link renderer would keep claiming the source is gone.
- **In a combined `--prune` run the threshold is measured after the upsert**, in the same
  transaction, so the denominator is the corpus as it would stand rather than as it was.
- **The browse endpoint sends `private, no-cache`, not a long `max-age`.** 26e shipped
  `max-age=86400` with `staleTime: Infinity`, which meant a browser that had seen the corpus
  could not see a re-ingest for a day without a hard reload — and "ingest, then look at it" is
  this page's only workflow. An `ETag` over the provider's latest `ingested_at` would turn
  revalidation into a 304 if it ever shows up in a profile.
- **From 26g on, this parser is the specification, not the Node script.** `imageUrl` is a field
  the script never had, so the golden file is no longer a comparison against it. `GoldenFileTests`
  records that.

### Decisions for the user

Each has the default this plan uses.

1. **Which categories the browse page covers.** *Default: all three the parser already
   emits — Monster, Spell, Item.* The parser does the work either way, so excluding spells
   and items would be extra code, not less.
2. **The artwork slot.** *Default: in, as 26g, last in the stack so it can be dropped
   without touching anything else.* It loads from 5eTools' CDN by URL and is never copied
   into our blob store. Say so if you would rather have no images at all.

   This one is formally yours, not a detail: `build-5etools-index.mjs`'s header states that
   the index holds "no rules text, descriptions, stat blocks, images or any other 5eTools
   field", and that **"any change to the allowlist is a decision for the user, not an
   implementation detail"**. 26g adds `image_url` to that allowlist. It is still only an
   identifier — a path we turn into a URL, with the bytes served by them to the browser and
   never touched by our server — but the script was written to make you say yes to this
   explicitly, so: say yes explicitly.
3. **A fourth bottom tab for the knowledge base.** *Default: no — it lives under the Wiki
   tab and in ⌘K.* A fourth tab costs a third of the wiki's touch target for something used
   far less often.
4. **Where the ingest runs for production.** *Default: from your machine, with
   `--connection` over an SSH tunnel.* The alternative is a one-off container in Coolify
   with the data mounted, which is tidier to repeat and more to set up; step 29 can add it
   later without changing the CLI.
5. **`--download`.** *Default: still out, as step 21 decided.* The CLI reads a folder you
   supply. A flag that fetches their data would put automation for retrieving book content
   into a public repo.
6. **The prune threshold.** *Default: 20% of a provider's rows, overridable with `--force`.*
7. **21d is still yours to run**: delete `origin/bestiary`, `origin/Bestiary_2025`,
   `origin/Bestiary_2025_CopyParsing`, `origin/Bestiary_2025_project_refactor`, then file
   the GitHub Support request to purge the cached commits. Deleting the refs does not stop
   GitHub serving those shas by id, and the repo is public. This step does not depend on it,
   but it is the natural moment.
