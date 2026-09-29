# 25 — Wiki redesign: mobile first, then desktop

## Goal

The wiki entry page answers **"who is this?"** in one phone screen, and makes the
relationship between notes and the curated text visible. Today
`pages/app/campaigns/[campaignId]/wiki/[entryId].vue` is one long column of eleven
sections, all expanded: header, source line, claim, stats, article, connections,
timeline, gallery, combats, "Add a note about X", and the dialogs. On a phone at the
table, what you want is buried under what you rarely need. Two ideas also confuse people:

- **Article vs notes.** The article is the curated layer built from notes (by promote), but
  the page doesn't show that relationship: the article and the timeline are two unrelated
  sections far apart.
- **"Claim".** It reads like ownership, but it only means "this Character entry is my
  player character".

And adding a note from the wiki is the wrong place for it: notes are written in the
session. That button goes.

So the entry becomes a short header, a stats peek, a connections strip, and **Summary |
Notes** tabs, with everything else collapsed under "More about X". At `lg` and up the page
goes full width: a capped main panel for reading and a sticky right-hand panel for
everything else. The words change in the UI only (§"Words"); code, API routes and events
keep their names, so nothing is migrated.

**Mockups:** the canvas at https://claude.ai/artifact/8YdLCzEPqDmnrpkgyhFWTR (mobile entry
page, both tabs, the wiki home and the desktop layout). The ASCII sketches below follow it.

"Running" at the end: at 390 × 844, a player opens Vex from the Wiki and sees kind, name,
"Played by Sam", the stats line and the first lines of the summary without scrolling. A tap
on Notes shows what happened with Vex, newest session first, and "Add to summary" on a note
puts it in the summary with a "From Jo's note · Session 4 ↗" chip that jumps back. At
1440 × 900 the same page reads in a centred column with the stats, connections, details,
gallery and combats in a panel on the right. No wiki page offers "Add a note about X".

The step ships as seven PRs stacked on `dev` (605be3a, after #262), starting with this
file's docs PR. Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 25a | `v2/25a-wiki-redesign-plan` | This plan and the glossary | Docs only | [x] |
| 25b | `v2/25b-wiki-drop-add-note` | Remove "Add a note about X" from the wiki | The entry page and its empty Connections panel no longer link to the composer. ⌘K's "Post a note about X" still works | [ ] |
| 25c | `v2/25c-played-by` | Claim → "Played by" in copy (web and API strings) | "Played by Sam", "This is my character", "Not my character" everywhere a claim shows; API errors say "player" | [ ] |
| 25d | `v2/25d-summary-notes-tabs` | Summary \| Notes tabs | The article and timeline become two tabs, with the "Built from N of M notes" line, source chips on quotes, "Add to summary" / "✓ In summary", and the empty state | [ ] |
| 25e | `v2/25e-entry-mobile-collapse` | The mobile order and collapsed sections | Stats peek, Connections strip, and "More about X" (Details, Gallery, Combats) collapsed | [ ] |
| 25f | `v2/25f-entry-desktop-panel` | The desktop layout (`lg+`) | Full-width page, capped main panel, sticky right-hand panel with the sections forced open | [ ] |
| 25g | `v2/25g-wiki-home-polish` | The wiki home | Icon-button toolbar, and each row shows "Played by", a one-line summary gist and "N notes · last in Session X". The step's Verify passes | [ ] |

Step 24 (the Discord import, #261) is unrelated and stays where it is; this step does not
depend on it.

## Depends on

- **15**: the entry page, `Article`, `ArticleBlock`, quotes and promote (`PromoteDialog`,
  `PromoteSelection`), `EntryTimeline`, `ClaimControl`, merge and history.
- **16**: `EntryGallery`. **18**: `CombatEntryCombats`. **19**: `EntryConnections`,
  `EvidenceSheet` and the graph. **20**: the source line and `StatsEditor`.
- **#262**: `components/PageContainer.vue`, the `mx-auto w-full max-w-5xl` column every
  campaign page now uses as its root, the entry page included. 25f has to let the entry
  page break out of it at `lg`.

These parts of [12-v2-design-session.md](12-v2-design-session.md) are binding and change
here: the glossary (§1, updated by this PR) and the entry page sketch (§4, updated by 25b
and 25d).

## Access patterns

Ranked by how often they happen. The layout serves them in this order.

1. **Quick lookup at the table** (phone, most often): "Who is Vex again?" Needs name, kind,
   the first lines of the summary and who plays it, in one screen with no scrolling.
2. **Checking your own character** (phone, player): stats, and the D&D Beyond sheet line.
3. **Catching up between sessions**: "What happened with X?" Wants the notes, newest first.
4. **Curating** (desktop, usually the DM): add notes to the summary, edit it, add secret
   blocks.
5. **Exploring relationships**: connections, then evidence, then the graph.
6. **Housekeeping** (rare): edit details, visibility, edit access, merge, history, loose
   ends.

### Show, peek or hide

| Section | Phone | Desktop (`lg+`) |
|---|---|---|
| Header: kind, name, aliases, ⋯ menu | Show (aliases truncated) | Show, main panel |
| Played by | Show, a chip in the header (Characters only) | Show, top of the right panel, with Change |
| Stats | Peek: one line, tap to expand. Open by default for the character's player | Show, right panel |
| Connections | Peek: a chip strip, "+N more", Graph | Show all chips, right panel |
| Summary | Show, the default tab | Show, main panel tab |
| Notes | Tab | Main panel tab |
| Details (source, sheet, Played-by select, creator, visibility, edit access) | Hide, under "More about X" | Show, right panel |
| Gallery | Hide, "4 images" under "More about X" | Show, right panel |
| Combats | Hide, "In 2 combats" under "More about X" | Show, right panel |
| Edit details, History, Merge into… | ⋯ menu | ⋯ menu |
| Add a note about X | **Removed** | **Removed** |

## Words

The UI copy changes; the code names stay. The glossary (§1) records both, and the old UI
words are banned from copy.

| Concept | UI copy | Code / API name (unchanged) |
|---|---|---|
| A member marked as a Character's player | **Played by Sam** / **Played by you** | `claim`, `ClaimedByMemberId`, `PUT entries/{id}/claim`, `EntryClaimed` events |
| Marking it yourself | **This is my character** | claim |
| Clearing it yourself | **Not my character** | unclaim |
| A DM setting it | **Played by** select, "Nobody" first | assign a claim (`canAssignClaim`) |
| An entry's curated content | **Summary** | `article`, `Article.vue`, `PutEntryArticle` |
| The notes that mention an entry | **Notes** | `timeline`, `EntryTimeline.vue`, `GetEntryTimeline` |
| Copying a note into it | **Add to summary** | `promote`, `PromoteDialog.vue`, `PostEntryQuote` |

"Claim", "Unclaim", "Article", "Timeline" and "Promote" do not appear in UI copy after 25d.
"Own" stays banned for a player character (the campaign has an owner).

## Removing "Add a note about X" from the wiki

Notes are written in the session. The wiki loses its two entry points into the composer:

- the button at the bottom of the entry page;
- the link in the empty Connections panel. Its text stays ("No connections yet. Mention
  another entry in a note about X.", `utils/connections.ts`).

These stay, because ⌘K uses them: ⌘K's "Post a note about X" action (`note-about`), the
composer's `?about=` prefill, and the helpers behind it (`utils/entries.ts`,
`utils/mentions.ts`, `Composer.vue`, the campaign page).

## Layouts

### Entry page, phone: Summary tab

```
┌──────────────────────────────────────┐
│ ‹ Wiki                               │
│                                      │
│ CHARACTER                        ⋯   │
│ Vex Thornwood                        │
│ aka Vex, The Grey Fox, Thorn…        │
│ [👤 Played by Sam]                   │
│                                      │
│ ▸ AC 15 · HP 38/44 · Init +3         │  ← stats peek, tap to expand
│                                      │
│ [@Phandalin 4] [@Tharden 3] [@Ember  │  ← connections strip
│  Court 2] +5 more        Graph ↗     │
│                                      │
│ ┌ Summary ─┬─ Notes ─┐               │
│ └──────────┴─────────┘               │
│ Built from 3 of 12 notes             │
│ · edited by Jo                [Edit] │
│                                      │
│ Half-elf rogue, grew up on the docks │
│ of Neverwinter. Owes the Ember Court │
│ a debt she won't talk about.         │
│ ┃ "She lifted the key off the guard  │
│ ┃  without breaking stride."         │
│ ┃ [From Jo's note · Session 4 ↗]     │
│ 🔒 DM  Her sister is the Court's     │
│        spymaster.                    │
│                                      │
│ MORE ABOUT VEX                       │
│ ▸ Details                            │
│ ▸ Gallery · 4 images                 │
│ ▸ Combats · in 2 combats             │
├──────────────────────────────────────┤
│  Campaign     Wiki      Combat       │
└──────────────────────────────────────┘
```

### Entry page, phone: Notes tab

```
┌──────────────────────────────────────┐
│ ‹ Wiki                               │
│ CHARACTER                        ⋯   │
│ Vex Thornwood                        │
│ [👤 Played by Sam]                   │
│ ▸ AC 15 · HP 38/44 · Init +3         │
│ [@Phandalin 4] [@Tharden 3] +6  ↗    │
│                                      │
│ ┌─ Summary ─┬ Notes ─┐               │
│ └───────────┴────────┘               │
│ Every note that mentions Vex. Add    │
│ the good bits to her summary.        │
│                                      │
│ SESSION 6 · 12 Sep                   │
│ ┌──────────────────────────────────┐ │
│ │ Sam: @Vex picked the lock on the │ │
│ │ cellar door, found the ledger.   │ │
│ │                  [Add to summary]│ │
│ └──────────────────────────────────┘ │
│ SESSION 4 · 29 Aug                   │
│ ┌──────────────────────────────────┐ │
│ │ Jo: She lifted the key off the   │ │
│ │ guard without breaking stride.   │ │
│ │                    ✓ In summary  │ │
│ └──────────────────────────────────┘ │
│                                      │
│          [Load older notes]          │
├──────────────────────────────────────┤
│  Campaign     Wiki      Combat       │
└──────────────────────────────────────┘
```

Order on a phone:

1. Back link.
2. Header: kind, name, aliases (truncated), a **"Played by Sam"** chip (Characters only),
   and the ⋯ menu (Edit details, History, Merge into…).
3. **Stats** peek row: "AC 15 · HP 38/44 · Init +3". Tap to expand. It starts open when
   the viewer is the character's player.
4. **Connections** chip strip: the top chips, "+N more" and a Graph link. The evidence
   sheet is unchanged.
5. **Summary | Notes** tabs.
   - **Summary:** "Built from N of M notes · edited by X" with Edit. Quote blocks carry a
     source chip ("From Jo's note · Session 4 ↗") that jumps to that note on the Notes tab.
     Secret blocks are unchanged. Then **More about X**, collapsed rows: **Details** (the
     Played-by select for DMs, creator, visibility, edit access, source line, sheet link),
     **Gallery** ("4 images") and **Combats** ("In 2 combats").
   - **Notes:** a one-line explainer, then notes grouped by session, newest first. Each has
     "Add to summary", or shows "✓ In summary". Then "Load older notes".
   - **Default tab:** Summary, or Notes when the summary is empty.
   - **Empty summary:** "No summary yet", a line on what a summary is, "Pick from notes"
     (switches to Notes) and "Write one".
   - **URLs:** `#timeline` and `?tab=notes` open Notes. `?edit` and `?block=` force Summary.
6. The app tab bar. There is no sticky note bar.

### Wiki home, phone

```
┌──────────────────────────────────────┐
│ Wiki                   [⌖] [⇅] [+]   │  ← Graph, Sort, New
│ [🔍 Search the wiki…              ]  │  ← pinned
│ (All)(Character)(Place)(Faction)…    │
│ 🧵 Loose ends (7)                  › │
│                                      │
│ Vex Thornwood         [Played by Sam]│
│ Character                            │
│ Half-elf rogue from the Neverwinter… │  ← one-line gist
│ 12 notes · last in Session 6         │
│ ──────────────────────────────────── │
│ Greyhollow Keep                      │
│ Place                                │
│ No summary yet, 4 notes to pick from │
│ 4 notes · last in Session 5          │
└──────────────────────────────────────┘
```

- A title row with icon buttons: Graph, Sort, and New (+).
- Search stays pinned below it. The kind chips and the Loose ends banner stay.
- Each row: name, kind, a "Played by X" chip, a **one-line summary gist** ("No summary
  yet, N notes to pick from" when there is none), and "N notes · last in Session X".
- On desktop, keep today's two-column list and add the gist.

### Entry page, desktop (`lg+`)

```
┌────┬──────────────────────────────────────────────────────┬──────────────────────┐
│    │ ‹ Wiki                                               │ PLAYED BY            │
│ C  │        CHARACTER                              ⋯      │ 👤 Sam     [Change]  │
│ a  │        Vex Thornwood                                 │                      │
│ m  │        aka Vex, The Grey Fox, Thorn                  │ STATS                │
│ p  │                                                      │ AC 15 HP 38/44 +3    │
│ a  │        ┌ Summary ─┬─ Notes ─┐                        │                      │
│ i  │        └──────────┴─────────┘                        │ CONNECTIONS  Graph ↗ │
│ g  │        Built from 3 of 12 notes · edited by Jo [Edit]│ [@Phandalin 4]       │
│ n  │                                                      │ [@Tharden 3] [@Ember │
│    │        Half-elf rogue, grew up on the docks of       │ Court 2] [@Mara 1] … │
│ r  │        Neverwinter. Owes the Ember Court a debt…     │                      │
│ a  │        ┃ "She lifted the key off the guard…"         │ DETAILS              │
│ i  │        ┃ [From Jo's note · Session 4 ↗]              │ Source, sheet, …     │
│ l  │                                                      │                      │
│    │        ←──────── max-w-3xl, centred ────────→        │ GALLERY · 4 images   │
│    │                                                      │ COMBATS · 2          │
│    │                                                      │ ↕ scrolls on its own │
└────┴──────────────────────────────────────────────────────┴──────────────────────┘
       ←────────────── minmax(0, 1fr) ──────────────────────→←── 360px / 400px xl ──→
```

- **The page goes full width** at `lg`: the entry page stops using `PageContainer`'s
  `max-w-5xl` there (for example `lg:max-w-none` on its `PageContainer`, or a `wide` prop
  on the component; 25f picks one) and becomes
  `lg:grid lg:grid-cols-[minmax(0,1fr)_360px] xl:grid-cols-[minmax(0,1fr)_400px]` across
  the content area beside the campaign rail. Check that `layouts/campaign.vue` does not cap
  the width.
- **Main panel:** the header (name plus ⋯) and the Summary | Notes tabs, centred at
  `max-w-3xl`.
- **Right-hand panel:** full height, a left border, `sticky` below the header, and scrolls
  on its own. It holds Played by (with Change), Stats, Connections (all chips, plus Graph),
  Details, Gallery and Combats. The mobile sections are reused and forced open at `lg`.
- Other campaign pages keep `PageContainer`'s width.

## Files touched

Paths are relative to `apps/TakeInitiative.Web` (Web), `apps/TakeInitiative.Api` (API) and
`apps/TakeInitiative.Api.Tests` (Tests), except where they start at the repo root.

**This PR (docs)**
- `docs/roadmap/25-wiki-redesign.md`: this file
- `docs/roadmap/12-v2-design-session.md`: the glossary (§1): Player ("Played by"), and
  Article, Timeline and Promote shown as Summary, Notes and Add to summary
- `docs/roadmap/README.md`: step 25; `docs/roadmap/HANDOVER.md`: the stack row and next work

**25b**
- Web modify `pages/app/campaigns/[campaignId]/wiki/[entryId].vue` (the bottom button,
  `aboutLink`, the `MessageSquarePlus` import, `:aboutHref`), `components/Wiki/EntryConnections.vue`
  (the `aboutHref` prop and its link)
- `docs/roadmap/12-v2-design-session.md` §2 and §4, `docs/roadmap/15-wiki-and-mentions.md`
  15c: say the wiki no longer links to the composer

**25c**
- Web modify `components/Wiki/{ClaimControl,EntryHeader,EntryListItem,MergeDialog}.vue`,
  `utils/entries.ts` (history labels and merge error text)
- API modify `src/Features/Entries/Api/PutEntryClaim/PutEntryClaim.cs`,
  `src/Features/Entries/Api/PutEntryKind/PutEntryKind.cs`, the merge errors
  (`EntryMerge.cs`, `PostEntryMerge.cs`)
- Tests: assertions on the old text (`ClaimTests.cs`, `MergeTests.cs`); Web
  `tests/unit/mergeClaimStats.test.ts`

**25d**
- Web modify `pages/app/campaigns/[campaignId]/wiki/[entryId].vue` (tabs, `?tab` / `#timeline`),
  `components/Wiki/{Article,ArticleBlock,ArticleEditor,EntryTimeline,PromoteDialog,PromoteSelection}.vue`
  (copy, source chips, "✓ In summary", "Built from N of M notes", the empty state)
- Web add a small pure helper for the tab choice and the "built from" count, with tests

**25e**
- Web add `components/Wiki/EntrySection.vue` (a collapsible section, on `components/ui/collapsible`),
  `components/Wiki/EntryDetails.vue` (source line, sheet line, Played-by select, creator,
  visibility, edit access), a stats peek and a connections strip (inside `StatsEditor` /
  `EntryConnections`, or small new components)
- Web modify `wiki/[entryId].vue` (the order), `EntryHeader.vue` (the Played-by chip, ⋯ menu)

**25f**
- Web modify `wiki/[entryId].vue` (the grid), `components/PageContainer.vue` (only if it gains
  a `wide` option), `EntrySection.vue` (forced open at `lg`); check `layouts/campaign.vue`

**25g**
- Web modify `components/Wiki/EntryListItem.vue`, `pages/app/campaigns/[campaignId]/wiki/index.vue`
- API, if the list DTO does not already carry what the gist needs: a derived first-line
  `summaryGist` and the note count / last session on `EntrySummaryResponse`, with tests;
  Web `utils/api/schema.d.ts` regenerated

## Steps

### 0. Start the stack (this PR)

```sh
git switch -c v2/25a-wiki-redesign-plan origin/dev
```

Each later sub-step branches from the one before (`gh stack` hangs here; use
`git push -u` and `gh pr create --base <previous branch>`).

**Glossary check.** §1 now says **Player ("Played by")** for a claim, with "claim" as the
code and API name, banned in UI copy, and records Summary, Notes and Add to summary as the
UI words for Article, Timeline and Promote.

### 25b. Drop "Add a note about X" from the wiki

1. Delete the bottom button on the entry page, its `aboutLink` computed, the
   `MessageSquarePlus` import, and the `:aboutHref` binding.
2. Remove `aboutHref` from `EntryConnections.vue` and its link in the empty state. The empty
   text stays.
3. Leave the ⌘K `note-about` action, `?about=` and its helpers alone.
4. Update §2's core-loop line and §4's sketch in `12-v2-design-session.md`, and 15c's
   description in `15-wiki-and-mentions.md`.

Verify: no wiki page shows the button or link; ⌘K "Post a note about Vex" still opens the
composer with `@Vex`.

### 25c. Claim → "Played by"

1. `ClaimControl.vue`: "Played by Sam" / "Played by you"; "Claim as my character" →
   **"This is my character"**; "Unclaim" → **"Not my character"**; the DM's "Claim for"
   select → **"Played by"**, "Nobody" first; the toast → "Could not change who plays this."
2. `EntryHeader.vue` and `EntryListItem.vue` badges: "Played by {name}". `MergeDialog.vue`:
   "Still played by {name}".
3. `utils/entries.ts` history labels: "said they play it", "set X as its player", "cleared
   its player"; the merge error text the same way.
4. API strings: "Only a Character can have a player.", "Someone else already plays this
   character.", "Only its player and the DMs can change who plays it." (`PutEntryClaim`),
   "Clear its player first." (`PutEntryKind`), and the merge errors.
5. Update the tests that assert on the old text. The route, `ClaimedByMemberId`, the events
   and the helpers (`claimerOf`, `canClaimEntry`, `canAssignClaim`) keep their names.

Verify: `grep -ri "claim" components pages` finds no user-facing string; `dotnet test` and
`vitest` pass.

### 25d. Summary | Notes tabs

1. The article and the timeline become `components/ui/tabs`: **Summary** and **Notes**.
   Default: Summary, or Notes when the summary is empty. `#timeline` and `?tab=notes` open
   Notes; `?edit` and `?block=` force Summary. Switching tabs updates `?tab` with `replace`.
2. Summary: "Built from N of M notes · edited by X" with Edit (N is the distinct notes
   quoted, M the notes in the timeline the viewer can see). Each quote block gets a source
   chip "From Jo's note · Session 4 ↗" that switches to Notes and scrolls to that note.
3. Notes: a one-line explainer, notes grouped by session, newest first; "Add to summary" on
   each, or "✓ In summary" when a quote already links to it; "Load older notes".
4. Empty summary: "No summary yet", "Pick from notes" (switches tab) and "Write one".
5. Copy: every "Article", "Timeline" and "Promote" in the UI becomes Summary, Notes and Add
   to summary (the Promote dialog's title and button included).

Verify: Add to summary on a Session 4 note; the chip appears on Summary and jumps back;
the note shows "✓ In summary"; `#timeline` opens Notes.

### 25e. The mobile order and collapsed sections

1. `Wiki/EntrySection.vue`: a titled collapsible (`components/ui/collapsible`) with a
   one-line peek when closed, 44px tap targets (invariant 11), and a `forceOpen` prop for 25f.
2. The page order: back link, header (Played-by chip, ⋯ with Edit details, History, Merge
   into…), stats peek, connections strip, tabs, then "More about X".
3. Stats peek: "AC 15 · HP 38/44 · Init +3"; open by default for the character's player.
   Editing stays where `StatsEditor` puts it today.
4. Connections strip: the top chips on one or two lines, "+N more", Graph ↗. The evidence
   sheet is unchanged.
5. "More about X": **Details** (`Wiki/EntryDetails.vue`: source line, sheet line, the
   Played-by select for DMs, creator, visibility, edit access), **Gallery** ("4 images")
   and **Combats** ("In 2 combats"), all collapsed. Gallery and combats load when opened.

Verify at 390 × 844: the quick lookup (pattern 1) fits one screen for a Character with a
summary.

### 25f. The desktop layout

1. At `lg`, the entry page leaves `PageContainer`'s `max-w-5xl` and becomes the two-column
   grid above. Below `lg` nothing changes.
2. Main panel: the header and tabs, `mx-auto max-w-3xl`.
3. Right panel: `border-l`, `sticky` under the app header, `h-[calc(100dvh-…)]`,
   `overflow-y-auto`. Played by (with Change), Stats, Connections (all chips), Details,
   Gallery, Combats, each an `EntrySection` with `forceOpen`.
4. Check `layouts/campaign.vue` and the campaign rail: nothing else may cap the width.

Verify at 1280 × 800 and 1440 × 900: the panel scrolls on its own while the summary stays
put, and the other campaign pages keep their width.

### 25g. The wiki home

1. The title row gets icon buttons: Graph, Sort and New (+). Search stays pinned, the kind
   chips and the Loose ends banner stay.
2. `EntryListItem.vue`: name, kind, "Played by X", a one-line summary gist ("No summary yet,
   N notes to pick from" when empty), and "N notes · last in Session X".
3. First check whether `EntrySummaryResponse` already carries article text and timeline
   counts. If not, add a derived `summaryGist` (the first plain-text line of the visible
   article, secret blocks excluded, mentions as text) and the counts on the API, computed
   per viewer, with a leak test: a gist never comes from a block the viewer cannot see.

## Verify

1. From `apps/TakeInitiative.Web`: `pnpm test`, `npx nuxi typecheck` and `pnpm build`. From
   the repo root: `dotnet test` (the Claim, Merge and Entry history tests). CI green on every
   PR in the stack.
2. In Chrome at 390 × 844, then 1440 × 900 (port 3100; 3000 is Ripple's), a DM and a player:
   1. Quick lookup: open a Character; name, kind, "Played by", the stats line and the
      summary's first lines are visible without scrolling.
   2. The player taps "This is my character" on an unplayed Character; the chip reads
      "Played by you"; "Not my character" clears it.
   3. The DM sets the player from Details' "Played by" select, and back to "Nobody".
   4. On Notes, "Add to summary" on a note; the quote's source chip jumps back to it, which
      now shows "✓ In summary".
   5. `…/wiki/{id}#timeline` and `?tab=notes` open Notes.
   6. Stats, Details, Gallery and Combats expand and collapse; stats start open for the
      player's own character.
   7. At 1440 the right panel is sticky and scrolls on its own; the main column is centred.
   8. No "Add a note about X" anywhere on the wiki; ⌘K's action still works.
   9. The wiki home shows gists and counts, and "No summary yet, N notes to pick from".
3. Screenshots in each PR description, compared against the mockup canvas.

## Notes / gotchas

- **Copy only.** Nothing is renamed in code, routes, DTOs or events, so there is no
  migration and old links (`#timeline`, `?block=`) keep working.
- **Another agent does UI work on `dev`.** Before each UI PR, check `git log` for recent
  changes to `components/Wiki` and the entry page, and rebase rather than overwrite.
- **Pages need a single element root** (HANDOVER). The entry page's root stays one
  `PageContainer`; the grid goes inside it or on it, not beside it.
- **Reuse:** `components/ui/{tabs,collapsible,accordion}`, the `Wiki/EvidenceSheet.vue`
  pattern, `Wiki/ChoiceChips.vue`, and the `claimerOf`, `canClaimEntry` and
  `canAssignClaim` helpers in `utils/entries.ts`, unchanged.
- **Not in 25:** editing the summary in the TipTap editor (a HANDOVER follow-up), a sticky
  note bar, and any change to who can claim or promote.
