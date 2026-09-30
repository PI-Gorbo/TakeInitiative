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
-- ingest --from ~/5etools-src/data` prints `+2,847 new · 0 updated · 0 removed` and
writes them to Postgres. A DM opens **Wiki ▸ Knowledge base** on a phone, filters to
Monsters, types "behol", taps the Beholder row and lands on 5etools in a new tab. ⌘K for
"beholder" behaves exactly as it did after step 21, but the rows now come from the
database. Running the ingest a second time prints `+0 new · 0 updated · 0 removed` and
changes nothing. Pointing it at a folder with one bestiary file in it prints a warning that
2,800 rows are missing from the source and **deletes none of them**.

The step ships as seven PRs stacked with `gh stack` on top of this file's docs PR. Each PR
leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 26a | `v2/26a-knowledge-base-plan` | This plan | Docs only | [ ] |
| 26b | `v2/26b-kb-parser` | `packages/TakeInitiative.KnowledgeBase`: the ported parser and the row model | 25's app unchanged, and nothing references the package yet. Its tests reproduce the Node script's output from the same fixtures, byte for byte | [ ] |
| 26c | `v2/26c-kb-cli` | The schema, the upsert and `apps/TakeInitiative.KnowledgeBase.Cli` | The same. `ingest --dry-run` reports a diff; `ingest` upserts into Postgres. Nothing reads the table yet | [ ] |
| 26d | `v2/26d-kb-provider` | `IReferenceProvider` goes async; the 5eTools provider reads Postgres | ⌘K behaves as it did after 21c, with its 5eTools rows served from the database. `FiveEToolsIndex`, its options and `scripts/5etools/` are gone | [ ] |
| 26e | `v2/26e-kb-api` | The browse API | `GET knowledge-base` lists, filters and pages; `GET knowledge-base/{provider}/{id}` answers one row. No UI yet | [ ] |
| 26f | `v2/26f-kb-page` | The Knowledge base page | Wiki ▸ Knowledge base browses, filters and searches on a phone and on a desktop. ⌘K gains "Browse all". The step's Verify passes | [ ] |
| 26g | `v2/26g-kb-images` | The artwork slot | A row and its detail show the item's 5eTools artwork where one exists, with a fallback, loaded from their CDN and never stored | [ ] |

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
    label         text,                       -- 'CR 13' | 'Level 3' | 'Rare'
    detail        text    not null,           -- the muted row line: 'CR 13 · Large Aberration'
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

- **The primary key is the parser's id**, not a generated one. `build-5etools-index.mjs:90`
  already forms `monster_beholder_mm` from category, name and source, and fails on a
  duplicate at line 327. That is what makes a link durable across re-ingests, and it is the
  single most important thing the port must preserve.
- **Search** gets a stored generated `tsvector` column over `name`, plus a `gin_trgm_ops`
  index on `lower(name)` for fuzzy matching — the same two-pronged approach step 17 uses for
  entries, following `SearchSchema`'s pattern for adding a generated column through Marten's
  `ExtendedSchemaObjects`.
- **Indexes**: the `tsvector`, the trigram index, and a plain `(provider, category,
  source_book)` for the browse page's filters.
- Created through the same `ExtendedSchemaObjects` path the search schema uses, so
  `ApplyAllDatabaseChangesOnStartup` handles it and the CLI does not own migrations.

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

- `scripts/5etools/` — the whole folder, in 26d, once 26b's golden test proves the port
- `apps/TakeInitiative.Api/src/Features/Reference/FiveETools/FiveEToolsIndex.cs`,
  `FiveEToolsOptions.cs`, `FiveEToolsCatalog.cs`, `FiveEToolsReferenceProvider.cs`
- `apps/TakeInitiative.Api.Tests/Fixtures/5etools-index.json`, once its tests move to the
  new provider

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

1. `KnowledgeBaseSchema`: the table, the generated `tsvector`, the trigram index and the
   filter index, as `ISchemaObject`s following `SearchSchema`.
2. `KnowledgeBaseStore`: `Diff(items)` → `IngestReport`, `Upsert(items)`,
   `Prune(provider, keep, force)` with the link check and the 20% threshold. One
   transaction per run.
3. The CLI: `ingest` with the flags above, configuration binding, a progress line per data
   file, the report, and the exit codes.
4. Store tests against Testcontainers Postgres: insert, re-run unchanged, change one field,
   a missing row with and without `--prune`, the threshold refusal, and the link-protection
   path (stubbed until 27 exists — assert the query, not an entry).
5. `dotnet run … -- ingest --dry-run --from scripts/5etools/fixture/data` prints a sane
   report against a dev database.

### 26d. The provider

1. `IReferenceProvider` → async; update the five call sites and `SrdReferenceProvider`.
   No behaviour changes anywhere.
2. `KnowledgeBaseReferenceProvider`: `Key = "5etools"`, `HasStatBlocks = false`, `Search`
   over the `tsvector` plus trigram, ranked on the same 0–4 ladder as
   `SearchSql.MatchCategory` so REFERENCE still ranks the way step 20 set out. `Get`
   returns null; `Find` reads one row.
3. Delete the four `FiveETools*` files, `FiveEToolsOptions` from configuration, and
   `scripts/5etools/`. Move the provider's tests over.
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
- **Postgres 15** is what dev and the test fixtures pin (`compose.dev.yml` notes
  `postgres:latest` is now 18+ and moved its data directory). Nothing in this schema needs
  anything newer.

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
