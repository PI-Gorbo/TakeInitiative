# 20 — SRD reference

## Goal

⌘K finds rules content as well as the campaign. Typing "goblin" shows the wiki's own
Goblin first and then a **REFERENCE** section: `Goblin Warrior  Monster · CR 1/4 · SRD
5.2  [view] [+ Wiki]` (design §11). **[view]** opens a read-only **stat-block card**
laid out the way SRD 5.2 lays out a monster, with the SRD's attribution under it.
**+ Wiki** creates a Character entry from the monster, with `Source` set. For a DM it
also fills **Stats**: initiative from the monster's initiative bonus, max HP from its
hit dice, and AC. So `@Goblin ×4` in a combat gives four goblins with rolled HP and
AC 15, straight from the SRD. The monster data is SRD 5.2 (CC-BY-4.0), transcribed
once by a script, checked in as JSON and embedded in the API. The app makes no network
call for it and it costs nothing (invariant 10).

The reference part sits behind an `IReferenceProvider` abstraction. Step 21 adds the
5eTools index as a second, **search-only** provider: its rows link out, have no
stat-block card, and still offer + Wiki with Stats. That provider is one more
registration.

"Running" at the end: a DM on a desktop searches ⌘K for "goblin", opens the Goblin
Warrior's card, adds it to the wiki as "Goblin" (DM visibility) and adds `@Goblin ×4`
to a combat, which gives four goblins with rolled HP. A player on a phone finds the
Owlbear, reads its card and adds it to the wiki. The player never sees the entry's
source or stats, and the DM fills its stats with one tap.

The step ships as four PRs stacked with `gh stack` on top of this file's docs PR,
which sits on 19e (#244). Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 20a | `v2/20a-srd-data` | The SRD 5.2 data, its catalog and the provider abstraction | 19's app unchanged. The API embeds 331 SRD monsters, and unit tests prove each one loads, matches and derives Stats that roll | [ ] |
| 20b | `v2/20b-reference-api` | Reference search, the item endpoint and + Wiki (API) | The same in the browser. `GET search` has a `Reference` section, `GET reference/srd52/{id}` answers a stat block, and `POST entries/from-reference` creates an entry with `Source` and Stats | [ ] |
| 20c | `v2/20c-stat-block-card` | The stat-block card and REFERENCE in ⌘K | ⌘K shows REFERENCE rows. [view] opens the card page with its attribution | [ ] |
| 20d | `v2/20d-add-to-wiki` | + Wiki, the entry's source line and "Use SRD stats" | + Wiki from ⌘K and from the card, the source line on the entry page, and filling Stats from the source. The step's Verify passes | [ ] |

The 5eTools index (21), spells, magic items, rules text and linking an existing entry
to a reference item are not in 20 (Notes, "Not in 20").

## Depends on

- **17**: `ISearchProvider`, `SearchService`, `SearchSectionKey`/`SearchHitKind` and
  the ⌘K sheet (`components/SearchSheet.vue`, `utils/search.ts`). They were built
  with a Reference section in mind (17's Notes, "Seams for later steps").
- **15g**: `Stats`, `EntryStats` (the read and write rules), `PutEntryStats` and
  `Wiki/StatsEditor.vue`.
- **18**: `CombatantDefaults`, which already turns an entry's Stats into a
  combatant's defaults.

These parts of [12-v2-design-session.md](12-v2-design-session.md) are binding:

- the glossary (§1), including Reference and Source, and the nouns this PR adds;
- §11, "Reference search in ⌘K": wiki results first, a REFERENCE heading,
  `[view]` and `[+ wiki]`, SRD 5.2 bundled with a read-only stat-block card, and
  + wiki creating an entry of the right kind with `Source` set, Stats filled for
  monsters and an empty article;
- the `Source?: { provider, externalId, url }` seam on entries (§11, `Entry.Source`);
- the invariants (§10, especially 5, 8 and 10).

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web), except where they start at the repo root.

**What exists today:**
- The seams: `Entry.Source` (`EntrySource(Provider, ExternalId, Url)`) and `Entry.Links`
  in `src/Features/Entries/Models/Entry.cs`. Both are declared, never written, and left
  out of every response.
- The search side: `ISearchProvider` with its "steps 20 and 21" comments,
  `SearchService.Order`, `SearchSectionKey` and `SearchHitKind` in
  `Search/Api/GetSearch/SearchResponse.cs`, and `AddSearch()` in
  `src/boostrap/Bootstrap.cs`.
- The embedding pattern, `EmbeddedResource` for `Embedded/*.mjml` in
  `TakeInitiative.Api.csproj`.
- The parked `origin/bestiary` and `origin/Bestiary_2025*` branches, a LiteDB 5eTools
  bestiary. Step 21 mines and deletes them; 20 does not touch them.

**This PR (docs)**
- `docs/roadmap/20-srd-reference.md`: this file
- `docs/roadmap/12-v2-design-session.md` §1: the new nouns (step 0 below)
- `docs/roadmap/README.md`: link step 20, `in progress`
- `docs/roadmap/HANDOVER.md`: "Next work" points here

**20a**
- Root add `scripts/srd/build-srd52.mjs`, the transcription script; modify root
  `package.json` (`"srd:build"`)
- Root add `NOTICE` (the SRD 5.2 attribution, and credit to Open5e for the
  transcription)
- API add `Reference/Srd52/monsters.json` (generated, checked in) and
  `Reference/Srd52/source.json` (the pinned commit, the licence and the attribution
  text)
- API modify `TakeInitiative.Api.csproj` (`EmbeddedResource Include="Reference/Srd52/*.json"`)
- API add `src/Features/Reference/{IReferenceProvider,ReferenceItem,StatBlock,ReferenceMatcher,ReferenceStats,ReferenceCatalog}.cs`
- API add `src/Features/Reference/Srd/{SrdMonster,SrdCatalog,SrdReferenceProvider}.cs`
- API modify `src/boostrap/Bootstrap.cs` (`AddReference()`), `Program.cs`, `GlobalUsings.cs`
- Tests add `Scopes/Unit/{SrdCatalogTests,ReferenceMatcherTests,ReferenceStatsTests}.cs`

**20b**
- API add `src/Features/Search/Providers/ReferenceSearchProvider.cs`
- API modify `Search/Api/GetSearch/SearchResponse.cs` (`Reference` section and hit
  kind, `SearchReferenceHit`), `Search/SearchService.cs` (`Order`),
  `Search/Api/GetSearch/GetSearch.cs` (doc comment for `sections`),
  `src/boostrap/Bootstrap.cs` (register the provider last)
- API add `src/Features/Reference/Api/GetReferenceItem/{GetReferenceItem,ReferenceItemResponse}.cs`
- API add `src/Features/Entries/Api/PostEntryFromReference/PostEntryFromReference.cs`
- API modify `src/Features/Entries/Models/Events/EntryCreated.cs` (`EntrySource? Source = null`),
  `Models/Entry.cs` (apply it; comments), `Models/Stats.cs` (`EntrySources.CanRead`
  next to `EntryStats`), `Api/GetEntry/EntryResponse.cs` (`source`),
  `Api/GetEntryHistory/*.cs` (redact `source` on `Created`), `Api/PostEntry/PostEntry.cs`
  (share the duplicate-name check)
- Tests add `Scopes/Integration/Features/Reference/{ReferenceSearchTests,ReferenceItemTests,EntryFromReferenceTests,ReferenceLeakTests}.cs`
- Web `utils/api/schema.d.ts`: regenerated

**20c**
- Web add `pages/app/campaigns/[campaignId]/reference/[provider]/[itemId].vue`
- Web add `components/Reference/{StatBlockCard,StatBlockTraits,ReferenceAttribution}.vue`,
  `utils/reference.ts` (modifiers, the header lines, grouping actions, the item's route),
  `utils/api/reference/getReferenceItemRequest.ts`, `utils/queries/reference.ts`
- Web modify `utils/search.ts` (`SECTION_LABELS.Reference`, `hitKey`, `hitTarget`),
  `components/Search/SearchHitRow.vue` (the reference row), `utils/api/types.ts`
- Web add `tests/unit/reference.test.ts`; modify `tests/unit/search.test.ts`

**20d**
- Web add `components/Reference/{AddToWikiDialog,EntrySourceLine}.vue`,
  `utils/api/entry/postEntryFromReferenceRequest.ts`
- Web modify `components/Search/SearchHitRow.vue` and `components/SearchSheet.vue`
  (the row's + Wiki button), the reference page (+ Wiki),
  `pages/app/campaigns/[campaignId]/wiki/[entryId].vue` (the source line),
  `components/Wiki/StatsEditor.vue` ("Use SRD stats"), `utils/reference.ts`
- Web modify `tests/unit/reference.test.ts`, `tests/unit/mergeClaimStats.test.ts`
- `docs/roadmap/20-srd-reference.md`, `README.md`, `HANDOVER.md`: tick and close the step

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/19e-loose-ends-ui
gh stack add v2/20-srd-plan
```

Add each sub-step on top with `gh stack add v2/20a-srd-data` and so on.

**Glossary check.** Reference and Source are in §1 already. This PR adds the nouns the
step puts into code and UI:

- **Reference item** (code: `ReferenceItem`): one thing a reference source offers, such
  as the SRD's Goblin Warrior. It is not in the wiki until it is added. Not
  "compendium entry" or "monster" as a type name.
- **Reference provider** (code: `IReferenceProvider`): a source of reference items. SRD
  5.2 is bundled (20) and the 5eTools index is search-only (21). One ⌘K search provider,
  `ReferenceSearchProvider`, asks each of them. Not "bestiary".
- **Stat block**: a reference monster's rules text, laid out as the SRD lays it out and
  drawn read-only by the **stat-block card**. Not **Stats**, which is an entry's
  three-field stat line.
- **+ Wiki** (code: `EntryFromReference`): creating an entry from a reference item. It
  gets the right kind and its `Source`, and for a DM, Stats. Not **Promote** (whose UI
  label is "Add to wiki" and which copies a note's text) and not **Import** (§11a).

### 20a. The SRD 5.2 data, its catalog and the provider abstraction

1. **Where the data comes from.** The source is Open5e's transcription of SRD 5.2,
   from `github.com/open5e/open5e-api`. The files are Django fixtures under
   `data/v2/wizards-of-the-coast/srd-2024/`:
   - `Creature.json`: 331 creatures, 235 in category Monsters and 96 in Animals;
   - `CreatureTrait.json`: the traits;
   - `CreatureAction.json`: 989 actions, with `action_type` one of `ACTION`,
     `BONUS_ACTION`, `REACTION` or `LEGENDARY_ACTION`, and `order_in_statblock`.

   The fixtures' `Document.json` names the document "System Reference Document 5.2"
   with `licenses: ["cc-by-40"]`. The script pins one commit (at the time of writing,
   `644ef5c4ebe1c996ee9ced0b42a6d8a2aad861bc` on `staging`) and downloads from
   `raw.githubusercontent.com/open5e/open5e-api/<sha>/…`. A rebuild is a deliberate
   change: bump the sha and review the diff (Notes, "Why Open5e's fixtures").
2. **`scripts/srd/build-srd52.mjs`** is plain Node with no dependencies, run with
   `pnpm srd:build`. It is run by hand, never in CI or at build time. Given `--sha`
   (default: the pinned one), it:
   - joins creatures, traits and actions on `parent`, and sorts actions by
     `order_in_statblock`;
   - writes **our own schema** (below), not Open5e's field names, so the API never
     depends on their model;
   - formats CR as `0`, `1/8`, `1/4`, `1/2`, `1`…`30`. It derives XP and the
     proficiency bonus from the CR with the SRD's tables, because Open5e leaves
     `experience_points_integer` and `proficiency_bonus` null;
   - normalises hit dice (`"20d10 + 40"` → `"20d10+40"`);
   - puts back the **"Hit:"** label that Open5e's text drops. Its Goblin Warrior Scimitar
     reads "Melee Attack Roll: +4, reach 5 ft. 5 (1d6 + 2) Slashing damage…", and the SRD
     reads "… reach 5 ft. Hit: 5 (1d6 + 2) …". The script inserts `Hit: ` after an
     attack roll's reach or range sentence, only where a damage number follows, and
     counts the fixes it made. Diff a sample against the SRD PDF once;
   - drops Open5e's own ids (`srd-2024_…`), keeping the slug as our `id`
     (`goblin-warrior`);
   - fails loudly on anything unexpected: fewer than 300 creatures, a duplicate id or
     name, a creature without AC, HP, hit dice or initiative bonus, or an unknown
     `action_type`;
   - writes `apps/TakeInitiative.Api/Reference/Srd52/monsters.json` as a JSON array
     with **one monster per line**, so a rebuild's diff shows which monsters changed.
     That is about 380 KB, or about 60 KB gzipped. It also writes `source.json`:
     `{ "document": "SRD 5.2", "license": "CC-BY-4.0", "licenseUrl", "sourceUrl",
     "sourceRepo", "sha", "count", "attribution" }`. There is no timestamp, so a rebuild
     at the same sha is byte for byte the same.

   ```jsonc
   // one line of monsters.json (pretty-printed here)
   { "id": "goblin-warrior", "name": "Goblin Warrior", "category": "Monster",
     "size": "Small", "type": "Fey", "alignment": "Chaotic Neutral",
     "ac": 15, "acNote": "natural armor", "initiativeBonus": 2, "hp": 10, "hitDice": "3d6",
     "speed": { "walk": 30, "fly": null, "swim": null, "climb": null, "burrow": null, "hover": false },
     "abilities": { "str": 8, "dex": 15, "con": 10, "int": 10, "wis": 8, "cha": 8 },
     "saves": { "str": -1, "dex": 2, "con": 0, "int": 0, "wis": -1, "cha": -1 },  // 5.2's SAVE column
     "skills": { "stealth": 6 },
     "vulnerabilities": "", "resistances": "", "immunities": "", "conditionImmunities": "",
     "senses": "Darkvision 60 ft.; Passive Perception 9", "languages": "Common, Goblin",
     "cr": "1/4", "xp": 50, "pb": 2,
     "traits":  [],
     "actions": [
       { "kind": "Action", "name": "Scimitar", "text": "Melee Attack Roll: +4, reach 5 ft. Hit: 5 (1d6 + 2) Slashing damage, plus 2 (1d4) Slashing damage if the attack roll had Advantage." },
       { "kind": "BonusAction", "name": "Nimble Escape", "text": "The goblin takes the Disengage or Hide action." } ] }
   ```
3. **Embedded, loaded once.** `TakeInitiative.Api.csproj` embeds
   `Reference/Srd52/*.json`, following the `Embedded/*.mjml` pattern. `SrdCatalog` is a
   singleton. It reads the resource with `System.Text.Json` on first use (`Lazy<T>`),
   holds `SrdMonster[]`, a dictionary by id, and each name folded once. It fails startup
   loudly if the resource is missing: `AddReference()` touches it in `Program.cs` after
   build, the way search's extension check does.
4. **The abstraction** (`src/Features/Reference/`):

   ```csharp
   public interface IReferenceProvider
   {
       string Key { get; }            // "srd52"; step 21: "5etools"
       string Label { get; }          // "SRD 5.2"
       bool HasStatBlocks { get; }    // true: [view] opens our card; false: rows link out (21)
       IReadOnlyList<ReferenceMatch> Search(string text, int take);   // in memory, ranked
       ReferenceItem? Get(string id); // the full item, or null (unknown id, or a search-only provider)
   }

   public record ReferenceSummary(
       string Provider, string Id, string Name, ReferenceCategory Category,  // Monster (21: Spell, Item, …)
       string Detail,                  // "CR 1/4 · Small Fey"
       string? Url,                    // a link out; null for SRD, which is shown in-app
       EntryKind SuggestedKind,        // Monster → Character
       Stats? Stats);                  // what + Wiki fills; on the summary so a search-only provider has it too
   public record ReferenceMatch(ReferenceSummary Item, int Category, double Similarity);
   public record ReferenceItem(ReferenceSummary Summary, StatBlock? StatBlock, ReferenceAttribution Attribution);
   ```

   - `ReferenceCategory` is an enum with one value, `Monster`, in 20.
   - `StatBlock` is our schema above as a C# record, `SrdMonster` mapped 1:1.
   - `ReferenceAttribution` is `{ Text, LicenseName, LicenseUrl, SourceUrl }`, taken
     from `source.json`.
   - `ReferenceCatalog` is the scoped list of registered `IReferenceProvider`s by key.
     `SrdReferenceProvider` is the first, as a singleton.
5. **Matching** (`ReferenceMatcher`, pure). Reference items are not in Postgres, so the
   match is in memory, over folded names (lower case, accents stripped with
   `NormalizationForm.FormD`). It uses the same ladder as `SearchSql.MatchCategory`, so
   the two sections rank alike:
   - 0: exact;
   - 1: prefix;
   - 2: word prefix ("warrior" → Goblin Warrior);
   - 3: substring;
   - 4: fuzzy, for a query of at least 3 characters, when a trigram word similarity
     (pg_trgm's `word_similarity`, ported: the best trigram overlap between the query
     and any run of the candidate's words) reaches `EntryMatchOptions.Default.MinSimilarity`.

   Within a category, higher similarity first, then the shorter name, then the name.
   A scan of 331 names is microseconds. 21's index has thousands, and a linear scan is
   still well under a millisecond (Notes, "In memory, not pg_trgm").
6. **Stats** (`ReferenceStats.From(StatBlock)`, pure):
   - `InitiativeRoll = "1d20+2"`, or `1d20-1`, or `1d20` for a bonus of 0;
   - `MaxHp = hitDice` (`"3d6"`), so HP is rolled when a combatant is added (§8);
   - `Ac = ac`.

   All go through `Stats.Of`, so the shape is the same as `PutEntryStats` stores.
7. **Tests.**
   - `SrdCatalogTests`:
     - the resource loads 331 monsters with unique ids and names;
     - every one has AC, HP and hit dice;
     - **every derived `Stats` passes the real `IDiceRoller.Check`** and is at most
       `Stats.ExpressionMaxLength` long;
     - `source.json` has a sha and the attribution.
   - `ReferenceMatcherTests`: each rung of the ladder, accents, ties, `take`, and
     "gobln" → Goblin Warrior.
   - `ReferenceStatsTests`: a positive, a negative and a zero bonus; hit dice with a
     minus (`"4d8-4"`); Goblin Warrior → `1d20+2 · 3d6 · 15`.

### 20b. Reference search, the item endpoint and + Wiki (API)

1. **The ⌘K section.**
   - `SearchSectionKey.Reference` and `SearchHitKind.Reference`, appended last to both
     enums and to `SearchService.Order`, so wiki results come first (§11).
   - `?sections=reference` works through the existing name check.
   - `ReferenceSearchProvider : ISearchProvider` is registered last in `AddSearch()`.
     It fills `Reference` only when:
     - `query.Scope` is `All`: an `@` query is entries only;
     - and the query is not `SingleCharacter`: one letter matches half the SRD.
   - It asks every provider in `ReferenceCatalog` for `take + 1` and merges them by
     category, then similarity, then provider order (SRD before 5eTools), then name.
     `HasMore` works as in the other sections. It needs no SQL and no connection.

   ```jsonc
   { "kind": "Reference",
     "reference": { "provider": "srd52", "providerLabel": "SRD 5.2", "id": "goblin-warrior",
                    "name": "Goblin Warrior", "category": "Monster", "detail": "CR 1/4 · Small Fey",
                    "url": null, "hasStatBlock": true, "suggestedKind": "Character" } }
   ```

   `Stats` are not in the hit. Nothing about reference items is secret, but the hit
   stays small (17's size budget).
2. **`GET /api/reference/{Provider}/{ItemId}`**, for any signed-in user and with no
   campaign in the route: reference content is the same for every campaign and every
   member.
   - It answers `ReferenceItemResponse { summary, statBlock, attribution }`.
   - An unknown provider or id is a 404, and so is a search-only provider (it has no
     stat block).
   - It sends `Cache-Control: private, max-age=86400`, because the data changes only
     with a deploy.
3. **`Entry.Source` goes live.**
   - `EntryCreated` gains `EntrySource? Source = null`, the way `CreatedFromNoteId` was
     added. Old events read as null, so there is no migration.
   - `Entry.Apply(EntryCreated)` sets it. The "always null in step 15" comments go.
   - `EntrySource.Url` is the SRD's public page (`source.json`'s `sourceUrl`) for SRD
     items. The web links to our own card through `provider` and `externalId`, not
     through `Url`.
4. **Who reads a source** (`EntrySources.CanRead`, next to `EntryStats` in `Stats.cs`):
   the same rule as Stats.
   - On a Character, the claimer's and everyone's if the entry is claimed, and the DMs'
     only if it is unclaimed.
   - On any other kind, everyone who can see the entry.

   An NPC's source *is* its stat block, so "Mysterious Stranger ← SRD Vampire" must not
   reach a player (invariant 8, and Notes, "Source is DM-only on NPCs"). Following the
   rule:
   - `EntryResponse` gains `source?: { provider, providerLabel, externalId, name, url }`
     under that rule, where `name` is the reference item's name at read time and null if
     it has gone from the data;
   - `GetEntryHistory` leaves `source` off a `Created` change unless `CouldReadStats`;
   - `EntrySummaryResponse`, which the `entryUpserted` push sends, gets **no** source.
5. **`POST /api/campaigns/{CampaignId}/entries/from-reference`**, which is + Wiki:

   ```jsonc
   { "provider": "srd52", "itemId": "goblin-warrior", "name": "Goblin", "visibility": "DM" }
   ```

   - It takes `RequireMember`. The item must exist in a registered provider (404 with
     `errors.itemId` otherwise), and search-only providers are allowed (21).
   - The name is `name`, or else the item's name, validated with `EntryName()`.
   - The duplicate check is `PostEntry`'s: a 409 with `errors.existingEntryId`. Move the
     check into a shared `EntryNameRules.ExistingVisible(session, campaignId, member,
     name, ct)`.
   - Kind is `SuggestedKind`, and edit access is `Anyone`.
   - One `SaveChangesAsync` appends `EntryCreated(… Source)` and, **when the caller
     could write the new entry's Stats** (`EntryStats.CanWrite`, which for a new
     unclaimed Character means a DM), `EntryStatsChanged(summary.Stats)`. For a player
     the entry is created without Stats. A DM fills them later (20d's "Use SRD stats").
   - It sends the existing pushes (`NotifyEntryUpserted`, and `entryStatsChanged` to
     readers) and answers `EntryResponse` as the caller sees it.
6. **Tests.**
   - `ReferenceSearchTests`:
     - "goblin" gives Entries (a wiki Goblin) before Reference, whose first rows are
       Goblin Warrior, Goblin Boss and Goblin Minion;
     - `sections=reference`, `take` and `hasMore` work;
     - `@goblin` and `g` have no Reference section;
     - a campaign with no entries still gets Reference.
   - `ReferenceItemTests`: 200 with a stat block and attribution, 404 for an unknown
     id or provider, 401 when signed out, and the cache header.
   - `EntryFromReferenceTests`:
     - a DM gets a Character with `source` and Stats `1d20+2 · 3d6 · 15`, and
       `POST combatants` with it ×4 gives four combatants with HP in 3–18 and AC 15;
     - a player gets the entry with no Stats, and `PUT stats` by the DM afterwards
       works;
     - a renamed entry ("Goblin") keeps the source;
     - a duplicate name gives a 409 with the existing id;
     - a bad visibility or name gives a 400.
   - `ReferenceLeakTests`, for a DM-created, unclaimed entry from a reference item:
     - a player who can see it (it is `Everyone`) gets no `source` from `GET entry`,
       `GET entries` or `GET entry history`;
     - `entryUpserted` has no source at all;
     - `GET search` for the entry's name never returns its source.

     Claiming the entry shows the source to its claimer and the table.

### 20c. The stat-block card and REFERENCE in ⌘K

1. **The route.** `pages/app/campaigns/[campaignId]/reference/[provider]/[itemId].vue`
   has one element root (HANDOVER, "Pages and layouts").
   - It sits inside the campaign, so the tabs, ⌘K and + Wiki's campaign are all there.
     The data itself is campaign-free.
   - The Wiki tab does not light up for it. The header shows "← Back"
     (`router.back()`, or the Wiki when there is no history).
   - `getReferenceItemQuery(provider, id)` in `utils/queries/reference.ts` has
     `staleTime: Infinity`.
   - An unknown item shows "That isn't in the SRD." with a link to ⌘K.
2. **`Reference/StatBlockCard.vue`** draws SRD 5.2's layout, in this order:
   - the name, then "Small Fey, Chaotic Neutral";
   - **AC** 15 · **Initiative** +2 (12), where the passive number is 10 + the bonus as
     in 5.2;
   - **HP** 10 (3d6);
   - **Speed** 30 ft., Fly 60 ft.;
   - the ability table, with 5.2's MOD and SAVE columns: three columns on a phone
     (STR DEX CON / INT WIS CHA) and six from `md`;
   - Skills, Vulnerabilities, Resistances, Immunities, Senses and Languages, each only
     when set;
   - **CR** 1/4 (XP 50; PB +2);
   - then `StatBlockTraits.vue` sections for Traits, Actions, Bonus Actions, Reactions
     and Legendary Actions, each only when it has rows. A row is a bold italic name
     followed by the text.

   The text is rendered as **text** (Vue interpolation), never `v-html` or markdown.
   The one exception is `_…_` italics (Notes, "Plain text, plus one italic"). The card uses the app's card tokens, with a thin
   accent-coloured rule under the name like the printed stat block, and works in the
   one dark theme. `utils/reference.ts` holds the pure parts: `modifier(10) → "+0"`,
   the type line, the speed line, the initiative line and grouping actions by kind.
3. **`Reference/ReferenceAttribution.vue`**, under every card, in small muted text: the
   SRD 5.2 attribution statement, exactly as `source.json` gives it (Notes,
   "Attribution"), with its links. The API sends it, so the web holds no copy.
4. **⌘K.**
   - `SECTION_LABELS.Reference` is `{ header: "Reference", more: "Show more reference" }`.
   - `hitKey` is `reference-{provider}-{id}`.
   - `hitTarget` goes to the card's route when `hasStatBlock`. Otherwise, for 21, it is
     the `url` in a new tab: `SearchTarget` gains `external?: string`, and
     `SearchSheet.vue` opens it with `window.open(…, "_blank", "noopener")`.
   - `SearchHitRow.vue` draws a reference row as 📖, the name, then "Monster · CR 1/4 ·
     SRD 5.2" muted. Enter or a tap is [view]. The + Wiki button comes in 20d.
5. **Tests.**
   - `reference.test.ts`: modifiers (1 → −5, 30 → +10), the initiative line, the
     speed line with hover, the type line, the `_italic_` splitter, action grouping and
     order, and the route builder.
   - `search.test.ts`: the Reference rows come after Combats, and there is an external
     target for a hit with a `url` and no stat block.

### 20d. + Wiki, the entry's source line and "Use SRD stats"

1. **`Reference/AddToWikiDialog.vue`**, the shadcn `Dialog` that fits its content, like
   the Promote dialog after #223.
   - Name is prefilled with the item's name and editable ("Goblin Warrior" → "Goblin").
   - The kind is shown, not picked: "Character".
   - Visibility is a `ChoiceChips` of Everyone / DM / Me. It defaults to **DM for a DM**
     (prep before the players meet it) and **Everyone for a player**.
   - A player sees the line "A DM can add its stats." A DM sees "Stats: 1d20+2 · 3d6 ·
     AC 15".
   - **Add** posts `from-reference`, toasts "Goblin added to the wiki" with **Open**,
     and invalidates the entry list.
   - On a 409 the dialog shows "There's already an entry called Goblin." with **Open
     it** and a rename field. It does not overwrite.
2. **Ways in.**
   - The card page has a **+ Wiki** button under the name.
   - Each ⌘K reference row has a trailing **+ Wiki** button: 44 px on a phone, reachable
     with Tab, with the aria-label "Add Goblin Warrior to the wiki".
   - In ⌘K, ⌘Enter on the active reference row opens the dialog too. `SearchSheet.vue`
     closes first, then opens the dialog.
3. **`Reference/EntrySourceLine.vue`** on `wiki/[entryId].vue`, under the header and
   above Stats, when `entry.source` is present: "📖 From SRD 5.2 · Goblin Warrior
   [view]". It links to the card for a provider with stat blocks, and to `url` otherwise
   (21).
4. **"Use SRD stats"** in `Wiki/StatsEditor.vue`, when `entry.source` has a stat block
   and the viewer can write Stats. The button reads the reference item (its query) and
   fills the form with `ReferenceStats`' values. The API sends them on `summary.stats`,
   so the web does not derive its own. The user reviews them and Saves, which is the
   normal `PUT stats`. This is also how a DM fills the Stats of an entry a player
   added. It adds no endpoint.
5. **Tests.**
   - `reference.test.ts`: the dialog's default visibility per role, and the 409 path's
     state (a pure helper in `utils/reference.ts`).
   - `mergeClaimStats.test.ts`: when "Use SRD stats" shows.
6. **Close the step**: tick this file's PR table, set README's status to `done`, and
   update HANDOVER.

## Verify

1. `dotnet test` and `pnpm build` pass, `vitest` and `npx nuxi typecheck` pass,
   `schema.d.ts` is fresh, and CI is green on every PR in the stack.
2. `pnpm dev` on an existing dev database starts cleanly. No schema change is expected:
   `EntryCreated` gains a nullable field, and Marten stores JSON. If Marten reports a
   schema change, it is a mistake.
3. `pnpm srd:build` on a clean checkout reproduces `monsters.json` byte for byte at the
   pinned sha.
4. Two browser profiles: A (the owner, DM) at 1280 × 800, and B (a Player) at 390 × 844.
   The wiki has an entry called Goblin (`Everyone`, no source).
   1. A presses ⌘K and types "goblin". ENTRIES shows Goblin first. REFERENCE shows
      Goblin Warrior ("Monster · CR 1/4 · SRD 5.2"), Goblin Boss (CR 1) and Goblin
      Minion (CR 1/8).
      `@goblin` shows no REFERENCE, and neither does "g".
   2. A opens Goblin Warrior. The card reads like the SRD page: AC 15, Initiative +2
      (12), HP 10 (3d6), the ability table, Scimitar and Shortbow under Actions, and
      Nimble Escape under Bonus Actions. The attribution is under it. Back returns to
      where A was.
   3. A presses + Wiki with the name "Goblin". A 409 offers the existing Goblin. A
      renames it to "Goblin Warrior", with visibility DM, and adds it. The entry page
      shows "📖 From SRD 5.2 · Goblin Warrior [view]" and Stats `1d20+2 · 3d6 · AC 15`.
   4. In a Draft combat, A adds `@Goblin Warrior ×4`. That gives four combatants, each
      with HP between 3 and 18, AC 15 and initiative roll `1d20+2`.
   5. On B's phone, ⌘K "owlbear" → REFERENCE Owlbear → the card is readable at 390 px
      (three-column ability table, no sideways scroll). B presses + Wiki, which defaults
      to Everyone, and adds it. B's entry page shows no source line and no Stats.
      B's `GET entry` JSON has no `source` key.
   6. A opens Owlbear. The source line shows, and so does "Use SRD stats" in Stats. One
      tap fills the form, and Save sets the Stats. B still sees neither.
   7. On B's phone, the + Wiki button on a ⌘K row and the dialog are reachable by
      touch, and the dialog is not under the keyboard.

## Notes / gotchas

- **Why Open5e's fixtures.** SRD 5.2 is published by Wizards as a PDF only (on D&D
  Beyond), under CC-BY-4.0.
  - Parsing the PDF well is a project of its own.
  - Open5e's `srd-2024` fixtures are the most complete machine-readable transcription
    we found: all 331 creatures, with 5.2's initiative bonus, actions split by kind,
    and the document tagged `cc-by-40`.
  - `5e-bits/5e-database` has a `src/2024` tree, but its monster coverage was not
    checked, and its API shape is 2014's.

  The script keeps only SRD text, so the licence that matters is the SRD's. Open5e's
  repository reports its licence as "Other" on GitHub. **The user should check it
  before 20a merges.** If it forbids reuse of the fixtures, the fallback is the same
  script reading the SRD 5.2 PDF's text (`pdftotext -layout`) with a hand-checked parser,
  which is much more work. Open5e is credited in `NOTICE` either way, as a courtesy.
- **SRD 5.2 vs 5.2.1.** Wizards issued 5.2.1 with errata in 2025, and Open5e's document
  is labelled "5.2". The UI says "SRD 5.2" because that is what the data is. When Open5e
  moves to 5.2.1, the rebuild bumps the sha and the label in `source.json`, and the label
  flows from there.
- **Attribution.** CC-BY-4.0 needs the attribution shown "in any reasonable manner",
  and SRD 5.2 supplies its own statement: *"This work includes material from the System
  Reference Document 5.2 ("SRD 5.2") by Wizards of the Coast LLC, available at
  https://www.dndbeyond.com/srd. The SRD 5.2 is licensed under the Creative Commons
  Attribution 4.0 International License, available at
  https://creativecommons.org/licenses/by/4.0/legalcode."*
  - It is under every stat-block card, in `NOTICE` at the repo root, and in
    `source.json`.
  - The ⌘K row's "SRD 5.2" names the source; the card carries the licence.
  - The script changes the text's form (our schema) but not its words. `NOTICE` says
    that the data was reformatted. That satisfies the licence's "indicate if changes
    were made".
  - Never use Wizards' or D&D's logos or trade dress, and never imply endorsement. The
    licence does not grant trademarks.
- **Checked in and embedded, not in Postgres.** It is 380 KB of read-only data that
  changes only with a deploy.
  - An embedded resource needs no migration, no seeding and no table, and every test
    host has it.
  - The API's DLL grows by about 380 KB and the web bundle by nothing: the card is
    fetched per item.
  - Nothing reaches the network at run time (invariant 10).
- **In memory, not pg_trgm.** Reference items are the same for every campaign and have
  no visibility, so SQL buys nothing. The ported ladder keeps the Reference section's
  ranking in step with Entries.
  - If 21's index is too big for a linear scan (unlikely below 50k names), it can build
    a trigram index in memory behind the same `Search`.
  - If it has to go to Postgres, `IReferenceProvider.Search` becomes async. That is a
    small change while only two providers exist.
- **Source is DM-only on NPCs.** Stats on an unclaimed Character are combat secrets
  (15g, invariant 8), and the source says which stat block it is, so it follows the same
  read rule. A reskinned monster stays a mystery to the players.
  - Reference search is **not** secret. Anyone can read the SRD, and a player finding
    "Vampire" in ⌘K learns nothing about the campaign.
  - Only the link between an entry and an item is guarded.
- **Stats are filled for DMs only.** `EntryStats.CanWrite` says only a DM writes an
  unclaimed Character's Stats. + Wiki does not bypass that for a player, even though the
  numbers come from the SRD rather than the player. The DM gets one-tap "Use SRD stats".
  When a player claims an entry made from a reference item (a druid's beast form, say),
  the claimer can use the button too.
- **HP is the hit dice, rolled per combatant.** §8 rolls HP on add, and `Stats.MaxHp` is
  already a dice expression. A DM who wants the average types `10`. A test checks that
  every monster's hit dice pass `IDiceRoller.Check`, so a bad transcription fails CI,
  not a combat.
- **Monsters are Characters.** Stats exist only on `Character` (15g), and `@Goblin ×4`
  needs Stats. The kind is fixed in the dialog. Changing it later drops the Stats, as
  it does today.
- **Plain text, plus one italic.** Open5e's text has no HTML. Its only markup is
  `_Trigger:_` and `_Response:_` in reactions (Goblin Boss's Redirect Attack).
  - `utils/reference.ts` splits `_…_` runs into `<em>` spans as text nodes, and
    everything else is rendered as text.
  - A bad transcription can never inject markup.
  - The bold italic names come from the structure, not from markup.
  - The build script fails on any other markdown character pattern it has not seen
    before (`**`, `[`, `<`), so a new kind of markup is noticed at build time, not in
    the UI.
- **The article starts empty** (§11). A + Wiki entry mentioned in a note is then an
  `EmptyArticle` loose end, as intended: the source is not a substitute for what
  happened at the table. The card is one tap away through the source line.
- **No updates flow into entries.** A rebuild of the data changes cards, not entries.
  An entry's Stats are its own once filled. "Use SRD stats" is how you re-apply them.
  An item that has gone from the data shows the source line with its stored id and no
  [view].
- **Seams for step 21.**
  - `FiveEToolsReferenceProvider` has `HasStatBlocks = false` and its summaries carry
    `Url` (the 5etools deep link) and `Stats` (HP, AC and initiative from DEX, from the
    local index).
  - It registers in `AddReference()`. Search, + Wiki and the source line work unchanged:
    `hitTarget` links out, and the source line links to `url`.
  - `GET reference/5etools/{id}` is a 404 by design: **the app never shows 5eTools
    content** (§11).
  - `ReferenceCategory` gains `Spell`, `Item` and so on. `SuggestedKind` maps an item to
    `Item`, and anything without Stats to its kind with `Stats = null`.
  - 21 deletes the Bestiary branches. 20 leaves them.
- **Not in 20:**
  - SRD spells, magic items, equipment, conditions and rules text (the fixtures have
    them; each needs its own card and kind mapping);
  - linking an existing entry to a reference item, and refreshing Stats from the source
    automatically;
  - monster images or tokens (the SRD has none);
  - SRD 5.1 (2014) content;
  - homebrew or campaign-specific reference sources;
  - reference items in connections, the graph or loose ends (they are not entries);
  - adding a reference monster straight to a combat without an entry (§11 goes through
    + Wiki).
