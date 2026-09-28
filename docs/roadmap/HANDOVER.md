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
  README's Conventions section gives the six sections; `17-search.md` is the
  latest example.
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

1. **Get #222–#244 checked in a browser.** No agent has run them in a real browser,
   and each PR body has a checklist. The biggest outstanding checks:
   - **Combat, 18c–18f (#235–#238):** step 18's Verify (three profiles, a whole fight)
     has not been run. In particular: drag to reorder, heal, the player's view, and
     everything at 390×844.
   - **Connections and loose ends, 19c–19e (#242–#244):** step 19's Verify (a DM and a
     player, phone and desktop): the panel, the evidence sheet, the graph's pan, pinch and
     edge taps, and linking a note from its loose end with the divider and Wiki counts
     dropping live.

   Fix what the user reports on top of the stack.
2. Steps 17, 18 and 19 are closed (PR tables ticked, README `done`). Step 19 closes
   the MVP line.
3. **Step 20 is next**, then 21–24 in README order. Write each step file first, as its
   own docs PR at the bottom of that step's stack.

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
