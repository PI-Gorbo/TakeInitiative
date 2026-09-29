# Handover — for the next orchestrator

Written 2026-09-28. Read this, then [README.md](README.md) (the roadmap is the single
source of truth), then the step file you are about to run.

## How to run the build

- **You orchestrate; subagents implement.** One subagent per stacked PR, run in
  order, because each branch builds on the last. Give each subagent a
  self-contained brief covering:
  - the step-file section it implements;
  - the branch it starts from and the branch to create;
  - the files involved and what exists there today;
  - the verify commands, commit rules and env gotchas below;
  - "report back in < 300 words: PR, commits, files, deferrals, caveats, browser checklist".
- **Don't pause between PRs** for review. Keep stacking and report the PR list at
  the end. Things will be wrong sometimes, and that is accepted.
- **Stacks use `gh stack`.** From the top branch, run `gh stack add <branch>`, then
  commit, then `gh stack submit`. `gh stack view` shows the stack.
  - When every PR below it is merged, `submit` starts a new stack and re-bases the
    lowest open PR onto `dev`. That is expected.
  - If `submit` prompts or fails, fall back to `git push -u origin <branch>` and
    `gh pr create --base <previous branch>`.
- **Write the step file first.** Step files 13+ are written one at a time, just
  before a step starts, and each has its own docs PR at the bottom of the stack.
  README's Conventions section gives the six sections; `19-connections.md` and
  `20-srd-reference.md` are the latest examples.
- **When a step lands,** tick its PR table in the step file and update README's
  status column, so both describe what was actually built.

## Conventions

- **Commit messages** are checked by the husky hook:
  `^(feat|fix|docs|style|refactor|perf|test|build|chore|ci)(\((api|web|identity|dice|root|ci|docs)\))?(!)?: .*$`.
  - release-please reads them.
  - End each commit with the `Co-Authored-By: Claude …` line.
  - End each PR body with the Claude Code line.
- **Branches** are `v2/NNx-slug`.
- **Verify, web.** From `apps/TakeInitiative.Web`, run `pnpm test` (vitest),
  `npx nuxi typecheck` (the real type check; there is no lint script) and
  `pnpm build`. Check the UI at 390×844 and 1280×800.
- **Verify, API.** Run `dotnet test`. Regenerate `utils/api/schema.d.ts` with
  `pnpm gen:api` whenever the API changes.
- **Pages and layouts need a single element root.** Nuxt runs `fade`
  page and layout transitions, and a comment or a `v-if` at the root breaks
  them. Put comments inside the root element.
- **Don't nest layouts.** `layouts/campaign.vue` no longer wraps
  `<NuxtLayout name="default">`, for the same reason.

## Environment gotchas

- Port 3000 is Ripple's `nuxt dev`. Don't kill it; run this web app on 3100.
- Start the API with `CORS__MainApp=http://localhost:3100 ASPNETCORE_URLS=http://localhost:5010 dotnet run --no-launch-profile`.
  Add `Reference__FiveETools__IndexPath=../../.data/5etools/index.json` to turn on the
  5eTools provider once `pnpm 5etools:build --from <5etools-src>` has written the index
  (21b); without it the provider is off.
- Dev Postgres is the `takedb` container on port 7401. Port 5432 is Ripple's.
- A context-mode hook blocks `curl` and inline `node -e` fetches. Write a `.mjs`
  script to the scratchpad and run that instead.
- FastEndpoints returns 400 for a GET that sends `Content-Type: application/json`
  with no body.
- Agents must not log in to the app in a browser. Browser click-throughs are for
  the user, so every PR report ends with a checklist for them.

## Where the stack stands

Stack #224. Everything is on top of `dev` and still open:

| PR | Branch | What |
|---|---|---|
| #222 | `v2/17a-search-api` | 17a: search index and query API. Also has `fix(web): give the campaign page and layouts a single root`, which fixes the Transition and multi-root warnings |
| #223 | `v2/17-composer-cta-dialog` | No sessions → the composer is only a "Start Session 1" call to action. The Promote and Merge dialogs fit their content |
| #225 | `v2/17-composer-tiptap` | The composer on TipTap: ⌘B, ⌘I, ⌘⇧8, ⌘K for the `@` picker, `@` mentions as nodes. The stored markdown format is unchanged |
| #226 | `v2/17-composer-edit-mode` | Editing a note happens in the composer, with Cancel and Save. `Session/NoteEditor.vue` is deleted |
| #227 | `v2/17-handover` | This file |
| #228 | `v2/17b-search-sheet` | 17b: the ⌘K search sheet |
| #229 | `v2/17c-search-actions` | 17c: ⌘K actions; closes step 17 |
| #230 | `v2/18-combat-plan` | Step 18's step file |
| #231 | `v2/18-fix-nested-layouts` | Fix: the app and logo layouts no longer nest the default layout |
| #232 | `v2/18a-combat-api` | 18a: the combat model, redaction, combatants and live pushes (API) |
| #233 | `v2/18-fix-loading-padding` | Fix: `LoadingFallback` pads only its loading and error states |
| #234 | `v2/18b-combat-turns-api` | 18b: roll, end turn, reorder, finish and history (API) |
| #235 | `v2/18c-combat-tab` | 18c: the Combat tab, the combat page and adding combatants |
| #236 | `v2/18d-combatant-sheet` | 18d: the combatant sheet, drag to reorder and the combat history |
| #237 | `v2/18e-combat-card` | 18e: combat cards in the stream, the Join combat banner and pulse, an entry's combats, the slim composer |
| #238 | `v2/18f-combat-search` | 18f: combats and "⚔ Start combat" in ⌘K; closes step 18 |
| #239 | `v2/19-connections-plan` | Step 19's step file |
| #240 | `v2/19a-connections-api` | 19a: connections, evidence and the graph (API), with the leak tests |
| #241 | `v2/19b-loose-ends-api` | 19b: loose ends, their counts and link suggestions (API) |
| #242 | `v2/19c-connections-panel` | 19c: the Connections panel on an entry and the evidence sheet |
| #243 | `v2/19d-graph-page` | 19d: the graph page (`/wiki/graph`), plus a fix for 19c's evidence snippets |
| #244 | `v2/19e-loose-ends-ui` | 19e: loose ends in the Wiki, on dividers and in ⌘K, resolved in place; closes step 19 |
| #245 | `v2/20-srd-plan` | Step 20's step file |
| #246 | `v2/20a-srd-data` | 20a: the SRD 5.2 data (331 monsters from Open5e's fixtures), its catalog and `IReferenceProvider` |
| #247 | `v2/20b-reference-api` | 20b: the Reference section in `GET search`, `GET reference/{provider}/{id}`, `POST entries/from-reference` and `Entry.Source` (API) |
| #248 | `v2/20c-stat-block-card` | 20c: the stat-block card page and REFERENCE rows in ⌘K |
| #249 | `v2/20d-add-to-wiki` | 20d: + Wiki (⌘K row, ⌘Enter, the card), the entry's source line and "Use SRD stats"; closes step 20 |
| #250 | `v2/21-5etools-plan` | Step 21's step file |
| #251 | `v2/21a-5etools-script` | 21a: the 5eTools index script (`pnpm 5etools:build`), its synthetic fixture and tests |
| #252 | `v2/21b-5etools-provider` | 21b: the search-only 5eTools provider in the API, off without an index |
| #253 | `v2/21c-5etools-web` | 21c: 5eTools rows link out in ⌘K, with + Wiki, the source line ("↗ From 5eTools · Beholder (MM p. 28)", its tooltip the book's title) and "Use 5eTools stats". Also sends book titles from the API. Closes step 21 apart from 21d |
| #254 | `v2/22-ddb-plan` | Step 22's step file (D&D Beyond link). Merged as a plan only: step 22 is deferred, with no code |
| #255 | `v2/23-suggestions-plan` | Step 23's step file (in-browser suggestions), on top of #254 |
| #256 | `v2/23a-extraction-spike` | 23a: the GLiNER variant spike (Laya dropped by the user); picks GLiNER small v2.5 and pins its weights, small v2.1 as the fallback |
| #257 | `v2/23b-extractor-runtime` | 23b: the extractor runtime (worker, lazy load, self-hosted weights from our own reproducible export of upstream GLiNER small v2.5, Cache API, "Suggestions on this device" on the Me page) |
| #263 | `v2/25a-wiki-redesign-plan` | Step 25's step file (the wiki redesign), on `dev` after #262. 25b–25g stack on it |
| #264 | `v2/25b-wiki-drop-add-note` | 25b: the wiki drops "Add a note about X" (entry page button and empty Connections link). ⌘K's "Post a note about X" stays. On #263 |
| #265 | `v2/25c-played-by` | 25c: "Played by" instead of claim in the web copy and API errors ("This is my character", "Not my character"). Route, DTOs, events and helpers keep their names. On #264 |
| #266 | `v2/25d-summary-notes-tabs` | 25d: the entry page's Summary \| Notes tabs ("Built from N of M notes", quote source chips, "Add to summary" / "✓ In summary", the empty summary, `?tab=`), and Article / Timeline / Promote become Summary / Notes / Add to summary in all UI copy. On #265 |

The local branches `pr/PI-Gorbo/222`, `pr/PI-Gorbo/222-1` and `pr/PI-Gorbo/222-2` are stale
checkouts of #222. Ignore them.

### The composer after #225 and #226

- **The editor** is `components/Composer/Editor.vue` (`<ComposerEditor>`), typed as
  `ComposerEditorApi`.
  - Props: `v-model` for the text, plus `v-model:links` and `v-model:newEntries`.
  - Emits: `submit`, `cancel` and `files`.
  - Exposes: `focus(pos)`, `toggle(fmt)`, `triggerMention()`, `caretOffset()`,
    `setCaretOffset()` and `active`.
- **Text ↔ document conversion** is hand-rolled in `utils/editorText.ts`
  (`textToDoc` / `docToText`); `utils/editorExtensions.ts` holds the extensions.
  - Each line is a paragraph or a bullet item.
  - Bold and italic are parsed with markdown-it's rule.
  - Anything else stays as markdown source.
- **Edit mode** keeps its state in `utils/noteEdit.ts` and
  `composables/useComposerEdit.ts`, next to the draft rather than stashed over it.
  - `ComposerToolbar` has `mode="edit"`.
  - Starting to edit a second note drops unsaved changes, without asking.
- **Known caveats:**
  - Shift+Enter starts a new paragraph; there are no soft breaks.
  - `* item` is rewritten as `- item`.
  - TipTap's typing shortcuts can italicise `2*3*`.
  - `?about=` and share links that arrive mid-edit go into the hidden draft.

## Next work

1. **Get #222–#253 checked in a browser.** No agent has run them in a real browser,
   and each PR body has a checklist. The biggest outstanding checks:
   - **Combat, 18c–18f (#235–#238):** step 18's Verify (three profiles, a whole fight)
     has not been run. In particular: drag to reorder, heal, the player's view, and
     everything at 390×844.
   - **Connections and loose ends, 19c–19e (#242–#244):** step 19's Verify (a DM and a
     player, phone and desktop): the panel, the evidence sheet, the graph's pan, pinch and
     edge taps, and linking a note from its loose end with the divider and Wiki counts
     dropping live.
   - **SRD reference, 20c–20d (#248–#249):** step 20's Verify 4 (a DM at 1280×800, a
     player at 390×844): REFERENCE rows in ⌘K, the card, + Wiki with its 409, the source
     line, `@Goblin Warrior ×4` in a combat, and "Use SRD stats".
   - **5eTools, 21b–21c (#252–#253)** (deferred with the rest of step 21, below; check
     it in the later pass): step 21's Verify 5 and 6 (a DM at 1280×800, a
     player at 390×844), with a local index. To build one, get a copy of the 5etools source
     data yourself (e.g. a checkout of its source repository next to this repo, as
     `5etools-src/`, which is git-ignored). Run `pnpm 5etools:build --from <that folder>`,
     which writes `.data/5etools/index.json`, then start the API with
     `Reference__FiveETools__IndexPath=../../.data/5etools/index.json` (`pnpm dev`'s launch
     profile sets it). Check:
     - ⌘K "goblin": SRD rows first, then 5eTools rows reading "Monster · CR … · MM
       (5eTools)" with ↗.
     - Enter on "beholder" opens 5etools in a new tab, and ⌘K stays open.
     - + Wiki as "The Eye": the dialog shows the Stats line, then the source line and its
       tooltip.
     - `@The Eye` in a Draft combat.
     - On a phone, "fireball" and "Bag of Holding".
     - "Use 5eTools stats".
     - `/reference/5etools/<id>` typed by hand shows the "Open on 5eTools" page.
     - Renaming the index turns it all off.

     The first real build may fail loudly on a shape the invented fixture lacks (21a's
     "Not checked against real data"). Add the mapping on top of the stack.
   - **Restart the API first:** 20b (#247) added endpoints and `Entry.Source`, and a
     running API from before it answers 404 for them. 21c (#253) added `bookTitle`, which
     an older API leaves out: the tooltip is missing, and nothing else breaks. Restarting
     logs everyone out (see the follow-ups).

   Fix what the user reports on top of the stack.
2. Steps 17 to 21 are closed (PR tables ticked, README `done`; step 21 except 21d). Step 19 closed the MVP
   line. Before 20a merges, the user should confirm the data source (Open5e's SRD 5.2
   fixtures at a pinned sha, whose licence 20a checked) and the attribution text (step
   20's Notes, "Why Open5e's fixtures" and "As built, 20a").
3. **Deferred by the user (2026-09-28): 5eTools and D&D Beyond** are to be handled in a
   later pass, after the cleanup below. Nothing in this item or the next is to be run now.
   **Step 21 is done apart from 21d.** 21a–21c are #251–#253. 21d and step 21's
   "Decisions for the user" are **deferred**; the plan's defaults stand until the user
   takes them up:
   - **21d: delete the four Bestiary branches** (`git push origin --delete bestiary
     Bestiary_2025 Bestiary_2025_CopyParsing Bestiary_2025_project_refactor`). No agent runs
     this.
   - **Non-SRD Stats in the index** (decision 1): AC, HP dice and initiative from non-SRD
     books are in the index by default, and `--no-stats` drops them. Keep them or make
     `--no-stats` the default?
   - **Production** (decision 3): off by default (no `IndexPath`). Turning it on shows
     non-SRD names to everyone signed in.
   - **The real 5eTools data in the public repo's history** (decision 5):
     `Bestiary_2025_CopyParsing` has about 286 real `bestiary-*.json` files in its earlier
     commits. After deleting the branch, ask GitHub Support to purge the cached commits,
     or accept that they stay reachable by sha until GitHub garbage-collects them (forks
     keep them).
4. **Step 22 (the D&D Beyond link) is deferred.** Its step file,
   [22-ddb-link.md](22-ddb-link.md), is on the stack as #254 (`v2/22-ddb-plan`, on top of
   `v2/21c-5etools-web`), as a plan only. 22a–22c are not built.
   - Partial 22a API work is saved **locally only** in `git stash`, as "22a D&D Beyond link
     API - partial, deferred 2026-09-28" (stashed on `v2/22a-ddb-link-api`). It is not
     pushed. Leave the stash alone until the later pass picks step 22 up.
   - Step 22's "Decisions for the user" (above all, whether to turn the refresh on given
     D&D Beyond's Terms of Service) are deferred with it.
   - When it resumes: **no agent calls D&D Beyond**, in tests or by hand (step 22's Notes).
5. **Step 23 is next: in-browser suggestions.** Its step file is
   [23-suggestions.md](23-suggestions.md) (branch `v2/23-suggestions-plan`, on top of
   `v2/22-ddb-plan`). 23a is done: the user chose GLiNER (Laya dropped), and the spike
   picked **GLiNER small v2.5** (small v2.1 pinned as the fallback); both are pinned in the
   step file's "Decision, 23a". 23b is done (the runtime; see "As built, 23b" for where the
   weights come from: `pnpm models:fetch`, or `pnpm models:export` to rebuild them from
   upstream). Next is 23c, then 23d–23e on top of it.
   - 23a's measurements on a phone and in a real browser are the user's: run
     `scripts/extraction-spike/` (`node serve.mjs`, http://localhost:3190) and fill in the
     table. Only Node on the Mac was measured.
   - No agent commits model weights. `public/models/` is git-ignored.
   - The user's decisions are in step 23's Notes, "Decisions for the user".
6. **Then step 24** (Discord import), whose step file is written just before it starts.
   **Step 25, the wiki redesign, runs alongside it.** Its step file is
   [25-wiki-redesign.md](25-wiki-redesign.md) (branch `v2/25a-wiki-redesign-plan`, on `dev`).
   25b–25g stack on it, one subagent per PR: drop "Add a note about X" from the wiki,
   Claim → "Played by" in copy, Summary | Notes tabs, the mobile collapse, the desktop side
   panel, and the wiki home. The mockups are at
   https://claude.ai/artifact/8YdLCzEPqDmnrpkgyhFWTR. Only UI copy is renamed; code names stay.
7. **Then a cleanup pass** over "Follow-ups found this round" below, before the deferred
   5eTools and D&D Beyond work is picked up again.

## Follow-ups found this round

- `components/Wiki/ArticleBlockEditor.vue` still uses a textarea with its own
  toolbar. Move it to `<ComposerEditor>` so articles get the same shortcuts and
  mentions. Its secret-block toggle needs a mark or node.
- `utils/composer.ts` still holds the textarea helpers (`applyEdit`,
  `toggleInline`, `toggleList`) for ArticleBlockEditor. Delete them after that move.
- Prettier `--check` fails on several web files from before this round. That's
  worth one `style(web)` PR on its own.
- The Promote dialog's Quote shows raw markdown (`**x**`, `@[Name]`). It should use
  the TipTap editor.
- shadcn's `DialogContent` passes `style` to a Teleport, which logs Vue's
  "extraneous non-props attributes" warning.
- `insertMentionTrigger` is auto-imported twice, from `utils/editorExtensions.ts` and
  `utils/mentions.ts` (Nuxt warns "Duplicated imports"). Keep one.
- `EntryHistoryTests.AnNpcsStats_AreLeftOutOfAPlayersHistory` is flaky: it asserts
  "5d8" is absent, and a random GUID in the payload can contain it.
- Restarting the API logs every user out: the data-protection signing keys are not
  persisted in dev.
- The combat add dialog's HP placeholder "2d6+2" reads like a default next to
  Initiative's real "1d20" default. Make it an obvious example, or leave it empty.
- `nuxi typecheck` does not catch an unknown component tag: `<NoteMarkdown>` instead of
  `<SessionNoteMarkdown>` renders as an empty unknown element (19c shipped one). A CI
  check that every PascalCase tag in `.vue` templates resolves in
  `.nuxt/components.d.ts` (or is imported) would catch it.
- **SRD sizes are wrong for 84 creatures.** Open5e's fixtures have one size per creature
  and no "tiny", so 84 show "small": SRD 5.2's Tiny ones (Bat, Cat, Imp, Sprite…) and its
  "Medium or Small" ones (the Vampire, Bandit, Mage, Priest, the were-creatures). Fix it
  with a hand-checked size table in `scripts/srd/build-srd52.mjs`, checked against the SRD
  5.2 PDF, with `size` as a string like "Medium or Small" (step 20, "As built, 20c").
- **CR 0 XP in 20a was invented:** 10 when the creature has a damaging action, else 0
  (Giant Fly, Seahorse and Shrieker Fungus get 0). Check each CR 0 creature against the
  SRD 5.2 PDF and put the real numbers in the build script.
- **Fixed in 21c:** the stat-block card header read "AC 15 ·Initiative", with the space
  before the dot and none after it. The spaces are now inside the dot's span.
- The ⌘K row's 5eTools line is parsed on the web from the API's `detail` ("{label} ·
  {book}"). It would be sturdier to send `book` on `SearchReferenceHit` too (the item
  endpoint already has it) and stop splitting strings.
- The source line's tooltip is a `title` attribute, so touch screens never show it. A
  popover would work on phones, if the book's title turns out to matter there.
