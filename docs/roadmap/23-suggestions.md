# 23 — In-browser suggestions

## Goal

A small **zero-shot extraction model runs in the member's browser** and proposes
**suggestions** (glossary §1, design §11a): "this span looks like a Place", or "this matches
the existing entry @Rellan Ashvale". They show on the author's own unlinked notes in **Loose
ends** ("Unlinked · 2 suggestions") and inline on the author's own note cards ("✨ 3"). A
suggestion has **no effect until the author accepts it**: accepting one links the span as a
normal mention, or creates the entry the author confirmed and links it, in one `PUT notes/{id}`
by the author. The model never creates an entry, a mention or anything else by itself.

**Suggestions, never facts.** The accepted mention is the member's, and its event also records
the model, its version and its confidence (the `Actor.Model` seam, design §9). So "human-asserted
only" is a filter over what is already stored, and a bad model version can be **reverted** by
name and version without touching anything a human wrote by hand.

**Note text never leaves the device for extraction.** Inference runs in a Web Worker on
`onnxruntime-web`. There is no inference server and no API bill (invariant 10). The only things
that cross the network are the model weights (downloaded once, from the app's own static files
by default) and the **span strings** the model found, sent to the app's own API so the entry
matcher can answer "is this an existing entry?" under the viewer's visibility. Those spans come
from notes the API already stores.

**Which model** is decided by a spike (23a) between **GLiNER** and **Laya** (Notes, "What
Laya is"). 23a measures download size, load time, inference latency on a typical note and
quality on a small invented test set, and records the choice in this file before 23b builds on
it.

"Running" at the end: a player on a desktop opens Loose ends, taps "✨ Find suggestions", accepts
the one-time download, and within seconds sees "✨ **Rellan** → @Rellan Ashvale?" and "✨
**Greyhollow Keep** looks like a Place · + Create" on their unlinked notes. One tap links the
first; the second opens a confirm with the name and the Place chip already picked. The note's
history reads "Linked @Rellan Ashvale · ✨ suggested by gliner_small-v2.1 (0.87)". On a phone on
mobile data nothing downloads until they say so. A DM who dislikes a model version reverts
their own accepted suggestions from it in one action, and the mentions go back to plain text.

The step ships as five PRs stacked with `gh stack` on top of this file's docs PR, which sits on
step 22's plan (#254); this file is #255. Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 23a | `v2/23a-extraction-spike` | The spike: GLiNER vs Laya, measured, and the decision | The app unchanged for users. A dev-only page runs each candidate over the invented test set and prints size, load, latency and F1. The decision is written into this file | [ ] |
| 23b | `v2/23b-extractor-runtime` | The extractor: worker, lazy load, weights, cache, device setting (web) | Nothing visible except "Suggestions on this device" on the Me page. The model is fetched only on request, cached, and run in a worker; the normal bundle does not grow by more than a few KB | [ ] |
| 23c | `v2/23c-suggestions-api` | Match, provenance and revert (API) | 22's app unchanged in the browser. `POST suggestions/match` matches spans; `PUT notes/{id}` accepts a `suggestion` and records `Actor.Model`; the note history shows it; `POST suggestions/revert` unlinks one model version's mentions for their author | [ ] |
| 23d | `v2/23d-suggestions-loose-ends` | Suggestions in Loose ends (web) | Unlinked notes on the loose-ends page offer ✨ suggestions beside 19's link suggestions; accepting links or creates-and-links | [ ] |
| 23e | `v2/23e-suggestions-inline` | Inline on the author's notes, history, revert (web) | "✨ 3" on the author's own note cards, the ✨ line in note history, and "Accepted suggestions" with Revert on the Me page. The step's Verify passes | [ ] |

Suggestions in the composer while typing, on other members' notes, on article blocks and on
images, a server-side model, fine-tuning, and the Discord import (24) are not in 23 (Notes,
"Not in 23").

## Depends on

- **15**: mentions stored by entry id (`@[text](entry:id)`, invariant 6), `NewEntries` on
  `PUT notes/{id}` (create-and-mention in one write), `MentionParser`, entry kinds and
  `Composer/KindChips.vue`, the `Actor` record (`Features/Campaigns/Models/Actor.cs`, which
  says a `Model { Name, Version, Confidence }` case comes later) and note history
  (`GetSessionNoteHistory`).
- **17**: `EntryMatcher.MatchAsync` (one round trip for many spans, viewer visibility, merged
  entries dropped). Its doc comment already names step 23 as a caller.
- **19**: loose ends and link suggestions: `LooseEnds.SuggestLinks`, `LinkSpans` (`From`,
  `Accepts`, `Pick`), the loose-ends page and `LooseEnds/{LooseEndList,LooseEndRow,LinkSuggestions}.vue`,
  and `utils/looseEnds.ts` (`findSpan`, `linkSpan`, `withHeld`). 19's Notes, "Seams for later
  steps", says suggestions are `LinkSuggestions.vue` fed by the model's spans.
- **14/15 composer**: `useComposerEdit` for "Edit" from a suggestion, and the TipTap editor
  (`components/Composer/Editor.vue`, `utils/editorExtensions.ts`, the `@` picker) only as the
  place the author finishes a link by hand. 23 adds nothing to the editor.

These parts of [12-v2-design-session.md](12-v2-design-session.md) are binding: the glossary
(§1: **Suggestion**, **Link suggestion**, **Entry matcher**, **Actor**), §9's provenance, §11a
all of it, and the invariants (§10), especially 4 (only the author edits a note), 5 (visibility
on the server), 6, 9 (every event carries an Actor), 10 (no paid services) and 11 (mobile).

README lists 15 and 17. 19 is on the stack already and 23 builds on its loose ends.

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web), except where they start at the repo root.

What exists today: nothing runs a model. The seams are `Actor` (a record with only
`MemberId`), `EntryMatcher` (named for 23 in its comments), 19's `LinkSuggestions.vue` and
`LinkSpans`, and the service worker `public/sw.js`, which precaches nothing and must stay that
way (offline is out of scope, §12).

**This PR (docs)**
- `docs/roadmap/23-suggestions.md`: this file
- `docs/roadmap/README.md`: link step 23, `in progress`; mark step 21's 21d and its decisions,
  and step 22, **deferred**
- `docs/roadmap/HANDOVER.md`: the deferrals, the stash, and "Next work" pointing here

**23a**
- Web add `pages/dev/extraction.vue` (dev only: 404 unless `import.meta.dev`), `utils/extraction/spike/{runGliner,runLaya,score}.ts`
- Web add `tests/fixtures/extraction/{notes.json,entries.json,README.md}` (the invented test set), `tests/unit/extractionScore.test.ts`
- Web modify `package.json` (the candidates' runtime packages as `devDependencies` for now)
- `docs/roadmap/23-suggestions.md`: the measurements table and "Decision, 23a" in Notes

**23b**
- Web add `workers/extractor.worker.ts`, `utils/extraction/{extractor,spans,modelSource,modelCache,deviceSetting}.ts`, `composables/useExtractor.ts`
- Web add `components/Me/SuggestionsSetting.vue`; modify `pages/app/me.vue`
- Web modify `package.json` (the chosen runtime as a dependency, the spike's other one removed), `nuxt.config.ts` (`runtimeConfig.public.suggestions`, the worker build, no precache), `.gitignore` (`public/models/`)
- Root add `scripts/models/fetch-model.mjs` and a `pnpm models:fetch` script; modify `apps/TakeInitiative.Web/Dockerfile` (fetch before `build`), `NOTICE` (the model's licence and attribution)
- Web add `tests/unit/{extractionSpans,modelCache,deviceSetting}.test.ts`

**23c**
- API modify `src/Features/Campaigns/Models/Actor.cs` (`ModelSuggestion? Model`)
- API add `src/Features/Suggestions/{SuggestedMention,SuggestionEdit}.cs`, `src/Features/Suggestions/Api/{PostSuggestionMatch,PostSuggestionRevert,GetSuggestionModels}/*.cs`
- API modify `src/Features/Sessions/Api/PutSessionNote/PutSessionNote.cs` (the optional `Suggestion`), `src/Features/Sessions/Models/SessionNote.cs` (`SuggestedMentions`), `src/Features/Sessions/Models/Events/SessionNoteEdited.cs` (doc comment only: the Actor may carry a model), `src/Features/Sessions/Api/GetSessionNoteHistory/GetSessionNoteHistory.cs` (the model on a version), `src/Features/Entries/Mentions/NewEntries.cs` (created entries carry the same Actor)
- Tests add `Scopes/Unit/SuggestionEditTests.cs`, `Scopes/Integration/Features/Suggestions/{SuggestionMatchTests,SuggestionAcceptTests,SuggestionRevertTests,SuggestionLeakTests}.cs`
- Web `utils/api/schema.d.ts`: regenerated

**23d**
- Web add `components/LooseEnds/ModelSuggestions.vue`, `components/Suggestions/{CreateFromSuggestion,FindSuggestionsButton,DownloadPrompt}.vue`, `utils/suggestions.ts` (merge with link suggestions, labels, local dismiss), `utils/api/suggestion/postSuggestionMatchRequest.ts`, `utils/queries/suggestions.ts`
- Web modify `components/LooseEnds/{LooseEndRow,LinkSuggestions}.vue`, `utils/looseEnds.ts` (row label with the suggestion count), `utils/api/session/putSessionNoteRequest.ts` (`suggestion`)
- Web add `tests/unit/suggestions.test.ts`

**23e**
- Web add `components/Suggestions/{NoteSuggestionsChip,NoteSuggestionsSheet,AcceptedSuggestions}.vue`, `utils/api/suggestion/{postSuggestionRevertRequest,getSuggestionModelsRequest}.ts`
- Web modify `components/Session/SessionNoteCard.vue` (the chip, author only), the note history view (the ✨ line), `pages/app/me.vue` (Accepted suggestions), `composables/useCampaignHub.ts` (nothing new pushed; invalidate the suggestion-models query on `sessionNoteUpserted`)
- Web modify `tests/unit/suggestions.test.ts`
- `docs/roadmap/23-suggestions.md`, `README.md`, `HANDOVER.md`: tick and close the step

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/22-ddb-plan
gh stack add v2/23-suggestions-plan
```

Add each sub-step on top with `gh stack add v2/23a-extraction-spike` and so on. Step 22's code
is deferred (README), so 23a sits directly on its plan.

**Glossary check.** **Suggestion** is in §1: "a mention or entry proposed by a model. It has no
effect until a member accepts it." **Link suggestion** (19) stays the entry matcher's trigram
hit, not a model's. The UI marks model suggestions with ✨ and link suggestions without, and
the code keeps the two names apart (`ModelSuggestion` vs `LinkSuggestion`). No new nouns: the
model is "the suggestion model" in the UI, never "AI", "auto-tag" or "prediction" (§1's
rejected words).

### 23a. The spike: GLiNER vs Laya

Measure, then pick. The spike's code is a dev page and a scorer; only the fixture and the scorer
survive into 23b.

1. **The invented test set** (`tests/fixtures/extraction/`). Invented, so no module text or
   real campaign notes are committed:
   - `entries.json`: about 25 entries with kinds and aliases (Rellan Ashvale, alias "Rellan";
     Greyhollow Keep; the Ember Court; the Salt Road; the Moonfall Blade; the Night of Ash…).
   - `notes.json`: 40 notes of three lengths (15 short, ~120 characters; 20 typical, ~400; 5
     long, ~1,500) with **gold spans**: start, length, kind (`Character`, `Place`, `Faction`,
     `Item`, `Event`). They include the hard cases: lower-case names ("met rellan"),
     possessives ("Rellan's map"), multi-word names, names at a sentence start, an existing
     `@[…](entry:…)` mention (never a span), markdown (`**bold**`, lists), dice (`2d6+3`),
     numbers, "the" before a name, a name that is also a common word ("Ash"), table talk
     ("pizza's here"), and three notes with no names at all.
   - `README.md`: how the set was made and that it is invented.
2. **The candidates.** Each runs in a Web Worker in the dev page, WASM backend first, WebGPU
   where the browser has it. Only checkpoints whose licence allows redistribution are
   candidates (Apache-2.0 or MIT; `gliner_base` and other CC-BY-NC GLiNER checkpoints are
   out):
   - **GLiNER small** (`urchade/gliner_small-v2.1`, Apache-2.0), ONNX int8. Labels are the
     kinds in lower case ("character", "place", "faction", "item", "event") passed at
     inference; threshold 0.5 to start, tuned on the set. Run with the `gliner` npm package
     (GLiNER.js, on `onnxruntime-web`) or, if it lags, a hand-written span decoder over
     `@huggingface/transformers`' tokenizer and `onnxruntime-web`. Record which, and its licence.
   - **GLiNER multi** (`onnx-community/gliner_multi-v2.1`, Apache-2.0), ONNX int8, the same
     way. It is bigger; it is here for tables that write in other languages.
   - **Laya** (`convaiinnovations/laya`, Apache-2.0; 421M parameters; ModernBERT-large), int8
     ONNX, through `onnxruntime-web`. Laya answers typed questions and does not find spans
     (Notes, "What Laya is"), so it is run in the only way it can serve: **candidate spans**
     from a TypeScript port of 19's `LinkSpans.From` rules (capitalised runs, words of four
     letters or more, the stop list), then one `choice` question per candidate: "What is
     '{span}' in this note?" with options Character, Place, Faction, Item, Event and "not a
     name". Keep answers whose best option is a kind and whose probability is ≥ 0.5.
   - If a GLiNER v2.5 or GLiNER2 checkpoint with ONNX weights and a permissive licence exists
     when 23a runs, add it as a fourth row. Do not go looking for more.
3. **What is measured**, for each candidate, on the user's desktop (1280×800, Chrome) and on a
   mid-range phone (the user's, in Safari or Chrome), written into the table below:
   - **Download**: bytes over the wire for weights, tokenizer and runtime WASM, compressed
     and not.
   - **Load**: cold (empty cache: fetch + compile + session create) and warm (from Cache
     API: read + compile + session create), in seconds.
   - **Latency**: p50 and p95 per note for the short, typical and long notes, in the worker,
     after one warm-up run. Long notes are chunked on sentence boundaries to the model's token
     limit (GLiNER: 384 tokens) and the spans shifted back.
   - **Memory**: peak `performance.measureUserAgentSpecificMemory()` where available, else the
     worker's heap from DevTools by hand. On the phone, note whether the tab reloads.
   - **Quality** (`score.ts`, unit-tested): span precision, recall and F1 against the gold
     spans (exact offsets; a second number with ±1-word overlap), kind accuracy on matched
     spans, and **top-3 precision per note** (the UI shows at most three, 19's cap).
4. **The gates.** A candidate is eligible only if, on the desktop: download ≤ 250 MB
   uncompressed, warm load ≤ 5 s, typical-note p50 ≤ 1.5 s; on the phone: warm load ≤ 10 s,
   typical-note p50 ≤ 4 s, no tab reload; and top-3 precision ≥ 0.6. **Pick** the eligible
   candidate with the highest span F1, and the smaller download on a tie within 0.03. If none
   is eligible, pick the best and set the phone default to "Off" (23b), and write that down.
5. **Record the decision** in Notes, "Decision, 23a": the table, the pick, the threshold, the
   runtime package and version, the exact Hugging Face repo and **revision sha**, the files
   used, their sha256, and the licence text's location. README's step 23 goal line changes
   from "GLiNER vs Laya" to the pick.
6. **Tests.** `extractionScore.test.ts`: exact and overlap matching, kind accuracy, top-3,
   empty gold sets. The dev page itself is not tested.

| Candidate | Download | Load cold / warm (desktop) | Typical note p50 (desktop / phone) | Span F1 | Top-3 precision | Eligible |
|---|---|---|---|---|---|---|
| GLiNER small v2.1 | _23a_ | | | | | |
| GLiNER multi v2.1 | _23a_ | | | | | |
| Laya (EN) + candidate spans | _23a_ | | | | | |

### 23b. The extractor runtime (web)

Built for the model 23a picked. Names below say "the model".

1. **The worker** (`workers/extractor.worker.ts`), created with
   `new Worker(new URL("../workers/extractor.worker.ts", import.meta.url), { type: "module" })`
   only when `useExtractor().ensure()` is first called. The runtime package and the model code
   are imported **only inside the worker**, so neither is in any page chunk. Messages:
   `load` (with the model source), `progress` (bytes), `ready`, `extract` (`{ id, text }` →
   `{ id, spans: [{ start, length, text, kind, confidence }] }`), `error`. One note at a time,
   in a queue; a newer request for the same note id replaces an older one.
2. **Spans** (`utils/extraction/spans.ts`, pure, shared with the worker):
   - Before inference, mentions (`@[text](entry:id)`), link targets, inline code and URLs are
     masked with spaces of the same length, so offsets stay the note's own (UTF-16, as 19's)
     and nothing already linked is suggested. Markdown syntax characters are masked the same
     way.
   - After: drop spans under the threshold, spans that are dice (`/^\d*d\d+/`), numbers or
     stop-list words (the same list as `LinkSpans`), strip a leading "the" and a trailing
     `'s`; keep the longest of overlapping spans, then the higher confidence; one span per
     distinct folded text; at most 10 a note sent to matching.
3. **Where the weights come from** (`utils/extraction/modelSource.ts`), from
   `runtimeConfig.public.suggestions`:
   `{ modelId, revision, baseUrl, files: [{ path, sha256, bytes }], runtimeWasmBaseUrl }`.
   - **Default: self-hosted** in the web app's static files, `/models/{modelId}/{revision}/…`.
     `pnpm models:fetch` (`scripts/models/fetch-model.mjs`) downloads the pinned files from
     `https://huggingface.co/{repo}/resolve/{revision}/…`, checks each sha256, and writes them
     to `apps/TakeInitiative.Web/public/models/`, which is git-ignored. The web Dockerfile runs
     it before `pnpm build`, so production serves them itself. `pnpm dev` runs it once if the
     folder is missing.
   - **Optional: Hugging Face directly.** `NUXT_PUBLIC_SUGGESTIONS_BASE_URL=https://huggingface.co/{repo}/resolve/{revision}`
     skips the fetch step (useful in dev). The browser then asks Hugging Face for the files:
     it learns the member's IP and that they use the model, never any note text.
   - The runtime's own WASM files are served from the app too (`runtimeWasmBaseUrl`, copied
     from `node_modules` at build), never the runtime's default CDN.
   - The web app checks each downloaded file's sha256 (`crypto.subtle.digest`) before using it,
     wherever it came from.
4. **Caching** (`utils/extraction/modelCache.ts`). Files go into the **Cache API**, cache
   `ti-models-v1`, keyed by the full URL, which contains the revision, so a new version is a
   new key. After a successful load, keys for other revisions are deleted. A small
   index lives in `localStorage` (`ti.model.cached`: model id, revision, bytes)
   so the Me page can show "183 MB on this device" without opening the cache. Before a download:
   `navigator.storage.estimate()` must show room for twice the bytes, else "Not enough storage
   on this device". After it, `navigator.storage.persist()` is asked for, and a refusal is fine.
   The service worker does not touch the cache (`public/sw.js` stays as it is).
5. **Lazy loading.** Nothing loads with the app. The model is fetched only when a member asks
   for suggestions (23d's "✨ Find suggestions", 23e's chip), and it is loaded from the cache
   automatically on later visits **only when the device setting allows it** (below). Loading
   shows progress ("Downloading the suggestion model · 64 of 183 MB"), can be cancelled
   (aborts the fetch; a partial file is never cached), and failure shows the error with Retry.
6. **The device setting** (`utils/extraction/deviceSetting.ts`, `Me/SuggestionsSetting.vue`
   on `me.vue` under "This device"). Stored in `localStorage` (`ti.suggestions`), because it
   is about the device's data plan and storage, not the account:
   - **Off**: no ✨ anywhere on this device.
   - **Ask** (the default): suggestions are offered, and the first download needs a tap on
     "Download (183 MB)". Once cached, the model loads when a page that shows suggestions
     opens, except on a metered connection (below), where it waits for a tap.
   - **Automatic**: downloads and loads without asking, except on a metered connection.
   - **Metered** means `navigator.connection.saveData`, or `type === "cellular"`, or
     `effectiveType` of `slow-2g`, `2g` or `3g`. Where the API is missing (Safari, so every
     iPhone), a phone (`useDevice().isMobile`) is treated as metered and a desktop as not.
     On a metered connection the first download is never started without an explicit tap, in
     any setting but Off.
   - The page also shows the model's name, version, licence, where its files come from, the
     space used, and **Remove from this device** (deletes the cache entries).
7. **The composable** (`composables/useExtractor.ts`): `state` (`off | idle | needsConsent |
   downloading | loading | ready | error`), `progress`, `ensure({ consent })`, `extract(noteId,
   text)`, `remove()`, `model` (`{ name, version }`). One worker per tab, kept for the tab's
   life once loaded. Results are memoised per `(noteId, text hash, model version)` in memory
   only.
8. **Bundle check.** `pnpm build`'s client output is compared before and after: the entry and
   page chunks grow by at most a few KB (the composable, the setting); the runtime and
   tokenizer appear only in the worker chunk.
9. **Licence.** The model's licence and attribution go in the repo's `NOTICE` and on the Me
   page. The fetch script refuses a file whose sha256 differs from the pinned one.
10. **Tests.** `extractionSpans.test.ts` (masking keeps offsets, mentions and markdown never
    spans, dice and stop words dropped, overlap and dedupe, the cap, unicode);
    `modelCache.test.ts` (keys by revision, old revisions dropped, partial downloads never
    stored, with a fake `caches`); `deviceSetting.test.ts` (the metered rule with and without
    `navigator.connection`, phone vs desktop, each setting).

### 23c. Match, provenance and revert (API)

1. **`Actor` gains the model** (`Actor.cs`):
   `public sealed record Actor(Guid MemberId, ModelSuggestion? Model = null)` with
   `public sealed record ModelSuggestion(string Name, string Version, double Confidence)`.
   The member is still the actor: a suggestion accepted is the member's act, and the model is
   recorded as what proposed it (§11a). Existing events deserialise with `Model` null, so no
   migration. `Actor.Member(id)` is unchanged.
2. **`POST /api/campaigns/{CampaignId}/suggestions/match`**, any member:

   ```jsonc
   // request
   { "spans": [ { "text": "Rellan", "kind": "Character" }, { "text": "Greyhollow Keep", "kind": "Place" } ] }
   // response, in request order
   { "matches": [ { "entry": EntrySummaryResponse, "similarity": 0.93 }, null ] }
   ```

   - One `EntryMatcher.MatchAsync` call (`Take = 2`, `MinSimilarity = 0.6`), and
     `LinkSpans.Accepts` decides, exactly as 19b's link suggestions do. The model's `kind` is
     not used to filter (a Place called "Rellan's Rest" should still match), only returned for
     the create path.
   - At most 50 spans a request, each 1–80 characters (400 otherwise). Nothing is stored or
     logged beyond the usual request line; the span strings are not written to logs.
   - The matcher applies the viewer's visibility and drops merged entries, so a hidden entry
     is never a match (a `null` looks the same as "no such entry").
3. **Accepting: `PUT notes/{id}` takes an optional `suggestion`:**

   ```jsonc
   { "text": "…@[Rellan](entry:…)…", "isRecap": false,
     "newEntries": [ /* only when creating */ ],
     "suggestion": { "model": "gliner_small-v2.1", "version": "urchade/gliner_small-v2.1@<sha>+int8",
                     "confidence": 0.87, "start": 12, "length": 6, "entryId": "…" } }
   ```

   - `SuggestionEdit.Check(oldText, newText, suggestion)` (pure) accepts the edit only when
     the new text is the old text with **exactly** the span at `start, length` replaced by
     `@[{span}](entry:{entryId})`. Anything else is a 400 (`suggestion`), so provenance can
     never be attached to an arbitrary edit. The author rule (invariant 4) is unchanged.
   - When it passes, `SessionNoteEdited` is appended with `Actor(member, new ModelSuggestion(…))`,
     and a created entry's `EntryCreated` (from `newEntries`, whose one entry must be
     `entryId`) carries the same Actor.
   - `confidence` is clamped to [0, 1]; `model` ≤ 80 and `version` ≤ 200 characters.
4. **`SessionNote.SuggestedMentions`**: `[{ entryId, start, model, version }]` kept by the
   projection. It gains a row on an accepted suggestion, and a row is dropped when a later edit
   no longer has that mention at that text (offsets are re-found by `MentionParser` on each
   edit). This is what "human-asserted only" and revert read, so nothing has to replay events.
   A mention the author typed by hand is never in it, even if a suggestion for the same entry
   was dismissed.
5. **History.** `GET notes/{id}/history` returns each version's `actor.model`
   (`{ name, version, confidence }` or null).
6. **`GET /api/campaigns/{CampaignId}/suggestions/models`**: for the caller's own notes,
   `[{ model, version, mentions: 14, notes: 9 }]` from `SuggestedMentions`. Only the caller's,
   because only the author can revert (below).
7. **`POST /api/campaigns/{CampaignId}/suggestions/revert`** `{ model, version }`: for every
   **note the caller authored** with `SuggestedMentions` from that model and version, rewrite
   each such mention `@[text](entry:id)` back to `text` if it is still there as accepted, and
   append one `SessionNoteEdited` per note with `Actor.Member(caller)` (a plain edit: the
   revert is the author's act). Mentions the author typed by hand, even of the same entry, are
   untouched. Entries created from suggestions are **not deleted** (there is no entry delete);
   the response lists them (`createdEntries`) so the UI can point at them. Answer:
   `{ notes: 9, mentions: 14, createdEntries: [EntrySummaryResponse] }`. Pushes go out as for
   any note edit.
8. **Tests.**
   - `SuggestionEditTests` (unit): the one-span rule, a span that moved, two spans at once
     (rejected), unicode, a span already inside a mention.
   - `SuggestionMatchTests`: order, nulls, the caps, an alias, `Accepts` rejecting a
     substring ("rock" in "Brockton").
   - `SuggestionAcceptTests`: the event's Actor has the model; history shows it; create-and-link
     puts the model on `EntryCreated`; a non-author is 403; a mismatched edit is 400.
   - `SuggestionRevertTests`: only that version's mentions go, hand-typed ones stay, only the
     caller's notes, an edited-away mention is skipped, created entries are listed and kept.
   - `SuggestionLeakTests`: a player's match never returns a `DM`, `Only me` (another
     member's) or merged entry, for an exact name or an alias; `models` never counts another
     member's notes.

### 23d. Suggestions in Loose ends (web)

1. **Where.** On `wiki/loose-ends`, each `UnlinkedNote` and captioned `UntaggedImageNote` row
   (the viewer's own, by 19's rule) gets model suggestions under 19's link suggestions.
2. **Starting.** With the device setting on and the model not loaded, the page shows
   `Suggestions/FindSuggestionsButton.vue`: "✨ Find suggestions". In **Ask** before the first
   download, or on a metered connection, a tap opens `DownloadPrompt.vue` ("The suggestion
   model runs on this device. Your notes are not sent anywhere to be read. It is a one-time
   183 MB download. [Download] [Not now]" with "Always ask on mobile data" / the setting's
   link). Once the model is ready, the page extracts every listed note in the worker, newest
   first, and the button becomes "✨ Looking at 12 notes…".
3. **Matching.** Spans from all listed notes go to **one** `POST suggestions/match` per 50
   spans. Each span becomes:
   - a **match** (an entry came back): "✨ **Rellan** → @Rellan Ashvale?", unless 19's link
     suggestions already offer that entry on that note (then the ✨ is added to 19's chip
     instead of a second chip);
   - a **new entry** (null): "✨ **Greyhollow Keep** looks like a Place · + Create".
   A note shows at most three model suggestions, matches first, then by confidence.
   `utils/suggestions.ts` does the merging and labels (pure).
4. **Accepting a match**: `linkSpan` (19's, with `findSpan`) and `PUT notes/{id}` with `text`,
   `isRecap` and the `suggestion`. The row resolves as 19e's do ("✓ Resolved", `withHeld`).
5. **Accepting a new entry**: `CreateFromSuggestion.vue`, a small dialog (a bottom sheet on a
   phone): the name, editable, prefilled with the span; the kind as `Composer/KindChips.vue`
   with the model's kind picked; the entry's visibility set the way the composer's
   `@` create sets it for a new entry in that note (check `NewEntries` and follow it). **Create and link** sends the rewritten text, `newEntries` with
   one entry and the `suggestion`. Nothing is created until that tap. If the name the author
   typed now matches an entry (checked with `match` on submit), the dialog offers "Link to
   @… instead".
6. **Dismissing**: ✕ on a chip hides that suggestion on this device (`localStorage`,
   `ti.suggestions.dismissed`: note id + folded span + model version; capped at 500, oldest
   dropped). Nothing is stored on the server (19's "No dismiss" holds: the loose end stays).
7. **Row label.** "Unlinked · 2 suggestions" (§11a) once extracted; plain "Unlinked" before or
   with the setting Off. The Wiki's and dividers' counts do not change: suggestions are not
   loose ends.
8. **Tests** (`suggestions.test.ts`): merging with link suggestions (same entry twice, the ✨
   marker), the three-per-note cap and order, labels, the dismiss key and cap, the
   create-dialog defaults (kind, visibility, name).

### 23e. Inline on the author's notes, history and revert (web)

1. **The chip.** On `Session/SessionNoteCard.vue`, **only on the viewer's own notes** (only
   the author can accept, invariant 4) and only when the model is `ready` in this tab
   (never triggers a download), a note with suggestions shows "✨ 3" next to its actions.
   Notes are extracted lazily: the ones in view (an `IntersectionObserver`), when the worker
   is idle, a note at a time. A note that already mentions the suggested entry at that span is
   skipped.
2. **The sheet.** Tapping the chip opens `NoteSuggestionsSheet.vue` (bottom on a phone, right
   on desktop, like `Wiki/EvidenceSheet.vue`) with the same chips and actions as 23d, plus
   **Edit** (19e's `useComposerEdit` path) to finish by hand with the `@` picker.
3. **History.** The note history view shows "✨ suggested by {name} ({confidence})" on a
   version whose actor has a model.
4. **Accepted suggestions** (`Suggestions/AcceptedSuggestions.vue`, on `me.vue` under the
   device setting, per campaign the member is in): "gliner_small-v2.1 · 14 mentions in 9
   notes · **Revert**". Revert asks "Unlink the 14 mentions this model suggested in your
   notes? Mentions you typed yourself stay." and then lists `createdEntries` with links
   ("These entries were created from its suggestions and stay: …").
5. **Close the step**: tick this file's PR table, set README's status to `done`, update
   HANDOVER.

## Verify

1. `dotnet test` and `pnpm build` pass, `vitest` and `npx nuxi typecheck` pass,
   `schema.d.ts` is fresh, and CI is green on every PR in the stack. CI never downloads a
   model: the web tests use fakes, and `models:fetch` is not run in CI's build (the build
   works without `public/models/`).
2. **No note text leaves the device for extraction.** With DevTools' Network panel open during
   23d's flow, the only requests are the model files (from the app's own origin by default),
   `POST suggestions/match` whose bodies hold span strings only, and the `PUT notes/{id}` the
   author tapped. No request goes to Hugging Face or any other host unless the base URL was
   set to it.
3. **Lazy.** Loading the Campaign tab, the Wiki and ⌘K fetches no model file and no
   runtime WASM; the client build's entry chunk did not grow by more than a few KB (23b.8).
4. Two browser profiles, A (the owner, DM) at 1280 × 800 and B (a Player) at 390 × 844. The
   wiki has Rellan Ashvale (alias "Rellan", Everyone), the Ember Court (`DM`), and Mara (B's
   claimed character).
   1. B posts "met rellan at the gates of Greyhollow Keep, the Ember Court's spies watched".
      On B's loose-ends page, "✨ Find suggestions" asks before downloading (a phone counts
      as metered). After the download: "✨ **rellan** → @Rellan Ashvale?", "✨ **Greyhollow
      Keep** looks like a Place · + Create", and for "Ember Court" a **create** suggestion,
      never the DM's hidden entry (B's `match` response has `null` there).
   2. B links Rellan: the note shows the chip in the stream, and its history says "✨
      suggested by …". B creates Greyhollow Keep with the Place chip: one new entry, linked.
   3. A never sees B's suggestions or a ✨ chip on B's notes. A's own unlinked note gets
      them on A's device.
   4. B reloads: the model loads from the cache (no download in Network), and the ✨ chip
      appears on B's own note cards in view.
   5. B's Me page shows the model, its licence, the space used, and "1 mention in 1 note"
      under Accepted suggestions (plus the created entry's). **Revert**: "@Rellan Ashvale"
      goes back to "rellan" and "Greyhollow Keep" to plain text; the Greyhollow Keep entry is
      listed as kept. A mention B typed by hand in another note stays.
   6. **Remove from this device** empties the cache; the next Find asks again.
   7. Setting **Off** removes every ✨ from B's device, and nothing loads.
5. On B's phone: the download prompt, the create dialog and the sheet are reachable by touch,
   nothing sits under the keyboard or the home indicator, and the tab does not reload during
   extraction (23a's memory check).

## Notes / gotchas

### What Laya is

- **The design doc only says** "Laya: named by the user as a second small in-browser
  candidate" (§11a). Nothing else in the repo names it.
- **The likely match** is **Laya** by Convai Innovations (`convaiinnovations/laya` on Hugging
  Face, source on GitHub): an open, Apache-2.0 **typed-decision model**, 421M parameters on
  ModernBERT-large, with a 322M multilingual checkpoint on mmBERT-base. It takes a "state"
  (a text or JSON) and typed questions (`choice` among options you define at request time,
  `score` on an ordinal rubric, `noul` for a boolean probability) and returns calibrated
  probabilities in one forward pass. It never generates text. Community ports run it in the
  browser on ONNX Runtime Web (WebGPU with a WASM fallback), and one reports an int8 pack of
  about 479 MB and 2–5 s for a three-question call on WASM on two cores.
- **What that means here.** Laya **does not extract spans**: it can say which kind a given
  span is, but something else must find the spans. So in 23a it runs behind a candidate
  generator (19's `LinkSpans` rules, ported), one `choice` question per candidate. It is also
  documented as weak zero-shot (its base checkpoint scores about 0.36 on its own typed-decision
  benchmark, near random, and about 0.77 only after fine-tuning), and its `choice` questions
  degrade with many options. GLiNER is built for exactly this task (zero-shot NER with labels
  given at inference, character offsets out). **The expected pick is GLiNER small**, but 23a
  measures rather than assumes.
- **If this is not the Laya the user meant**, 23a still runs with GLiNER and whatever the user
  names, under the same gates. This identification came from a web search on 2026-09-28, not
  from the user.

### Decision, 23a

_Written by 23a: the measurements table (23a step 3), the pick, threshold, runtime package and
version, repo, revision sha, files and their sha256, and licence._

### Where the weights come from

- **Self-hosted by default.** The weights are served from the app's own origin, fetched at
  build time at a pinned revision and checked by sha256. Reasons: no third party sees who uses
  the app or when; the app does not break if a Hugging Face repo is renamed, gated or deleted;
  CORS and caching headers are the app's own. The cost is a web image a few hundred MB bigger,
  which is free on the hosting the app uses (invariant 10).
- **Not in git**, not even LFS: the files are large and binary, and the licence travels in
  `NOTICE`. `public/models/` is git-ignored.
- **Not in S3/MinIO.** That bucket holds members' images; mixing app assets in means a bucket
  policy for public reads that the image store does not have.
- **Hugging Face directly** is one environment variable away, for dev or a small deployment.
- **Why the Cache API, not IndexedDB.** It stores `Response`s by URL, streams large files
  without holding them in memory, and is what `@huggingface/transformers` uses itself.
  `localStorage` holds only the small index. The service worker stays out of it (no
  precaching, §12).

### Privacy

- The model runs in a worker on the member's device, on the member's own notes only.
- The span strings sent to `match` are words from notes the API already stores; they are not
  logged. The draft in the composer is never extracted in 23 (below), so no unsent text is
  sent anywhere.
- No telemetry: nothing reports what was suggested, accepted or dismissed, except the accepted
  suggestion's provenance on the author's own edit.

### Other notes

- **Only the author sees suggestions on a note.** Only the author can accept (invariant 4), so
  showing ✨ to anyone else is noise and would run a model over notes to no end. A DM who
  wants suggestions on a player's notes asks the player.
- **Suggestions are never stored on the server.** They are recomputed per device, and only the
  accepted ones leave a trace (the Actor and `SuggestedMentions`). This keeps "suggestions,
  never facts" literal: a model's opinion is not data until a member makes it theirs.
- **One suggestion per edit.** The one-span rule in `SuggestionEdit.Check` means each accepted
  suggestion is its own event with its own confidence, which is what revert by version needs.
  Accepting three is three small `PUT`s; that is fine at this scale.
- **Revert is per author.** Invariant 4: a DM cannot edit a player's note, so a DM cannot
  revert a player's accepted suggestions. Each member reverts their own. A campaign-wide revert
  by the DM would be the first write to another member's note (decision 4).
- **Human-asserted only.** Every mention not in `SuggestedMentions` was typed by a member. 23
  stores what the filter needs and uses it for revert; a "Human-linked only" toggle on the
  graph or connections is not built (Not in 23).
- **Kinds as labels.** The labels are the entry kinds (`Character`, `Place`, `Faction`, `Item`,
  `Event`) with no mapping (§11a). `Other` is never suggested: it is the "don't know" kind,
  and a span the model can't place is not suggested at all.
- **Matching before creating.** A span is matched against names and aliases first (§11a), and
  a create is offered only on `null`. The create dialog checks again on submit, because the
  author may have edited the name into an existing one.
- **Multithreading.** `onnxruntime-web`'s threads need cross-origin isolation (COOP/COEP
  headers), which would break images and links from other origins. 23 runs single-threaded
  WASM, and WebGPU where available, unless 23a shows threads are essential.
- **Mobile budget.** The first download never starts on a metered connection without a tap;
  iPhones always count as metered (no Network Information API). A phone that cannot run the
  model within 23a's gates gets "Off" as its default.
- **Seams for 24 (Discord import).** The import preview extracts the export's messages with
  the same worker before import ("✨ 61 mentions, 18 new entries", §11a). Those texts are not
  in the API yet, so matching their spans would send unsent words; 24 decides whether to match
  on the device against the Wiki list the viewer already has, instead of `match`.
- **Not in 23:** suggestions in the composer while typing (it would extract and match unsent
  drafts); suggestions on other members' notes, article blocks or image contents; a
  server-side model or any paid inference (invariant 10, §12); fine-tuning; a "human-linked
  only" filter in the UI; a campaign-wide DM revert; deleting entries created from suggestions;
  storing dismissals on the server; the Discord import (24).

### Decisions for the user

Each has the default this plan uses.

1. **Is Convai's Laya the one you meant?** *Default: yes, as described above; GLiNER is
   expected to win the spike.*
2. **Where the weights are served from.** *Default: self-hosted from the web app, fetched and
   checksum-checked at build time; Hugging Face only when an environment variable says so.*
3. **The default device setting.** *Default: "Ask" everywhere, and never a first download on a
   metered connection (every phone without the Network Information API) without a tap.*
4. **Revert scope.** *Default: each member reverts their own notes only. The alternative is a
   DM revert across the campaign, which breaks invariant 4.*
5. **The quality bar.** *Default: 23a's gates (top-3 precision ≥ 0.6, the latency and size
   limits). If nothing passes on phones, phones default to Off.*
