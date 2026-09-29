# 22 — D&D Beyond link

## Goal

A player character can carry its **D&D Beyond sheet URL** (design §11, "D&D Beyond"). The
entry page shows "↗ D&D Beyond sheet" under the claim line, and the link out always works,
for everyone who may see it. Nothing is fetched to show it.

When the deployment turns it on, the claimer or a DM can press **Refresh from D&D Beyond**.
The API fetches that one sheet's public character JSON once, server side, and answers a
**preview**: the character's name, a level and class summary, and the three **Stats**
fields (initiative roll, max HP, AC). The preview fills the Stats editor the way "Use SRD
5.2 stats" does (step 20d), and **Save is the normal `PUT stats`**. Nothing from D&D
Beyond is stored except the link and whatever numbers the user chose to save. There is no
live sync, no polling, no background job and no bulk fetch.

**The fetch is unofficial.** D&D Beyond has no public API. The endpoint this step uses is
the undocumented one that community tools use, it answers only for sheets set to Public,
it may change or disappear without notice, and using it may conflict with D&D Beyond's
Terms of Service (Notes, "D&D Beyond's terms"). So the fetch is **behind a config flag
that is off by default**, in dev and in production. With it off, the step is a link and
hand-entered Stats, which is the no-fetch fallback and needs nothing from D&D Beyond.

"Running" at the end: with the flag off, a player claims "Thorin", pastes
`https://www.dndbeyond.com/characters/12345678/thorin`, and the entry shows "↗ D&D Beyond
sheet", which opens the sheet in a new tab. They type Stats by hand and `@Thorin` joins a
combat with them. With the flag on (the user's own machine, against a real public sheet),
the same player presses Refresh, sees "Thorin Oakenshield · Level 5 · Fighter 5 ·
Initiative 1d20+1 · HP 44 · AC 18", presses "Use these", then Save, and the Stats change.
A private sheet gives "This sheet isn't public on D&D Beyond" and the link stays.

The step ships as three PRs stacked with `gh stack` on top of this file's docs PR, which
sits on 21c (#253). Each PR leaves the app runnable.

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 22a | `v2/22a-ddb-link-api` | The sheet link on a Character entry (API) | `PUT entries/{id}/dndbeyond` stores or clears a validated sheet link; `GET entry` has `dndBeyond` for those who may read it; history and pushes know about it. No network code | [ ] |
| 22b | `v2/22b-ddb-refresh-api` | The manual refresh, behind a flag (API) | With `DndBeyond:Refresh:Enabled` off (the default), `POST …/dndbeyond/refresh` is 404 and nothing calls out. With it on, it answers a preview, rate-limited, with a timeout and a size cap. Tests use an invented fixture and never touch the network | [ ] |
| 22c | `v2/22c-ddb-web` | The sheet line, its editor and "Refresh from D&D Beyond" (web) | The entry page shows and edits the link; Refresh (when offered) fills the Stats editor. The step's Verify passes | [ ] |

Portraits, any other sheet content, private sheets, D&D Beyond logins, other sheet sites
and automatic refresh are not in 22 (Notes, "Not in 22").

## Depends on

- **15**: Character entries, **Claim** (`PutEntryClaim`, `ClaimedByMemberId`,
  `ClaimControl`), **Stats** (`Stats.cs`, `EntryStats.CanRead`/`CanWrite`,
  `PutEntryStats`, `StatsEditor`), entry history (`GetEntryHistory`), merge
  (`EntryAbsorbed`) and the entry pushes (`EntryHub`). The `Entry.Links` /
  `EntryLink(Url, Label)` seam declared in 15 and never written.
- **18**: `CombatantDefaults` reads Stats when an entry joins a combat, and "Add my
  character" (`Combat/AddMyCharacter.vue`). Neither changes: a refreshed sheet reaches
  combat only through saved Stats.
- **20/21**: the pattern this copies. `EntrySources.CanRead` (source is DM-only on an
  unclaimed Character), the source line (`Reference/EntrySourceLine.vue`), and "Use …
  stats" (`wantsSourceStats`/`canUseSourceStats` in `utils/entries.ts`), which fills the
  Stats form and leaves the write to `PUT stats`.

README lists 15 as the only dependency; 20 and 21 are already on the stack, and 22 reuses
their web pieces rather than needing them.

These parts of [12-v2-design-session.md](12-v2-design-session.md) are binding:

- §11, "D&D Beyond": the sheet URL is stored as a `Link`; the link out always works; the
  refresh is a manual button that reads the public character JSON; it fails gracefully and
  the link stays; no live sync;
- §4, "Player characters": Stats on a claimed entry are written by its claimer and the DMs,
  on an unclaimed one by the DMs only;
- the glossary (§1): Player character, Claim, Stats, Source, Link (§11);
- the invariants (§10): 5 (visibility on the server; the link follows the Stats rule), 8
  (an NPC's numbers never reach a player), 9 (every event carries an Actor), 10 (no paid
  services) and 11 (every control works on a phone).

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web), except where they start at the repo root.

**What exists today:**
- `src/Features/Entries/Models/Entry.cs`: `Links` (always empty) and `EntryLink(Url,
  Label)`, "the §11 seam", left out of every response. `Source` is set once, at creation, by
  + Wiki.
- `Stats.cs`: `Stats { InitiativeRoll, MaxHp, Ac }`, `EntryStats` and `EntrySources`.
- `Api/PutEntryStats/PutEntryStats.cs`: the write, its 409 for a non-Character and its 403
  wording, `DiceExpressionRules`, and `NotifyEntryStatsChanged`.
- `Api/GetEntryHistory/GetEntryHistory.cs`: `CouldReadStats` drops an unclaimed
  Character's stats changes from a player's history.
- No `HttpClient`, `IHttpClientFactory` or rate limiter anywhere in the API yet.
- Web: `pages/app/campaigns/[campaignId]/wiki/[entryId].vue` stacks
  `ReferenceEntrySourceLine`, `WikiClaimControl` and `WikiStatsEditor`; the Stats editor
  already takes values from outside ("Use … stats") and saves with `PUT stats`.

**This PR (docs)**
- `docs/roadmap/22-ddb-link.md`: this file
- `docs/roadmap/README.md`: link step 22, `in progress`
- `docs/roadmap/HANDOVER.md`: "Next work" points here, and the stack table gains this PR

**22a**
- API add `src/Features/Entries/DndBeyond/DndBeyondSheet.cs` (URL parsing and the
  canonical link) and `src/Features/Entries/Api/PutEntryDndBeyond/PutEntryDndBeyond.cs`
- API add `Models/Events/{EntryLinkAdded,EntryLinkRemoved}.cs`
- API modify `Models/Entry.cs` (`EntryLink` gains `Provider`; the two `Apply`s; merge),
  `Models/Events/EntryAbsorbed.cs` (`FromLinks`), `Stats.cs` (`EntryLinks` read rule),
  `Api/GetEntry/EntryResponse.cs` (`DndBeyond`), `Api/GetEntryHistory/GetEntryHistory.cs`,
  `Api/PostEntryMerge/*`, `EntryHub.cs`
- Tests add `Scopes/Unit/DndBeyondSheetTests.cs`,
  `Scopes/Integration/Features/Entries/DndBeyondLinkTests.cs`
- Web `utils/api/schema.d.ts`: regenerated

**22b**
- API add `src/Features/Entries/DndBeyond/{DndBeyondOptions,DndBeyondClient,DndBeyondCharacter,DndBeyondRefreshLimiter}.cs`
  and `src/Features/Entries/Api/PostEntryDndBeyondRefresh/PostEntryDndBeyondRefresh.cs`
- API modify `src/boostrap/Bootstrap.cs` (`AddDndBeyond(configuration)`), `Program.cs`,
  `appsettings.json` (the section, `Enabled: false`), `Api/GetEntry/EntryResponse.cs`
  (`canRefresh`)
- Tests add `Fixtures/dndbeyond/*.json` (**invented** characters), a
  `FakeDndBeyondHandler`, `Scopes/Unit/DndBeyondCharacterTests.cs`,
  `Scopes/Integration/Features/Entries/DndBeyondRefreshTests.cs`
- Root modify `NOTICE` (a D&D Beyond paragraph)
- Web `utils/api/schema.d.ts`: regenerated

**22c**
- Web add `components/Wiki/DndBeyondLine.vue`, `components/Wiki/DndBeyondRefresh.vue`,
  `utils/dndBeyond.ts`
- Web modify `pages/app/campaigns/[campaignId]/wiki/[entryId].vue`,
  `components/Wiki/StatsEditor.vue` (take a preview's values), `utils/entries.ts`
  (`describeChange` for the link events), the entry pushes' handler
- Web add `tests/unit/dndBeyond.test.ts`; modify `tests/unit/mergeClaimStats.test.ts`
- `docs/roadmap/22-ddb-link.md`, `README.md`, `HANDOVER.md`: tick and close the step

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/21c-5etools-web
gh stack add v2/22-ddb-plan
```

Add each sub-step on top with `gh stack add v2/22a-ddb-link-api` and so on.

**Glossary check.** No new glossary nouns. The stored thing is a **Link** (§11), and the UI
calls it "D&D Beyond sheet". It is **not** the entry's **Source**: `Source` is set once at
creation by + Wiki and names a reference item; a sheet link is added and removed later
(Notes, "Link, not Source"). "Refresh" is the button's verb, not a noun; there is no "sync"
anywhere, in code or UI.

### 22a. The sheet link (API)

1. **Parsing: `DndBeyondSheet.TryParse(string url, out DndBeyondSheet sheet)`.** Accepts
   only these, over `https` (an `http` URL is upgraded), with or without `www.`, a
   trailing slug, a trailing slash, a query or a fragment:
   - `https://www.dndbeyond.com/characters/{id}`
   - `https://www.dndbeyond.com/characters/{id}/{slug}`
   - `https://www.dndbeyond.com/profile/{user}/characters/{id}`
   - `https://ddb.ac/characters/{id}/{slug}` (D&D Beyond's short links)

   `{id}` is 1–12 digits. Anything else (another host, a campaign or builder URL, a
   `javascript:` URL, a user-info part, a port) is rejected. The result is the id as a
   `long` and the **canonical URL** `https://www.dndbeyond.com/characters/{id}`, which is
   what is stored and linked. The pasted text is not stored. Because only the id survives,
   22b can never be steered at another host (Notes, "Why only an id").
2. **Storage: a `Link`.** `EntryLink` becomes `EntryLink(string Url, string? Label, string?
   Provider = null, string? ExternalId = null)`; a sheet link is `(canonical url, "D&D
   Beyond", "dndbeyond", "{id}")`. An entry has **at most one** `dndbeyond` link.
   - Events: `EntryLinkAdded(Actor, EntryLink Link)` and `EntryLinkRemoved(Actor, string
     Provider)`. Replacing a link is Removed then Added in one append. Both leave
     `UpdatedAt` alone, like `EntryStatsChanged` (the summary everyone sees must not move
     when an NPC's link changes).
   - Merge: `EntryAbsorbed` gains `FromLinks` (default empty, so old events replay). The
     winner keeps its own `dndbeyond` link and takes the loser's only if it has none, as
     with `ClaimedByMemberId` and `Stats`.
3. **Who reads and writes it: the Stats rules.** A new `EntryLinks` next to `EntryStats` in
   `Stats.cs`:
   - `CanRead` = `EntryStats.CanRead` (a Character; claimed → everyone who sees it;
     unclaimed → the DMs only). An NPC's sheet link tells a player what the DM ran, so it
     is hidden the same way (invariant 8).
   - `CanWrite` = `EntryStats.CanWrite` (the claimer and the DMs; on an unclaimed one the
     DMs only).
   - A link is kept if the entry's kind changes away from Character, and hidden while it
     is not one, exactly as Stats are.
4. **`PUT /api/campaigns/{CampaignId}/entries/{EntryId}/dndbeyond`** with `{ url: string |
   null }`. Null or blank removes the link.
   - 409 `errors.kind` "Only a Character can link a D&D Beyond sheet." for other kinds.
   - 403 with `PutEntryStats`' two messages, reworded for the link.
   - 400 `errors.url` "That isn't a D&D Beyond character link. Copy it from the sheet's
     address bar or its Share button." for anything `TryParse` rejects. At most 500
     characters before parsing.
   - The same link again appends nothing. The response is the caller's `EntryResponse`.
   - Push: `entryLinksChanged` to the members who may read it (reuse
     `NotifyEntryStatsChanged`'s audience code, generalised).
5. **The response.** `EntryResponse.DndBeyond: { url, characterId }`, with
   `[JsonIgnore(WhenWritingNull)]` like `Source`, so a player's JSON for an NPC does not
   even have the key. `Links` as a list stays out of the response until something else
   uses it.
6. **History.** `EntryChangeType` gains `DndBeyondLinked` (with the url) and
   `DndBeyondUnlinked`, filtered with `CouldReadStats`, so a player's history of an NPC
   has neither.
7. **Tests.**
   - `DndBeyondSheetTests`: every accepted shape gives the same canonical URL and id;
     rejects other hosts, `dndbeyond.com.evil.test`, `http://user@www.dndbeyond.com/…`,
     ports, a campaign URL, a 13-digit id, `javascript:`, and whitespace-only input.
   - `DndBeyondLinkTests`: the claimer sets, replaces and clears it; another player gets
     403; a DM links an unclaimed NPC and a player's `GET entry`, entry list, history and
     `GET search` JSON never contain `dndbeyond` or the id (the leak test from 20b, with
     the link as the secret); merge keeps the winner's link; a Place gets 409.

### 22b. The manual refresh, behind a flag (API)

1. **Configuration: `DndBeyondOptions`**, section `DndBeyond:Refresh`
   (`DndBeyond__Refresh__Enabled` in the environment):

   | Key | Default | Meaning |
   |---|---|---|
   | `Enabled` | `false` | The whole refresh. Off: the endpoint is 404, `canRefresh` is false, and no `HttpClient` is ever used |
   | `BaseUrl` | `https://character-service.dndbeyond.com/character/v5/character/` | The one URL the client calls, with the id appended. One constant so a move is one config change |
   | `TimeoutSeconds` | `10` | The whole request, headers and body |
   | `MaxResponseBytes` | `4_000_000` | Bigger answers are cut off and fail |
   | `PerEntryCooldownSeconds` | `60` | One real fetch per character id per window; a second press in the window answers the cached preview |
   | `PerMemberPerHour` | `20` | Refreshes a member may start in an hour |
   | `PerServerPerHour` | `120` | A ceiling for the whole deployment |

   `appsettings.json` has the section with `Enabled: false`. `launchSettings.json` does
   **not** turn it on for `pnpm dev`; the user sets `DndBeyond__Refresh__Enabled=true`
   by hand when they want it (Notes, decision 1).
2. **The call: `DndBeyondClient`**, a typed `HttpClient` (`AddHttpClient<DndBeyondClient>`),
   registered only when `Enabled` is true.
   - `GET {BaseUrl}{id}`, the id being 22a's parsed `long`. No other host, path or query
     is ever built. `Accept: application/json`; a `User-Agent` of
     `TakeInitiative/{version} (+https://github.com/PI-Gorbo/TakeInitiative)`; **no
     cookies, no auth header, no D&D Beyond token of any kind**.
   - `HttpClient.Timeout` from the options, a `CancellationToken` linked to the
     request's, `MaxResponseContentBufferSize` from the options, and redirects off
     (`AllowAutoRedirect = false`: a redirect is a failure, not a hop to somewhere else).
   - No retries. One press is at most one request.
3. **The rules for calling it: `DndBeyondRefreshLimiter`**, a singleton in memory:
   - the per-id cooldown doubles as a cache: within it, the last good preview for that id
     is answered with no request, and a failure is cached too (so hammering a private
     sheet sends one request a minute);
   - per member and per server, fixed hourly windows; over either, 429 with `Retry-After`
     and "Too many refreshes. Try again in N minutes.";
   - at most two requests to D&D Beyond in flight across the server (a semaphore); a
     third waits up to the timeout, then 503.

   In memory is enough for one API instance; a second instance would double the ceilings
   (Notes, "Other notes").
4. **The endpoint: `POST /api/campaigns/{CampaignId}/entries/{EntryId}/dndbeyond/refresh`**,
   no body. `POST`, because it makes an outbound request and is not cacheable (and it
   avoids the FastEndpoints GET quirk).
   - 404 when `Enabled` is false (the route does not exist as far as a client can tell).
   - The entry must be visible, a Character, have a `dndbeyond` link, and the caller must
     pass `EntryLinks.CanWrite`; otherwise 404, 409 (`errors.kind`, `errors.link` "Link a
     D&D Beyond sheet first.") or 403. **Writing it is refreshing it**: a player who may
     only read a claimed character's link cannot start fetches for it.
   - It **writes nothing**: no event, no push. It answers a preview:

     ```jsonc
     { "characterId": "12345678", "name": "Thorin Oakenshield",
       "summary": "Level 5 · Fighter 5",
       "stats": { "initiativeRoll": "1d20+1", "maxHp": "44", "ac": 18 },
       "warnings": ["AC counts armour, a shield and flat bonuses only. Check it."],
       "fetchedAt": "2026-10-01T12:00:00Z", "cached": false }
     ```
   - Failures answer a problem with a message the web shows as is, and the link stays:

     | D&D Beyond answers | Status | Message |
     |---|---|---|
     | 403, 404, or `success: false` | 422 | "This sheet isn't public on D&D Beyond. Set it to Public there, or enter the stats by hand." |
     | a timeout | 504 | "D&D Beyond didn't answer. Try again later, or enter the stats by hand." |
     | 429 | 503 | "D&D Beyond is busy. Try again later." |
     | any other status, a redirect, too big, not JSON, or no usable `data` | 502 | "D&D Beyond's answer wasn't one we understand. Enter the stats by hand." |

   - **Logs**: the character id, the status code, the duration and the outcome. Never the
     body, never the name.
5. **What is read: `DndBeyondCharacter.From(JsonElement)`**, an allowlist, field by field,
   from `data`. It never keeps the document; the `JsonDocument` is disposed after mapping.
   The shape below is what community tools describe for `character/v5`; it is **not
   checked against a live answer** by this plan (Notes, "Not checked against real data").
   - `name`: string, trimmed, at most `Entry.NameMaxLength`.
   - `classes[]`: `level` and `definition.name` → `summary` "Level 5 · Fighter 5", or
     "Level 5 · Wizard 3 / Fighter 2" (highest level first). Subclasses are left out.
   - Ability scores, for DEX and CON only: `overrideStats[id]` if set, else `stats[id]` +
     `bonusStats[id]` + every `modifiers.*[]` with `type: "bonus"` and `subType:
     "dexterity-score"` / `"constitution-score"`. `id` 2 is DEX, 3 is CON.
   - **Initiative** → `"1d20+{DEX mod + initiative bonuses}"` (`"1d20"` at 0, `"1d20-1"`
     below). Bonuses are `modifiers.*[]` with `type: "bonus", subType: "initiative"` and a
     numeric `value`. Anything else that affects initiative (half proficiency, advantage)
     adds a warning, not a number.
   - **Max HP** → `overrideHitPoints` if set, else `baseHitPoints + bonusHitPoints + (CON
     mod + per-level HP bonuses) × total level`, per-level bonuses being `bonus` /
     `hit-points-per-level` modifiers. A plain number string, which `Stats.MaxHp` accepts.
     Removed and temporary HP are ignored: Stats hold the maximum.
   - **AC**, best effort: the equipped item in `inventory[]` whose `definition` has an
     `armorTypeId` of 1–3 (light, medium, heavy) gives its `armorClass` plus the full DEX
     mod, DEX up to +2, or none; with no armour, 10 + DEX, or an unarmoured-defence
     modifier's ability when there is one; plus an equipped shield's `armorClass`
     (`armorTypeId` 4); plus `bonus` / `armor-class` modifiers. Any `set` modifier on AC, an
     AC override in `characterValues`, or more than one armour equipped gives a warning and
     `ac: null`, so the user types it rather than trusting a wrong number.
   - Every field is optional. A field that is missing or the wrong type becomes null plus a
     warning; `stats` with nothing in it is null (`Stats.Of`'s rule). The whole preview
     fails (502) only when `data` or `data.name` is missing.
   - Each dice string is passed through `IDiceRoller.Check` before it is answered, so the
     preview can always be saved.
   - **Never read or answered:** the portrait (`decorations.avatarUrl`), backstory,
     notes, features, spells, inventory names and descriptions, feats, race and background
     text, the owner's username and the campaign block (Notes, "What is stored").
6. **`EntryResponse.DndBeyond.canRefresh`**: `Enabled && EntryLinks.CanWrite(entry,
   viewer)`. That is how the web knows to show the button; there is no separate
   feature-flag endpoint.
7. **Tests. No test touches the network.**
   - The test host replaces `DndBeyondClient`'s primary handler with
     `FakeDndBeyondHandler`, which answers from `Fixtures/dndbeyond/` by id, records every
     request, and **throws on any request whose host is not
     `character-service.dndbeyond.com`**. The base test fixture leaves `Enabled` false.
   - `Fixtures/dndbeyond/` holds **invented** characters, hand-written in the shape above:
     "Testy McTestface" (a Fighter 5 in chain mail and a shield, 44 HP), a multiclass
     Wizard 3 / Rogue 2 with a `bonus initiative` modifier, a Monk with unarmoured defence,
     one with an HP override, one with a `set` AC modifier, one missing `classes`, a
     `success: false` body, and a body with a long invented `notes.backstory` and feature
     text. Never a real D&D Beyond export, not even an excerpt, and no real character's
     name or id.
   - `DndBeyondCharacterTests`: each fixture's preview, number by number; the warnings;
     that the backstory and feature words never appear in the serialised preview; the
     allowlisted keys, exactly.
   - `DndBeyondRefreshTests`: 404 with the flag off, and the fake saw **zero** requests;
     the preview for the claimer and for a DM; 403 for another player; 409 with no link;
     the second press inside the cooldown answers `cached: true` with no second request;
     429 after `PerMemberPerHour`; the error table row by row (the fake can answer 403,
     404, 429, 500, a 302, a slow body past a 1-second test timeout, a body over a small
     test `MaxResponseBytes`, and HTML); after every failure the link is unchanged and no
     event was appended; `GET entry` has `canRefresh` true only when enabled and writable.
8. **`NOTICE`** gains a D&D Beyond paragraph: an entry can hold a link to a D&D Beyond
   sheet; with the refresh turned on, the server reads a public sheet's numbers when a user
   asks; nothing from D&D Beyond is distributed with TakeInitiative; not affiliated with
   D&D Beyond or Wizards of the Coast. D&D Beyond's name is text only, never a logo.

### 22c. The sheet line and the refresh (web)

1. **The sheet line** (`Wiki/DndBeyondLine.vue`), under `WikiClaimControl`, on a
   Character whose response has `dndBeyond`:
   - "↗ D&D Beyond sheet", a link to `url` in a new tab (`rel="noopener noreferrer"`),
     with a 44px target on a phone, like 21c's source line;
   - for a writer, a small "Edit" that turns it into a URL field with Save and "Remove
     link". An error from the API shows under the field.
   - For a writer with no link, "Link D&D Beyond sheet" opens the same field. The field
     is `type="url"`, `inputmode="url"`, and stays above the on-screen keyboard (invariant
     11).
   - The hint under the field: "Paste the address of the character's sheet. Refresh reads
     it only if the sheet is Public." The second sentence shows only when `canRefresh`.
2. **Refresh** (`Wiki/DndBeyondRefresh.vue`), a button on the line when `canRefresh`:
   "Refresh from D&D Beyond". It calls the refresh endpoint and shows the preview in a
   small card: the name, the summary, the three numbers (with the current ones struck
   through where they differ), and the warnings.
   - **"Use these"** fills the Stats editor's form with the preview's non-null fields and
     opens it; **Save** is the existing `PUT stats`. It is the same flow as "Use … stats"
     (21c), so `StatsEditor` gains one way in (a `prefill` prop or an exposed `fill()`),
     not a second writer.
   - When the preview's name differs from the entry's, a checkbox "Also rename to
     'Thorin Oakenshield'", **off by default**. Ticked, Save also sends `PUT name`.
   - Errors show the API's message in the card, with the link still there; 429 shows the
     `Retry-After` time. No automatic retry.
   - The card says "Read from D&D Beyond just now" (or "a minute ago" when `cached`).
3. **Pushes and history.** `entryLinksChanged` refreshes the entry query.
   `describeChange` gets "linked a D&D Beyond sheet" and "removed the D&D Beyond sheet".
4. **Tests.** `dndBeyond.test.ts`: which controls show for the claimer, another player and
   a DM, with the flag on and off; the preview-to-form mapping (null fields leave the form
   alone); the rename checkbox's default. `mergeClaimStats.test.ts`: filling from a
   preview then saving sends one `PUT stats` with the three fields.
5. **Close the step**: tick this file's PR table, set README's status to `done`, and
   update HANDOVER, including the decisions below that are still open.

## Verify

1. `dotnet test`, `vitest`, `npx nuxi typecheck` and `pnpm build` pass, `schema.d.ts` is
   fresh, and CI is green on every PR. CI never enables the refresh and never reaches D&D
   Beyond; a test fails if anything tries.
2. `git grep -n "dndbeyond.com" -- apps` finds only `DndBeyondSheet.cs`, the options'
   default, the fake handler, tests and the web's hint text. `git grep -il "cobalt" -- apps`
   finds nothing (no D&D Beyond session cookie is ever handled).
3. With the flag **off** (the default), `pnpm dev`: the API logs nothing about D&D Beyond,
   `POST …/dndbeyond/refresh` is 404, and no entry has `canRefresh`.
4. Two browser profiles, A (the owner, DM) at 1280 × 800 and B (a Player) at 390 × 844,
   flag off:
   1. B claims "Thorin", taps "Link D&D Beyond sheet", pastes a sheet URL with a slug and
      a query. It shows "↗ D&D Beyond sheet" and opens
      `https://www.dndbeyond.com/characters/{id}` in a new tab.
   2. B pastes `https://example.com/characters/1`: the field shows the error and the old
      link stays.
   3. B types Stats by hand; in A's combat, "Add my character" gives Thorin those numbers.
   4. A links a sheet on an unclaimed NPC "Mysterious Stranger" (visibility Everyone). B
      opens it: no sheet line, and B's `GET entry` and history JSON have no `dndBeyond`.
   5. A merges a duplicate "Thorin O." with its own link into "Thorin": Thorin keeps its
      link.
5. **With the flag on: the user only, by hand.** No agent runs this, because it calls D&D
   Beyond. Start the API with `DndBeyond__Refresh__Enabled=true`, and use a sheet the user
   owns and has set to Public:
   1. Refresh shows the character's name, summary and numbers; compare them with the sheet.
      Note any wrong number and the character's build (what armour, which feats), so the
      mapping can be fixed on top of the stack.
   2. "Use these", then Save: Stats change, and a new combatant gets them.
   3. Press Refresh again at once: "a minute ago" (cached), and the API log shows no
      second request.
   4. Set the sheet to Private on D&D Beyond (or use a made-up id): the "isn't public"
      message, the link unchanged.
   5. Turn the network off: the timeout message within about 10 seconds.
   6. As B, on a player character B does not own: no Refresh button.

## Notes / gotchas

### D&D Beyond's terms

- **There is no public API.** D&D Beyond does not publish or license an API for character
  data. `character-service.dndbeyond.com/character/v5/character/{id}` is the JSON its own
  sheet page loads, found and used by community tools (initiative trackers, VTT
  importers). It is undocumented, unversioned for outsiders, and has changed shape and
  host before (earlier tools used `/character/{id}/json` on the main site). It answers
  anonymous requests **only for characters whose privacy is set to Public**.
- **The Terms of Service question is not settled by this plan.** D&D Beyond's terms
  restrict automated access to its services (robots, scrapers and the like) and grant no
  right to its data outside its own site. A single, user-initiated read of a sheet the
  user chose to make public is at the mild end, but it is still an automated request to an
  endpoint nobody licensed, and the terms can change. That is why the refresh is off by
  default, rate-limited, one request per press, cached, without retries, identified by an
  honest `User-Agent`, and never uses anyone's D&D Beyond login.
- **What this plan does to keep it small**: no polling, no background refresh, no bulk
  or campaign-wide import, no following redirects, no crawling of anything but the one id
  the user linked, no storage of the answer, no display of sheet content.
- **The link is safe either way.** Storing a URL a user pasted and linking out to it is an
  ordinary hyperlink, which is why the link (22a, 22c) is always on and the fetch (22b) is
  the only part behind the flag.

**Decisions for the user.** Each has the default this plan uses.

1. **Turn the refresh on at all?** Read D&D Beyond's current Terms of Service and decide.
   *Default: build it (22b), keep it off everywhere (`Enabled: false`), dev included. The
   app is then a link plus hand-entered Stats.* If you decide never to call D&D Beyond,
   say so and 22b is dropped: 22a and 22c (without the button) still stand on their own.
2. **Production.** *Default: off.* Turning it on is one environment variable; the
   ceilings in 22b.1 then apply to the whole deployment.
3. **The portrait.** Design §11 lists it. *Default: not in 22.* Showing D&D Beyond's image
   URL makes every viewer's browser fetch from D&D Beyond (and it is the user's uploaded
   art or D&D Beyond's stock art, whose licence is unclear); copying it into the S3 store
   (step 16) means storing D&D Beyond-hosted content. If wanted later, it should be an
   explicit "Use this portrait" that copies the image through step 16's pipeline, and the
   user owning that choice.
4. **Renaming from the sheet.** Design §11 says refresh "fills in name". *Default: offered
   as an unticked checkbox*, because a wiki name ("Thorin") and a sheet name ("Thorin
   Oakenshield, Heir of Durin") are often deliberately different, and a rename is visible
   to the whole table.
5. **Who may refresh.** *Default: whoever may write the Stats* (the claimer and the DMs).
   Letting every reader refresh would let any player start requests for any PC.
6. **Keep the level and class summary?** *Default: shown in the preview only, not stored.*
   Stats are three fields by design (§1); storing "Level 5 · Fighter 5" would be a new
   field on the entry and a glossary question.

### Link, not Source

- The glossary's **Source** mentions "a D&D Beyond sheet", but step 20 made `Source` "set
  once, at creation" and the source line's code assumes a reference provider. A sheet link
  is added later, replaced and removed, and an entry can have both (a PC made with + Wiki
  from an SRD creature, then linked). Design §11 says the sheet URL is stored as a `Link`,
  so 22 uses `Entry.Links`, which was declared for this.
- Consider trimming "a D&D Beyond sheet" from the glossary's Source row when this step
  closes; that is a docs-only change to §1 and needs the user's nod (the glossary is law).

### Why only an id

The API never fetches a URL a user typed. 22a reduces any accepted URL to a numeric id and
a fixed canonical link, and 22b builds its one request from `BaseUrl` (configuration, not
user input) and that number. So the refresh cannot be pointed at an internal address, a
different host, or a D&D Beyond page other than a character's JSON (no server-side request
forgery through this feature). Redirects are off for the same reason.

### What is stored

- **Stored:** the canonical sheet URL and its id (22a), and, only when a user presses Save,
  the three Stats values (and the name, if they tick rename). Those go through the same
  `PUT stats` / `PUT name` as hand edits, with the user as the actor, so history shows
  who changed what.
- **Held in memory for the cooldown only:** the last preview per id (name, summary, three
  numbers, warnings), at most `PerEntryCooldownSeconds` old. Never the raw answer.
- **Never stored, logged or shown:** the raw JSON, the portrait, backstory, notes,
  features, spells, inventory, the owner's D&D Beyond username, D&D Beyond campaign data.
  The allowlist test (22b.7) keeps the preview to its keys.
- A sheet's numbers are the player's own character, not rules text; nothing from a book
  passes through.

### Not checked against real data

This plan and its fixtures are written from how community tools describe the v5 JSON, not
from a live answer; the agent building 22b must not call D&D Beyond to find out. Expect
the user's first live check (Verify 5) to find something: a field renamed, a modifier
shape the mapper does not know, an AC off by one. Each gets a fix and a new invented
fixture on top of the stack. The mapper's rule is to warn and leave a field null rather
than guess, so a wrong shape shows up as an empty field, not a wrong number.

### Other notes

- **The fallback is the product.** With the flag off (the default), everything a table
  needs for combat works: the link out, Stats by hand, "Add my character". The refresh
  saves typing three numbers; it is not required by anything.
- **Why a preview, not a write.** The mapping is best effort (AC especially), so a person
  confirms the numbers before they become Stats. It also keeps a single writer for Stats
  (`PUT stats`), a single event (`EntryStatsChanged`) and a single push, and it means a
  failed or odd refresh can never change an entry.
- **Rate limits are per instance.** The limiter is in memory. The app runs one API
  instance today; if that changes, move the counters to Postgres or accept N× the
  ceilings.
- **HttpClient.** This is the API's first outbound HTTP call. Use
  `IHttpClientFactory` (`AddHttpClient<DndBeyondClient>`), so handlers are pooled and the
  test host can swap the primary handler; don't `new HttpClient()`.
- **Kinds.** Only Characters have Stats, so only Characters have a sheet link. A
  Character whose kind is changed keeps its link, hidden, as its Stats are.
- **Not in 22:**
  - private sheets, D&D Beyond logins, `cobalt` session cookies or any token a user pastes
    (some community tools do this; it hands the server a user's D&D Beyond session);
  - the portrait, spells, features, inventory, skills, saving throws, passive scores,
    conditions and current HP;
  - automatic, scheduled or campaign-wide refresh, and a "refresh all" button;
  - D&D Beyond campaigns, encounters or monsters;
  - other sheet sites (Roll20, Foundry, Dicecloud). A second one would reuse `EntryLink`'s
    `Provider` and add its own parser, and its own decision about fetching;
  - storing the level and class summary (decision 6).
