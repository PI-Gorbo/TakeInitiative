# 21 — 5eTools index

## Goal

⌘K finds things that are not in the SRD. Typing "beholder" shows a **REFERENCE** row
`Beholder  Monster · CR 13 · MM (5eTools)  [↗] [+ Wiki]` (design §11). The row **links
out** to that item's page on 5etools in a new tab. **The app never shows 5eTools
content**: there is no card, no rules text and no description. **+ Wiki** still works: it
creates an entry with `Source` set, and for a DM it fills **Stats** (initiative, HP dice
and AC) from the index, so `@Beholder` in a combat has numbers.

The data behind it is an **index**, not a copy of 5eTools. A preprocessing script reads a
5eTools data folder that the **deployer supplies on their own machine**, and writes a small
JSON file of names, categories, source books, pages, short labels (CR, spell level, item
rarity), the three Stats numbers for monsters, and a deep link. That file is **never
committed**, never embedded in the API and never baked into an image. The API reads it
from a path in its configuration at startup. With no file there, the 5eTools provider is
off and the app behaves exactly as after step 20.

The step also closes design question 9: the parked **Bestiary branches** are mined for
what they learned about 5eTools' JSON (this file's Notes) and then deleted.

"Running" at the end: on a dev machine with an index built, a DM searches ⌘K for
"beholder", presses Enter and lands on 5etools' Beholder page in a new tab. Back in the app,
the DM adds it to the wiki as "The Eye" (DM visibility). The entry's source line reads
"↗ From 5eTools · Beholder (MM p. 28)", its Stats are filled, and `@The Eye` in a Draft
combat gives a combatant with rolled HP. A player on a phone finds "fireball", taps
through to 5etools, and adds nothing. On a machine with no index, ⌘K shows only SRD rows,
as in step 20.

The step ships as three PRs stacked with `gh stack` on top of this file's docs PR, which
sits on 20d (#249). Each PR leaves the app runnable. Deleting the branches is a fourth,
manual step for the user, not a PR:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 21a | `v2/21a-5etools-script` | The preprocessing script, its index schema and its tests | 20's app unchanged. `pnpm 5etools:build --from <5etools-src>` writes a git-ignored `.data/5etools/index.json`, and the script's tests pass on a synthetic fixture | [x] |
| 21b | `v2/21b-5etools-provider` | The search-only provider (API) | With an index configured, `GET search` has 5eTools rows with a `url`, + Wiki creates entries from them, and `GET reference/5etools/{id}` answers a summary with no stat block. Without one, nothing changes | [x] |
| 21c | `v2/21c-5etools-web` | Rows, + Wiki and the source line for a search-only provider (web) | 5eTools rows in ⌘K link out; + Wiki, the source line and "Use 5eTools stats" work for them. The step's Verify passes | [x] |
| 21d | — (the user) | Delete the Bestiary branches | `origin/bestiary`, `origin/Bestiary_2025`, `origin/Bestiary_2025_CopyParsing` and `origin/Bestiary_2025_project_refactor` are gone | [ ] |

Stat blocks or rules text from 5eTools, backgrounds, feats, classes and races, and any
automatic download of 5eTools data are not in 21 (Notes, "Not in 21").

## Depends on

- **20**: `IReferenceProvider` (with `HasStatBlocks`, `Search`, `Get` and `Find`),
  `ReferenceSummary.Url` and `.Stats`, `ReferenceCatalog`, `ReferenceMatcher`,
  `ReferenceStats.Initiative`, `ReferenceSearchProvider`'s merge, `POST
  entries/from-reference`, `EntrySources.CanRead`, and on the web `hitTarget`'s `external`
  target, the row's external-link icon, `AddToWikiDialog`, `EntrySourceLine` and "Use SRD
  stats". Step 20's Notes, "Seams for step 21", list what this plugs into.
- **17**: the ⌘K sheet and `SearchService`.

These parts of [12-v2-design-session.md](12-v2-design-session.md) are binding:

- §11, "Reference search in ⌘K": 5eTools is **search-only**; a preprocessing script turns
  its raw JSON into a small index; **the app never displays 5eTools content**; a result
  links out; the index is built locally per deployment and is not committed; the parked
  Bestiary branches are mined and then deleted;
- the glossary (§1): Reference, Reference item, Reference provider, Source, + Wiki. The
  Bestiary branches' word "bestiary" is not a glossary word; don't bring it back;
- the invariants (§10, especially 5, 8 and 10). Reference search is not secret, and an
  entry's source follows `EntrySources.CanRead` (step 20, "Source is DM-only on NPCs").

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web), except where they start at the repo root.

**What exists today:**
- `src/Features/Reference/*` from step 20. `ReferenceCategory` has one value, `Monster`.
  `AddReference()` in `src/boostrap/Bootstrap.cs` registers only `SrdReferenceProvider`.
  `Program.cs` calls `SrdCatalog.EnsureLoaded()` after `Build()`.
- `GetReferenceItem` answers 404 for a provider whose `Get` is null, which a search-only
  provider's always is.
- `scripts/srd/build-srd52.mjs`, the model for the new script: plain Node, no
  dependencies, run by hand, fails loudly, no timestamps.
- On the web: `hitTarget` already sends a row with no stat block and a `url` to
  `external`, and the sheet opens it with `window.open(…, "_blank", "noopener")`.
  `EntrySourceLine` already links to `url` when `hasStatBlock` is false.
  `canUseSourceStats` (in `utils/entries.ts`) needs a stat block, and the button says
  "Use SRD stats".
- The four `origin/*estiary*` branches (Notes, "The Bestiary branches"). No local branch
  tracks them.

**This PR (docs)**
- `docs/roadmap/21-5etools-index.md`: this file
- `docs/roadmap/README.md`: link step 21, `in progress`
- `docs/roadmap/HANDOVER.md`: "Next work" points here, and the stack table gains this PR

**21a**
- Root add `scripts/5etools/build-5etools-index.mjs` and
  `scripts/5etools/build-5etools-index.test.mjs` (`node --test`)
- Root add `scripts/5etools/fixture/` — a **synthetic** 5eTools-shaped data folder with
  invented names and a made-up source `TST` (Notes, "No 5eTools data in the repo")
- Root modify `package.json` (`"5etools:build"`, `"5etools:test"`), `.gitignore`
  (`/.data/`, `5etools-src/`)
- Root modify `NOTICE` (a short 5eTools section: what the index holds, and that no
  5eTools content is included)

**21b**
- API add `src/Features/Reference/FiveETools/{FiveEToolsIndex,FiveEToolsCatalog,FiveEToolsReferenceProvider,FiveEToolsOptions}.cs`
- API modify `src/Features/Reference/ReferenceItem.cs` (`ReferenceCategory` gains `Spell`
  and `Item`), `src/boostrap/Bootstrap.cs` (`AddReference(configuration)`), `Program.cs`,
  `appsettings.Development.json` (`Reference:FiveETools:IndexPath`)
- API modify `src/Features/Reference/Api/GetReferenceItem/*` (a search-only item answers
  its summary with `statBlock: null`)
- Tests add `Scopes/Unit/FiveEToolsCatalogTests.cs`,
  `Scopes/Integration/Features/Reference/FiveEToolsReferenceTests.cs`, and a synthetic
  `Resources/5etools-index.json`
- Web `utils/api/schema.d.ts`: regenerated

**21c**
- Web modify `utils/search.ts` and `components/Search/SearchHitRow.vue` (the category
  label and the book), `components/Reference/{AddToWikiDialog,EntrySourceLine}.vue`,
  `components/Wiki/StatsEditor.vue` and `utils/entries.ts` ("Use 5eTools stats"),
  `utils/reference.ts`, the reference page (a search-only item links out)
- Web modify `tests/unit/{reference,search,mergeClaimStats}.test.ts`
- `docs/roadmap/21-5etools-index.md`, `README.md`, `HANDOVER.md`: tick and close the step

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/20d-add-to-wiki
gh stack add v2/21-5etools-plan
```

Add each sub-step on top with `gh stack add v2/21a-5etools-script` and so on.

**Glossary check.** No new nouns. The provider is a **Reference provider** whose items
are **Reference items**; "index" is the file the script writes, and it stays a word for the
file, not for anything in the UI. The UI says "5eTools".

### 21a. The preprocessing script

1. **Input: a folder the deployer supplies.** `pnpm 5etools:build --from <dir>` reads a
   local copy of the 5eTools source data. `<dir>` is either a checkout of the 5etools
   source repository or its `data/` folder; the script finds `data/` either way. **The
   script never downloads anything** (Notes, "Where the data comes from"). With no
   `--from`, it prints how to get a copy and exits 1.
2. **What it reads**, and nothing else:
   - `data/bestiary/index.json` and the `bestiary-*.json` files it lists (`monster[]`);
   - `data/spells/index.json` and the `spells-*.json` files it lists (`spell[]`);
   - `data/items.json` (`item[]`) and `data/items-base.json` (`baseitem[]`);
   - `data/books.json` and `data/adventures.json`, for each source abbreviation's full
     title (the ⌘K row shows the abbreviation; the source line's tooltip shows the title).

   It skips anything flagged as fluff, homebrew or UA (a source whose abbreviation starts
   `UA`, and `data/bestiary/fluff-*`), and prints each count.
3. **What it writes: an allowlist, field by field.** The script builds each row from
   named fields, never by copying a 5eTools object. It writes these and only these:

   ```jsonc
   // .data/5etools/index.json (one item per line, like monsters.json)
   { "format": 1, "fiveEToolsVersion": "2.x.y", "counts": { "monster": 0, "spell": 0, "item": 0 },
     "sources": { "MM": "Monster Manual (2014)", "XMM": "Monster Manual (2025)", "PHB": "…" },
     "items": [
       { "id": "monster_beholder_mm", "name": "Beholder", "category": "Monster",
         "source": "MM", "page": 28, "url": "https://5e.tools/bestiary.html#beholder_mm",
         "label": "CR 13 · Large Aberration",
         "stats": { "ac": 18, "hp": "19d10+76", "initiativeBonus": 2 } },
       { "id": "spell_fireball_xphb", "name": "Fireball", "category": "Spell",
         "source": "XPHB", "page": 274, "url": "https://5e.tools/spells.html#fireball_xphb",
         "label": "Level 3 Evocation", "stats": null },
       { "id": "item_bag-of-holding_xdmg", "name": "Bag of Holding", "category": "Item",
         "source": "XDMG", "page": 228, "url": "https://5e.tools/items.html#bag%20of%20holding_xdmg",
         "label": "Uncommon Wondrous Item", "stats": null } ] }
   ```

   - **Never written:** `entries` (rules and description text), actions, traits,
     spellcasting, legendary groups, fluff, images, tokens, alignment, ability scores
     other than what initiative needs, saves, skills, senses, languages, spell components,
     ranges and durations, item properties and attunement text.
   - `label` is built from enums the script maps itself (size letters → "Large",
     `type` → "Aberration", school letters → "Evocation", rarity → "Uncommon"). Any
     free text in a 5eTools label field (a `type` with a free-text tag, say) is dropped,
     not copied.
   - `stats` is monsters only, and only when all three resolve (step 5). Otherwise it is
     null, and + Wiki creates the entry without Stats.
   - `--no-stats` writes `stats: null` everywhere (Notes, decision 1).
   - The output is sorted by category, then name, then source, and has no timestamp, so
     a rebuild from the same 5eTools version is byte for byte the same.
4. **Ids and links.**
   - `id` is `{category}_{name slug}_{source lower}`: `monster_goblin-boss_xmm`. It is
     stable across rebuilds, has no characters a route needs to escape, and is what
     `Entry.Source.ExternalId` stores. A duplicate id fails the build.
   - `url` is `https://5e.tools/{page}.html#{hash}`, with `page` one of `bestiary`,
     `spells` and `items`, and `hash` built the way 5etools' `UrlUtil.encodeForHash`
     does: each part lower-cased and URI-encoded, joined with `_`. The Bestiary
     branches' `BuildLink` did neither, so "Goblin Boss" linked to
     `#Goblin Boss_MM`; don't copy it. The test checks names with a space, an
     apostrophe, a comma and parentheses.
   - The base URL is one constant, `--base-url` overrides it (Notes, "Which 5eTools").
5. **Monsters: resolving what the index needs.** 5eTools' monster JSON is uneven. The
   Bestiary branches found these, and the script handles each one and counts it:
   - `_copy: { name, source, _mod, _templates }`: the monster is a variant of another.
     The script takes the base's fields, then the copy's own top-level fields over them.
     `_mod` edits text the index never stores, so it is ignored. `_templates` can change
     size, type or CR; the script ignores them and counts the monsters that had one. A
     copy whose base is missing is skipped and named in the summary.
   - `ac`: an array whose items are a number or `{ ac, from, condition }`. The index
     takes the first item's number (the unconditional AC).
   - `hp`: `{ average, formula }` or `{ special }`. Stats take the `formula`, normalised
     like the SRD's (`"19d10 + 76"` → `"19d10+76"`) and checked by the tests with the
     real dice roller (21b). `special` HP (a swarm "equal to half…", a summon) gives
     `stats: null`.
   - Initiative: `initiative.initiative` when it is a number; otherwise the DEX modifier
     plus `initiative.proficiency` × the proficiency bonus for the CR. This is the
     Bestiary branches' `CalculateInitiative`, with its CR table.
   - `cr`: a string (`"1/4"`), `{ cr, lair, coven }`, or `"Unknown"` (the Mechanical
     Bird). The label shows `cr.cr`; "Unknown" gives a label with no CR.
   - Broken rows: a monster with neither `hp` nor `_copy` (Elzerina Cassalanter, WDH) is
     skipped and named.
   - SRD duplicates: a monster flagged `srd52` duplicates a row the SRD provider already
     has with a stat block. The script drops it (Notes, decision 4).
6. **Checks.** It fails loudly on: no `data/` folder; fewer than 1,000 monsters (a wrong
   folder; the tests pass `--min-monsters 1`); a duplicate id; an unknown category,
   size or school letter; a `stats.hp` that is not `NdM[+-K]`; any row whose serialised JSON has a key outside the allowlist. It
   prints the counts, the skips and the 5eTools version it read (from the checkout's
   `package.json`).
7. **Tests** (`node --test scripts/5etools/`), against `scripts/5etools/fixture/`, a
   hand-written folder shaped like 5eTools' `data/` with invented creatures, spells and
   items under the made-up source `TST`:
   - a plain monster, a `_copy` with and without `_mod`, a `_templates` copy, a copy with
     a missing base, `special` HP, every AC shape, every CR shape, and initiative with and
     without `proficiency`;
   - the link encoder's edge cases;
   - the allowlist: a fixture monster has `entries`, `action` and `fluff` text, and a test
     asserts that none of its words reach the output;
   - `--no-stats`, and a byte-for-byte rebuild.
8. **Git and packaging.**
   - `.gitignore` gains `/.data/` and `5etools-src/`, so neither the index nor a checkout
     next to the repo can be committed by accident.
   - `NOTICE` gains a short "5eTools" section: the app can link to 5etools and hold, per
     deployment, an index of item names, source books, pages and a few numbers built from
     data the deployer supplies; no 5eTools or Wizards of the Coast content is
     distributed with TakeInitiative; the non-affiliation line.

### 21b. The search-only provider (API)

1. **Configuration.** `FiveEToolsOptions { IndexPath }`, bound from
   `Reference:FiveETools` (`Reference__FiveETools__IndexPath` in the environment).
   - `appsettings.Development.json` sets it to the repo's `.data/5etools/index.json`,
     resolved from the content root.
   - Production has no value, so the provider is off until the user decides to turn it
     on (Notes, decision 3).
2. **`FiveEToolsCatalog`**, a singleton, loads the file once at startup with
   `System.Text.Json` into rows with folded names, like `SrdCatalog`.
   - A missing path or file is **not** an error: it logs one information line ("5eTools
     index not configured; the 5eTools provider is off") and holds nothing.
   - A file that is there but unreadable, or has an unknown `format`, **fails startup**,
     so a bad deploy is loud.
   - It never touches the network (invariant 10).
3. **`FiveEToolsReferenceProvider : IReferenceProvider`.**
   - `Key` is `"5etools"`, `Label` is `"5eTools"`, `HasStatBlocks` is false.
   - `Search` is `ReferenceMatcher.Search` over the rows, as the SRD's is. A linear scan
     of the expected ~10–15k names is well under a millisecond (step 20's Notes, "In
     memory, not pg_trgm"). If a benchmark in the tests says otherwise, add an in-memory
     trigram prefilter behind `Search`.
   - `Get` is always null. `Find` answers the summary.
   - The summary: `Detail` is `"{label} · {source}"` (`"CR 13 · Large Aberration ·
     MM"`); `Url` is the row's `url`; `SuggestedKind` is `Character` for a monster, `Item`
     for an item and `Other` for a spell; `Stats` is
     `Stats.Of(ReferenceStats.Initiative(bonus), hp, ac)` when `stats` is set, else null.
   - It registers in `AddReference()` **after** the SRD, and only when the catalog
     loaded rows, so the Reference merge's provider order puts SRD rows first and an
     empty index adds nothing. `ReferenceCatalog` is unchanged.
4. **`ReferenceCategory`** gains `Spell` and `Item`. `ReferenceSearchProvider`'s merge is
   unchanged (rung, similarity, provider order, length, name), so "goblin" still gives
   the SRD's goblins first, then 5eTools' Goblin Boss (MM) and so on.
5. **`GET reference/{provider}/{id}`** for a search-only provider answers `{ summary,
   statBlock: null, attribution }` instead of 404. The summary holds only index fields,
   so no 5eTools content leaves the server, and it gives the web the item's `stats` for
   the + Wiki dialog and "Use 5eTools stats" without a new endpoint (Notes, "Why the item
   endpoint answers"). `attribution` for 5eTools is `{ text: "Found in the 5eTools index.
   Opens 5etools in a new tab.", licenseName: "", licenseUrl: "", sourceUrl: <base url>
   }`; the web does not show it as a licence.
6. **+ Wiki** needs no change: `POST entries/from-reference` already allows search-only
   providers, and `EntrySource.Url` becomes the row's deep link. `EntrySourceResponse`
   gets the book too: `detail` (`"MM p. 28"`), from the row, when the item is still in
   the index.
7. **Tests**, against `Resources/5etools-index.json` (synthetic, `TST`), which the test
   host points `Reference:FiveETools:IndexPath` at:
   - `FiveEToolsCatalogTests`: it loads; every monster's Stats pass the real
     `IDiceRoller.Check`; no path means no rows and no exception; an unknown `format`
     throws.
   - `FiveEToolsReferenceTests`:
     - `GET search` returns SRD rows before 5eTools rows at the same rung, and 5eTools
       rows carry `url`, `hasStatBlock: false` and the category;
     - `GET reference/5etools/{id}` answers a summary with `statBlock: null`, and 404 for
       an unknown id;
     - + Wiki from a 5eTools monster gives a DM an entry with `source.url` and Stats, and
       a player an entry with neither;
     - a spell becomes an `Other` entry and an item an `Item` entry, both without Stats;
     - the leak test from 20b, repeated for a 5eTools source: a player never sees
       `5etools` or the deep link on a DM's unclaimed Character;
     - with the index path unset, the Reference section is exactly step 20's.

### 21c. Rows, + Wiki and the source line (web)

1. **The ⌘K row** for a search-only item: 📖, the name, then muted "Monster · CR 13 · MM
   (5eTools)" or "Spell · Level 3 · XPHB (5eTools)", then the external-link icon (20c).
   Enter or a tap opens the `url` in a new tab; the sheet stays open behind it so the
   user can come back to the same results. The trailing + Wiki button and ⌘Enter work
   as for SRD rows.
2. **The + Wiki dialog** shows the kind from `suggestedKind` (Character, Item or Other),
   and for a DM the Stats line from `getReferenceItemQuery`, which now answers for 5eTools
   items too. A spell or item has no Stats line.
3. **The source line**: "↗ From 5eTools · Beholder (MM p. 28)", linking to `url` in a new
   tab with `rel="noopener noreferrer"`. The tooltip is the book's full title.
4. **"Use 5eTools stats"**: `canUseSourceStats` becomes "the source's item has Stats and
   the viewer can write them", not "the source has a stat block", and the label is
   "Use {providerLabel} stats". It reads the item's `summary.stats`, fills the form, and
   Save is the normal `PUT stats`.
5. **The reference page** for a search-only item (reachable only by typing its URL)
   shows the name, the label and an "Open on 5etools ↗" button, and nothing else.
6. **Tests.** `search.test.ts`: the row's line for each category, and the external
   target. `reference.test.ts`: the source-line text and link for a search-only
   provider, and the dialog's kind per category. `mergeClaimStats.test.ts`: "Use … stats"
   shows for a 5eTools source with Stats, and not for a spell.
7. **Close the step**: tick this file's PR table (21a–21c), set README's status to
   `done` once 21d is done too, and update HANDOVER.

### 21d. Delete the Bestiary branches (the user)

This is not a PR. The knowledge worth keeping is in this file's Notes ("The Bestiary
branches"), and 21a's script replaces the code. After 21a merges, the user runs:

```sh
git push origin --delete bestiary Bestiary_2025 Bestiary_2025_CopyParsing Bestiary_2025_project_refactor
git fetch --prune
```

No agent deletes them. Before running it, read Notes, "5eTools data already in the
public repo".

## Verify

1. `dotnet test`, `pnpm 5etools:test`, `vitest`, `npx nuxi typecheck` and `pnpm build`
   pass, `schema.d.ts` is fresh, and CI is green on every PR. CI never has a 5eTools
   index, so it also proves the app runs with the provider off.
2. `git status` after `pnpm 5etools:build --from <5etools-src>` shows **no new tracked or
   untracked files** outside `.data/` (which is ignored). `git grep -il '"_copy"' -- ':!scripts/5etools/fixture'`
   and a search for any `bestiary-*.json` outside `scripts/5etools/fixture/` (the invented
   `TST` fixture, 21a) find nothing.
3. A rebuild from the same 5eTools checkout is byte for byte the same.
4. Spot-check the index: pick five rows and confirm each has only the allowlisted keys,
   and that each `url` opens the right page on 5etools (a name with a space, one with an
   apostrophe, a spell and an item among them).
5. `pnpm dev` with the index: the API logs how many 5eTools items it loaded. Without it
   (rename the file): the API logs that the provider is off and ⌘K is step 20's.
6. Two browser profiles, A (the owner, DM) at 1280 × 800 and B (a Player) at 390 × 844,
   with the index built:
   1. A presses ⌘K and types "goblin". The SRD's goblins come first; 5eTools rows
      follow with "Monster · CR … · MM (5eTools)" and the external-link icon.
   2. A types "beholder" and presses Enter. 5etools' Beholder page opens in a new tab;
      ⌘K is still open in the app with the same results.
   3. A presses + Wiki on it, renames it "The Eye", DM visibility, and adds it. The
      dialog showed its Stats. The entry page shows "↗ From 5eTools · Beholder (MM p.
      …)" and Stats.
   4. In a Draft combat, A adds `@The Eye`. The combatant has rolled HP and AC 18.
   5. On B's phone, ⌘K "fireball" shows a Spell row; a tap opens 5etools. B adds "Bag of
      Holding" to the wiki: it is an Item entry with no Stats line in the dialog.
   6. A adds 5eTools' Goblin Boss as "Grik" with visibility Everyone. B opens Grik: no
      source line and no Stats, and B's `GET entry` JSON has no `source` key.
   7. A adds a 5eTools monster as a player would have (no Stats), then uses "Use 5eTools
      stats" on it, and Saves.

## Notes / gotchas

### Licensing and what is stored

- **What 5eTools is.** 5etools is a fan site whose data files hold most of Wizards of the
  Coast's published 5e books, well beyond the SRD. That content is **copyrighted and not
  licensed for reuse**; only the SRD is CC-BY. So this step is built on the rule in §11:
  **store an index, show nothing, link out.**
- **What the index holds**, and all it holds: for each monster, spell and item, its
  name, category, source-book abbreviation, page number, a short label built from
  enums (CR and size/type; spell level and school; item rarity and type), a deep link,
  and for monsters AC, the HP dice formula and the initiative bonus. These are
  identifiers and game numbers, not rules text or descriptions. The allowlist and its
  test (21a.3, 21a.7) are what keep it that way; any change to the allowlist is a
  decision for the user, not an implementation detail.
- **What is never stored or shown:** rules and description text, actions, traits,
  spell and item text, fluff, images and tokens, full stat blocks. There is no 5eTools
  card. Everything beyond the row's name and label happens on 5etools' own site.
- **Where it lives.** The index is built on the deployer's machine from a 5eTools copy
  the deployer supplied, written to the git-ignored `.data/`, and read by the API from a
  configured path. It is not committed, not embedded in the DLL, not in a Docker image,
  and not downloaded by the app at run time.
- **No 5eTools data in the repo.** The script's tests and the API's tests use synthetic
  fixtures with invented names under the source `TST`. Never add a real 5eTools file,
  not even a small excerpt, as a test resource. The Bestiary branches did exactly that
  (below).
- **Where the data comes from.** The deployer gets a copy of the 5etools source data
  themselves (for example a checkout of the 5etools source repository) and passes its
  path with `--from`. The script does not download it: fetching a copy of copyrighted
  books is the deployer's own choice, not something the repo automates. The Bestiary
  branches downloaded the source repository's release zip, and also scraped 5e.tools
  with Playwright when that failed; neither is carried over (decision 2).
- **Attribution and trademarks.** Nothing from 5eTools is shown, so no attribution
  statement applies. The row names its book by abbreviation, which is a citation. Don't
  use Wizards' or 5etools' logos, and keep `NOTICE`'s non-affiliation line.

**Decisions for the user.** Each has the default this plan uses.

1. **Stats in the index.** Design §11 puts HP, AC and initiative in the index so + Wiki
   can fill Stats. They are numbers, not text, but they come from non-SRD books.
   *Default: included, as designed. `--no-stats` drops them, and the app then creates
   5eTools entries without Stats.* If you want the most conservative index (name,
   category, book, page, link), say so and 21a makes `--no-stats` the default.
2. **Downloading 5eTools data.** *Default: the script only reads a local folder you
   point it at; it never downloads.* An opt-in `--download` from the 5etools source
   repository's releases is possible, but it means the repo automates fetching
   copyrighted books, so it is left out unless you ask for it.
3. **Turning it on in production.** A public deployment with the index shows non-SRD
   monster, spell and item names (Beholder, Mind Flayer) to anyone signed in, and links
   to 5etools. *Default: off in production (no `IndexPath`); on in dev when you build an
   index.* Turning it on is setting one environment variable and putting the file on the
   server.
4. **SRD duplicates.** *Default: the script drops monsters flagged `srd52`, since the SRD
   provider already has them with a card.* The cost is that the 2025 Monster Manual's own
   page for those monsters has no link. Spells and items are not in the SRD provider yet,
   so none are dropped.
5. **5eTools data already in the public repo.** See below: whether to ask GitHub to purge
   it after the branches are deleted.

### 5eTools data already in the public repo

`PI-Gorbo/TakeInitiative` is **public**. `origin/Bestiary_2025_CopyParsing` has real 5eTools
data in its history: over 280 `bestiary-*.json` files under
`apps/TakeInitiative.Bestiary.Tests/Resources/` in its earlier commits (the full monster
data of most books), trimmed later to `bestiary-mm.json` (about 78k lines) and
`bestiary-cos.json`. No other branch, and neither `dev` nor `main`, has them.

- Deleting the branch (21d) removes the ref. GitHub can still serve those commits by sha
  until it garbage-collects them, and forks keep them. No PR references
  `Bestiary_2025_CopyParsing` (#185 merged `Bestiary_2025_project_refactor`, which has
  no data files), so nothing pins them in the repo.
- To remove them for certain, the user can ask GitHub Support to purge the cached
  commits after deleting the branch. That is decision 5; *default: delete the branch
  and don't file a request*.
- `bestiary_Schema.json` on `Bestiary_2025*` is a copy of 5eTools' JSON schema, which is
  code, not book content. It goes with the branches anyway.

### The Bestiary branches

Four remote branches, none merged into `dev`, none tracked locally:

| Branch | Last commit | What it is |
|---|---|---|
| `origin/bestiary` | 2024-04-06 | `apps/TakeInitiative.BestiaryHandler`: an early attempt that deserialised 5eTools' book JSON into Marten, with `_copy`/`_mod` and trait handling |
| `origin/Bestiary_2025_project_refactor` | 2025-07-11 | A separate `TakeInitiative.Bestiary` API (FastEndpoints, LiteDB), a `Domain` project and an `IngestionScript`; merged into `Bestiary_2025` by #185 |
| `origin/Bestiary_2025` | 2025-07-11 | The same, after #185 |
| `origin/Bestiary_2025_CopyParsing` | 2025-07-26 | `Bestiary_2025` plus C# classes for `_copy` blocks and their modifiers (`CopyBlockGeneric`, `CopyModifiers`), a normaliser that copies AC, DEX, HP, initiative and CR from the base, tests, and the 5eTools data files above |

What they taught, all of it now in 21a:

- The data's shape: `data/bestiary/index.json` maps sources to `bestiary-*.json` files,
  each with a `monster[]` array. Only files starting `bestiary` are monsters.
- The fields combat needs: `name`, `source`, `hp { average, formula }`, `ac[]` (number or
  `{ ac, from, condition }`), `dex`, `initiative { initiative?, proficiency?,
  advantageMode? }`, `cr` (string or `{ cr, lair, coven, xp }`).
- Initiative: the explicit number if there is one; otherwise the DEX modifier, plus
  `proficiency` × the CR's proficiency bonus.
- Edge cases: `_copy` monsters lack their own HP, AC and DEX, so they must be resolved
  against the base; a monster with neither `hp` nor `_copy` (Elzerina Cassalanter, WDH)
  is broken; a CR of "Unknown" (Mechanical Bird); `_mod` and `_templates` exist and are
  mostly about text (5eTools' homebrew `Spec_Copy.md` documents them).
- The link format `https://5e.tools/bestiary.html#{name}_{source}`, which needs 5eTools'
  hash encoding (lower case, URI-encoded) that `BuildLink` left out.
- LiteDB and a separate service were more machinery than a read-only list of names
  needs. 20's in-memory catalog replaces them.

### Other notes

- **Why the item endpoint answers for 5eTools.** Step 20's seam note said `GET
  reference/5etools/{id}` would be a 404 "because the app never shows 5eTools content".
  The dialog's Stats line and "Use … stats" read the item's summary through that
  endpoint, so a 404 would need a second endpoint for the same thing. The summary has only
  index fields (name, label, book, link, Stats), which the ⌘K row already shows, so
  answering it shows no content. `statBlock` stays null, and the reference page never
  draws a card for it.
- **Which 5eTools.** 5etools has moved hosts over the years. The base URL is one constant
  in the script and is stored in every row's `url`. If the host moves, a rebuild with
  `--base-url` fixes new rows; existing entries keep their old `Source.Url` (a follow-up
  could rewrite them on read from `externalId`, which does not change).
- **Ids survive 5eTools renames badly.** If 5eTools renames an item, its id changes and
  an entry's source line shows the stored id with no link (step 20, "No updates flow into
  entries"). That is acceptable for a link-out index.
- **Size.** Expect roughly 5k monsters, 1k spells and 3k items (bases and magic items)
  across 2014 and 2024 books, at about 200 bytes a row: 2–3 MB of JSON, held once in
  memory. The API's start-up cost is one file read.
- **Not in 21:**
  - any 5eTools text, stat block or card, now or as an option;
  - backgrounds, feats, classes, subclasses, races, conditions, rules, adventures'
    content, deities, vehicles, objects and traps (each is another category mapping; add
    one only when there is a use for it);
  - homebrew and UA content;
  - SRD spells and items in the SRD provider (step 20's "Not in 20");
  - a UI or admin page for uploading an index; it is a file on the server;
  - rewriting old entries' `Source.Url` when 5etools moves.

### As built

- **As built, 21a.** Where it differs from 21a above:
  - **Shape.** One script, `scripts/5etools/build-5etools-index.mjs`, that exports its
    helpers (`buildIndex`, `encodeHash`, `makeId`, `acOf`, `hpDiceOf`, `initiativeOf`,
    `crOf`, `serialize` and the `TOP_KEYS` / `ITEM_KEYS` / `STATS_KEYS` allowlists) and
    runs `main` only when called directly, so the tests import it. It also takes
    `--out <file>` (default `.data/5etools/index.json`), which the tests use to write to
    a temp folder. `pnpm 5etools:test` is `node --test "scripts/5etools/*.test.mjs"`
    (23 tests); there was no root test runner to reuse.
  - **The allowlist is enforced twice.** `pick()` builds each row from named fields
    only, and `check()` fails the build if a row's keys (or its stats' keys) are not
    exactly `ITEM_KEYS` / `STATS_KEYS`, in order. A test asserts a fixture monster's
    whole object and key list, and another that none of the fixture's trait, action,
    entry, fluff, `_mod`, type-tag or `{@…}` words reach the output.
  - **Copies take only what the index needs.** A `_copy` takes `size`, `type`, `ac`,
    `hp`, `cr`, `dex` and `initiative` from its base (resolved recursively, across files),
    never `srd52` or `page`. A copy of an `srd52` monster is kept.
  - **HP dice that don't parse give `stats: null`, not a failure.** Step 6 said to fail
    on a `stats.hp` that is not `NdM[+-K]`; the output check still does, but a formula
    such as `"2d8 + 3 + 1"` now just leaves that monster without Stats and is counted in
    the summary's "without stats", so one odd row in real data doesn't stop the build.
    Same for special AC (`{ special }`), a missing DEX, and proficiency with an unknown CR.
  - **Labels.** Monsters: `CR x · Size Type`, with multiple sizes as "Large or Huge",
    `{ type: { choose } }` as "Beast or Plant", and swarms as their type. Spells: `Level
    3 Evocation` or `Illusion Cantrip`. Items: rarity (none/unknown dropped), then
    "Wondrous Item" or the type code's name (`M|XPHB` → "Melee Weapon"). An unknown type or
    item code is dropped from the label rather than failing; an unknown size or school
    letter fails, as planned. A row with nothing to say has `label: null`.
  - **`sources`** holds only the abbreviations the index uses, mapped to the book or
    adventure title (or to the abbreviation if neither file names it).
  - **`fiveEToolsVersion`** is null when the checkout has no `package.json` (e.g. `--from`
    a bare `data/` copy).
  - **Not checked against real data.** No 5eTools copy was used; the fixture is
    invented. The first real build may surface new shapes (a size letter, a school, an
    id collision) that fail loudly and need a mapping.
  - **Verify 2 amended**: the fixture itself has `"_copy"` and `bestiary-*.json` files, so
    the grep excludes `scripts/5etools/fixture/`.
- **As built, 21b.** Where it differs from 21b above:
  - **The provider is always registered; with no index it answers nothing.** Registering
    it only when rows loaded would mean reading the configuration inside
    `AddReference()`, before `Build()`, where a test host's settings don't apply yet. So
    `FiveEToolsCatalog` reads `Reference:FiveETools:IndexPath` when DI builds it (a
    factory in `AddReference()`, which keeps its step 20 signature), `Program` builds it
    right after `Build()`, and `FiveEToolsReferenceProvider` returns no matches and no
    summaries while the catalog is empty. `ReferenceCatalog` is unchanged but lists
    `5etools` second. The only visible difference from "not registered" is that an old
    entry's source line reads "5eTools" instead of the raw key when the index is gone.
  - **Missing is off, bad is fatal**, as planned: no path or no file logs one
    information line; unreadable JSON, a `format` other than 1, an unknown `category`, a
    row without `id`/`name`/`source`/`url`, or a duplicate id throws at startup with the
    path in the message. A loaded index logs "5eTools index loaded from … : N items (…
    monsters, … spells, … items)".
  - **Dev config lives in `Properties/launchSettings.json`**, not
    `appsettings.Development.json`, which is git-ignored.
    The `https` profile (`pnpm dev` → `dotnet watch run`) sets
    `Reference__FiveETools__IndexPath=../../.data/5etools/index.json`, resolved from the
    content root (`apps/TakeInitiative.Api`). `dotnet run --no-launch-profile` (the
    HANDOVER's start command) needs that variable set by hand.
  - **`IReferenceProvider` gains `Attribution`**, so the item endpoint can answer a
    search-only item without `Get`. `ReferenceItemResponse.StatBlock` is nullable (the
    JSON has `"statBlock": null`); `ReferenceSummary` gains an optional `Book` ("TST p.
    12", or just the source when there is no page), which `EntrySourceResponse.Detail`
    carries. `sources` (book titles) is read but not exposed yet; 21c can add it if the
    source line's tooltip wants it.
  - **The attribution's `sourceUrl`** is the scheme and host of the rows' links
    (`https://5e.tools/`), so a `--base-url` rebuild moves it too.
  - **Test fixture path.** The synthetic index is
    `apps/TakeInitiative.Api.Tests/Fixtures/5etools-index.json` (the test project has
    `Fixtures/`, not `Resources/`): the 21a fixture's output plus two invented TST
    goblins ("Goblin Boss", "Goblin Tinkerer") so the SRD-first merge can be tested.
    `AuthenticatedWebAppWithDatabaseFixture` pins the path to empty, so every other test
    runs with the provider off whatever the machine has built; `FiveEToolsFixture` points
    it at the fixture.
  - **Web.** Only `schema.d.ts` and a guard on the reference page: an item with no stat
    block shows "Not found" until 21c links it out.
- **As built, 21c.** Where it differs from 21c above:
  - **Book titles reach the web (API change).** The tooltip needs the book's full title,
    which 21b read but did not send. `ReferenceSummary` gains `BookTitle` (from the index's
    `sources`), the item endpoint's summary gains `book` and `bookTitle`, and
    `EntrySourceResponse` gains `bookTitle`. All are null for the SRD. The 21b tests
    assert them, and the leak test adds the fixture's book title to its secrets.
    `schema.d.ts` is regenerated.
  - **The ⌘K row's line** is built on the web from the API's `detail` ("{label} ·
    {book}"): the last part is the book and the label is shortened per category. Monsters
    keep the CR, spells "Level 3" or "Cantrip", and items the whole label ("Item ·
    Uncommon Wondrous Item · XDMG (5eTools)"). A row with no label is "Item · XPHB
    (5eTools)". `referenceHitLine` takes `hasStatBlock` to tell the two shapes apart, so
    SRD rows are unchanged. If `detail`'s shape changes in the API, this parse has to
    change with it.
  - **Enter or a tap on a 5eTools row** calls `window.open(url, "_blank",
    "noopener,noreferrer")` and leaves the sheet open, as planned. Before this, it closed
    the sheet.
  - **The source line** is "↗ From 5eTools · Beholder (MM p. 28) ↗". The name and the
    book together are one link, with a 44px target on a phone. It opens in a new tab with
    `rel="noopener noreferrer"`, and its `title` is the book's full title. It has no
    "[view]". An item that has gone from the index (or an index that is off) shows 📖, the
    stored id, no book and no link. SRD sources are unchanged apart from the helper
    (`sourceLineParts`).
  - **"Use … stats".** The label is `Use {providerLabel} stats`, so the SRD's button
    now reads "Use SRD 5.2 stats". `wantsSourceStats` (the source item is still in the
    data, and the viewer writes Stats) makes the Stats editor read the item up front, and
    the item stays cached. `canUseSourceStats(entry, viewer, itemStats)` shows the button
    only when the item has Stats. So a 5eTools monster without Stats (special HP) gets no
    button, rather than a button that fails, and a spell never gets one (an `Other`
    entry has no Stats). The cost is one cached item request when a DM opens an entry
    that came from the reference. Before, that request waited for the button's click.
  - **+ Wiki** reads the item's Stats for any `Character` suggestion, not only for
    stat-block items, so a DM sees a 5eTools monster's Stats line.
  - **The reference page** for a search-only item shows the name, the row's line and an
    "Open on 5eTools ↗" button (`rel="noopener noreferrer"`), with no card, no
    attribution and no + Wiki. A 404's text is now "That isn't in the reference." rather
    than "…in the SRD".
  - **Also fixed:** the stat-block card header read "AC 15 ·Initiative". Vue dropped the
    newline after the dot's span, so the spaces are now inside the span.
  - **Not checked in a browser.** Verify 6 is for the user (see HANDOVER).
