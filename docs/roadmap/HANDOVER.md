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

1. **Get #222–#249 checked in a browser.** No agent has run them in a real browser,
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
   - **Restart the API first:** 20b (#247) added endpoints and `Entry.Source`, and a
     running API from before it answers 404 for them. Restarting logs everyone out (see
     the follow-ups).

   Fix what the user reports on top of the stack.
2. Steps 17 to 20 are closed (PR tables ticked, README `done`). Step 19 closed the MVP
   line. Before 20a merges, the user should confirm the data source (Open5e's SRD 5.2
   fixtures at a pinned sha, whose licence 20a checked) and the attribution text (step
   20's Notes, "Why Open5e's fixtures" and "As built, 20a").
3. **Step 21 is next: the 5eTools index.** Its step file is
   [21-5etools-index.md](21-5etools-index.md) (#250, on top of `v2/20d-add-to-wiki`). Run
   21a–21c as stacked PRs on top of it. 21d, deleting the four Bestiary branches, is the
   user's to run, not an agent's. Before 21a starts, the user should answer the step's
   "Decisions for the user" (Stats in the index, no download, off in production, SRD
   duplicates, purging the 5eTools data already in the public repo's
   `Bestiary_2025_CopyParsing` history); the plan's defaults stand until they do. Then run
   22–24 in README order.

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
