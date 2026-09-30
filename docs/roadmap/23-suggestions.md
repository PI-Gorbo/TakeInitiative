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

**The model is GLiNER** (the user's decision, 2026-09-28: "I'm interested in using GLiNER. It
is specialised for the task."). 23a picked the GLiNER variant by download size, load time,
inference latency and quality on a small invented test set, and pinned its weights (Notes,
"Decision, 23a") before 23b builds on it.

"Running" at the end: a player on a desktop opens Loose ends, taps "✨ Find suggestions", accepts
the one-time download, and within seconds sees "✨ **Rellan** → @Rellan Ashvale?" and "✨
**Greyhollow Keep** looks like a Place · + Create" on their unlinked notes. One tap links the
first; the second opens a confirm with the name and the Place chip already picked. The note's
history reads "Linked @Rellan Ashvale · ✨ suggested by gliner_small-v2.5 (0.87)". On a phone on
mobile data nothing downloads until they say so. A DM who dislikes a model version reverts
their own accepted suggestions from it in one action, and the mentions go back to plain text.

The step ships as five PRs stacked with `gh stack` on top of this file's docs PR, which sits on
step 22's plan (#254); this file is #255. Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 23a | `v2/23a-extraction-spike` | The spike: which GLiNER variant, measured, and its weights pinned | The app unchanged for users. A Node runner and a static browser harness (`scripts/extraction-spike/`) run each variant over the invented test set and print size, load, latency and F1. The decision is written into this file | [x] |
| 23b | `v2/23b-extractor-runtime` | The extractor: worker, lazy load, weights, cache, device setting (web) | Nothing visible except "Suggestions on this device" on the Me page. The model is fetched only on request, cached, and run in a worker; the normal bundle does not grow by more than a few KB | [x] |
| 23c | `v2/23c-suggestions-api` | Match, provenance and revert (API) | 22's app unchanged in the browser. `POST suggestions/match` matches spans; `PUT notes/{id}` accepts a `suggestion` and records `Actor.Model`; the note history shows it; `POST suggestions/revert` unlinks one model version's mentions for their author | [x] |
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

**23a** (as built: the runner lives in `scripts/`, not a dev page, so the web app gains no
dependency)
- Root add `scripts/extraction-spike/` (its own `package.json`, outside the workspace:
  `fetch-models.mjs`, `build-fixture.mjs`, `run.mjs`, `models.mjs`, `lib/{gliner,text,extract}.mjs`,
  and the browser harness `harness.html`, `harness-worker.mjs`, `serve.mjs`)
- Web add `utils/extraction/spike/score.ts` (the scorer, kept for 23b)
- Web add `tests/fixtures/extraction/{notes.json,entries.json,README.md}` (the invented test set), `tests/unit/extractionScore.test.ts`
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
- `docs/roadmap/23-suggestions.md`, `README.md`: tick and close the step

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

### 23a. The spike: which GLiNER variant

GLiNER is the user's choice (2026-09-28). 23a only picks the variant and pins its weights.
The spike's code is a Node runner, a static browser harness and a scorer in
`scripts/extraction-spike/`; only the fixture and the scorer survive into 23b.

1. **The invented test set** (`tests/fixtures/extraction/`). Invented, so no module text or
   real campaign notes are committed:
   - `entries.json`: 27 entries with kinds and aliases (Rellan Ashvale, alias "Rellan";
     Greyhollow Keep; the Ember Court; the Salt Road; the Moonfall Blade; the Night of Ash…).
   - `notes.json`: 40 notes of three lengths (15 short, ~100 characters; 20 typical, 300–400;
     5 long, 1,300–1,550) with **gold spans**: start, length, kind (`Character`, `Place`,
     `Faction`, `Item`, `Event`). They include the hard cases: lower-case names ("met
     rellan"), possessives ("Rellan's map"), multi-word names, names at a sentence start, an
     existing `@[…](entry:…)` mention (never a span), markdown (`**bold**`, lists), dice
     (`2d6+3`), numbers, "the" before a name, a name that is also a common word ("Ash"), table
     talk ("pizza's here"), and three notes with no names at all.
   - `README.md`: how the set was made and that it is invented. The JSON is generated by
     `scripts/extraction-spike/build-fixture.mjs`, never edited by hand.
2. **The candidates.** Only checkpoints whose licence allows redistribution (Apache-2.0 or MIT;
   `gliner_base` and other CC-BY-NC GLiNER checkpoints are out). Labels are the kinds in lower
   case ("character", "place", "faction", "item", "event") passed at inference. All run through
   a hand-written span decoder (`lib/gliner.mjs`) over `@huggingface/tokenizers` 0.2.0 and
   `onnxruntime-web` 1.30.0 (WASM, one thread); the `gliner` npm package was not used (0.0.19
   pins onnxruntime-web 1.19.2 and @xenova/transformers 2.17.2, and its span mask is a no-op).
   - **GLiNER small v2.1** (`onnx-community/gliner_small-v2.1`, `onnx/model_int8.onnx`).
   - **GLiNER multi v2.1** (`onnx-community/gliner_multi-v2.1`, `onnx/model_int8.onnx`), for
     tables that write in other languages.
   - The same two as `onnx/model_uint8.onnx`, to test whether the WASM quality loss (below) is
     the int8 kernels.
   - **GLiNER small v2.5** (`GG-QandV/gliner_small-v2.5-onnx`, `model_quantized.onnx`): the
     optional fourth row, a community int8 export of `gliner-community/gliner_small-v2.5`.
   - Laya (Convai) was considered and dropped by the user on 2026-09-28.
3. **What was measured**, in Node 24 on the user's Mac (Apple silicon), `node run.mjs`: the
   same ONNX files the browser would load, on `onnxruntime-web` WASM with one thread (what the
   browser worker runs) and, for comparison, `onnxruntime-node` CPU with one thread.
   - **Download**: weights + tokenizer + config + `ort-wasm-simd-threaded.wasm` (14.2 MB),
     raw and gzip.
   - **Load**: read + session create, cold (first in the process) / warm (second), from local
     disk. The network is not in it.
   - **Latency**: p50 and p95 per note by size, after one warm-up. Long notes are chunked on
     sentence boundaries to 384 words and the spans shifted back.
   - **Memory**: the Node process's peak RSS (not the browser's).
   - **Quality** (`score.ts`, unit-tested): span precision, recall and F1 (exact offsets, and
     ±1-word overlap), kind accuracy on matched spans, and top-3 precision per note, at 0.5 and
     at the best threshold of a 0.30–0.95 sweep.
   - **Not measured: a real browser, WebGPU and the phone.** The user runs the harness
     (`pnpm install --ignore-workspace && node fetch-models.mjs && node serve.mjs` in
     `scripts/extraction-spike/`, then http://localhost:3190 on the desktop, or the Mac's LAN
     address from the phone) and adds those columns.
4. **The gates.** A candidate is eligible only if, on the desktop: download ≤ 250 MB
   uncompressed, warm load ≤ 5 s, typical-note p50 ≤ 1.5 s; on the phone: warm load ≤ 10 s,
   typical-note p50 ≤ 4 s, no tab reload; and top-3 precision ≥ 0.6. **Pick** the eligible
   candidate with the highest span F1 **on WASM** (what the browser runs), and the smaller
   download on a tie within 0.03. If none is eligible on the phone, set the phone default to
   "Off" (23b).
5. **Recorded** in Notes, "Decision, 23a".
6. **Tests.** `extractionScore.test.ts`: exact and overlap matching, kind accuracy, top-3,
   empty gold sets. The runner and harness are not tested.

Results (2026-09-28, Node 24.15 on the user's Mac; WASM = `onnxruntime-web` 1.30.0, one
thread). F1 and top-3 are on WASM at the best threshold, native F1 in brackets.

| Candidate | Download raw / gzip | Load cold / warm (WASM, Node) | Typical p50 / p95 (WASM, Node) | Long p50 | Peak RSS | Span F1 exact (native) | Top-3 precision | Kind acc. | Eligible (desktop, Node) |
|---|---|---|---|---|---|---|---|---|---|
| **GLiNER small v2.5**, `model_quantized` | 219 / 144 MB | 0.7 / 0.5 s | 479 / 533 ms | 1.95 s | 1.25 GB | **0.896** @0.35 (0.902); 0.873 @0.5 | 0.881 | 0.88 | **yes** |
| GLiNER small v2.1, `model_int8` | 206 / 127 MB | 0.9 / 0.5 s | 330 / 372 ms | 1.32 s | 1.15 GB | 0.719 @0.3 (0.791); 0.601 @0.5 | 0.817 | 0.93 | yes |
| GLiNER small v2.1, `model_uint8` | 206 / 132 MB | 0.6 / 0.3 s | 314 / 374 ms | 1.28 s | 1.41 GB | 0.707 @0.3 (0.709) | 0.784 | 0.94 | yes |
| GLiNER multi v2.1, `model_int8` | 380 / 257 MB | 1.4 / 1.2 s | 654 / 737 ms | 2.69 s | 1.60 GB | **0.033** @0.3 (0.766) | 0.8 (5 shown) | – | no: download, and broken on WASM |
| GLiNER multi v2.1, `model_uint8` | 380 / 265 MB | 1.5 / 0.7 s | 651 / 720 ms | 2.64 s | 1.75 GB | 0.000 (0.008) | – | – | no |

What the table says:

- **Multi v2.1 is out.** It is 380 MB, over the 250 MB gate, and its int8 weights find almost
  nothing on `onnxruntime-web` WASM (F1 0.03) although the same file scores 0.77 on
  `onnxruntime-node`. Its uint8 export finds nothing on either.
- **Small v2.1 loses quality on WASM**: F1 0.79 on native, 0.72 on WASM with the same file
  (recall drops most). The uint8 export does not help. So v2.1's dynamic quantisation and the
  WASM integer kernels do not agree; v2.5's export does not have the problem (0.90 / 0.90).
- **Small v2.5 is best on quality** (+0.18 F1 over small v2.1 on WASM) for 13 MB more, and
  ~45% slower per note, still well inside the desktop gate.

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
     "suggestion": { "model": "gliner_small-v2.5", "version": "gliner-community/gliner_small-v2.5@<sha>+onnx-int8",
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
   device setting, per campaign the member is in): "gliner_small-v2.5 · 14 mentions in 9
   notes · **Revert**". Revert asks "Unlink the 14 mentions this model suggested in your
   notes? Mentions you typed yourself stay." and then lists `createdEntries` with links
   ("These entries were created from its suggestions and stay: …").
5. **Close the step**: tick this file's PR table and set README's status to `done`.

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

### Why GLiNER

- **The user chose GLiNER** on 2026-09-28: "I'm interested in using GLiNER. It is
  specialised for the task." It is zero-shot NER with labels given at inference and
  character offsets out, which is exactly what suggestions need.
- Laya (Convai's typed-decision model) was considered as a second candidate and dropped by the
  user; it does not find spans on its own.

### Decision, 23a

- **Pick: GLiNER small v2.5**, the int8 ONNX export, at **threshold 0.4** (the WASM sweep's
  best is 0.35 at F1 0.896, 0.4 is within 0.01 of it with better precision; 23b may retune on
  the fixture). It passes every desktop gate measured in Node and has the best F1 by 0.18.
  Model id in provenance: `gliner_small-v2.5`.
- **The user framed the choice as small vs multi v2.1.** Between those two, **small v2.1**
  wins (multi is too big and broken on WASM). v2.5 is here because the plan's step 2 asked for
  a v2.5 row if one existed; if the user prefers the `onnx-community` v2.1 export for its
  provenance, the fallback pins are below and nothing else in 23 changes.
- **Provenance caveat.** The v2.5 ONNX is a community export (`GG-QandV`, published
  2026-09-18, no other users yet) of the Apache-2.0 upstream
  `gliner-community/gliner_small-v2.5@f227d3cd637bd4e6757ae143935316d062393341`, recorded in
  its `NOTICE`. The weights are pinned by revision and sha256 and self-hosted (below), so the
  repo changing cannot change what ships. 23b may instead re-export upstream itself with the
  GLiNER Python package; if so, re-run `run.mjs` and re-pin.
- **Runtime**: `onnxruntime-web` 1.30.0 (WASM, single-threaded; WebGPU untested),
  `@huggingface/tokenizers` 0.2.0, and a hand-written span decoder (the spike's
  `scripts/extraction-spike/lib/gliner.mjs`, which 23b ports to TypeScript). Chunking: 384
  words, on sentence boundaries.
- **Browser and phone numbers are not measured.** The Node WASM numbers are a floor for the
  desktop; the user runs `scripts/extraction-spike/harness.html` (above) on the desktop and on
  the phone, and fills in: warm load, typical p50, peak memory and whether the tab reloads.
  If the phone misses its gates, 23b's phone default is "Off". Node's peak RSS for v2.5 was
  1.25 GB, which is a risk on phones.

**Pinned (chosen): `GG-QandV/gliner_small-v2.5-onnx` @ `a748820c906f7af707a25bb52411b21b999f8de9`**,
licence Apache-2.0 (`LICENSE` and `NOTICE` in the repo, shipped alongside the weights):

| File | Bytes | sha256 |
|---|---|---|
| `model_quantized.onnx` | 196,786,385 | `60f2f4da1ccad2230626ecc00cbbb18474b5415d3a9fddfca8078f52c2ab2930` |
| `tokenizer.json` | 8,332,739 | `08bb5853718f4a829fa9ce773d7984f7f3f6a7073fdc82a07a382675c5061ba6` |
| `tokenizer_config.json` | 531 | `54121ac6feec6b4d5bf85245a54c3c5b1ff05ef2318cf83e45340849b8981566` |
| `gliner_config.json` | 2,274 | `b327b6b5fe3cbefc4583e8cc50ecce3442f5c42855d0f5362dd51d8fa620d84f` |
| `LICENSE` | 11,358 | `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30` |
| `NOTICE` | 1,110 | `c0aa9762a278e5328ff4e24be3a7ef9e003706e8b9a10bbacfbe6e8476f30d2e` |

**Pinned (fallback): `onnx-community/gliner_small-v2.1` @ `8142fb00740ccea973e64b1272949ff48653df5e`**,
upstream `urchade/gliner_small-v2.1`, Apache-2.0 (upstream model card). Threshold 0.3 on WASM.

| File | Bytes | sha256 |
|---|---|---|
| `onnx/model_int8.onnx` | 183,403,734 | `c76c90920547fd937aaf505e7f2de5ec73168bf1c25abbb55a298104cb061400` |
| `tokenizer.json` | 8,657,198 | `677203884d026e721115cf0daccf70ec4239545a13d6619e3e66d7151e0c9ce3` |
| `tokenizer_config.json` | 1,806 | `cef106fb5c03d234f0af80f7577f1fc90b4317f26c26888d625abba11331dc89` |
| `gliner_config.json` | 731 | `8e8b59de124a256a3f3de67879d0da686fe3f73ecc05093506ba525e451b920d` |

All weights live in the git-ignored `.data/models/` (`node fetch-models.mjs` fetches and
checks them); none are committed.

### As built, 23b

- **Weights: our own export of the official upstream, which reproduces the community file
  byte for byte.** `scripts/gliner/export.py` (run with `uv run`, or `pnpm models:export`;
  Python 3.12 with gliner 0.2.28, torch 2.14.0, transformers 5.13.1, onnx 1.23.0, onnxruntime
  1.30.0 pinned inline) downloads `gliner-community/gliner_small-v2.5` (Apache-2.0) at
  `f227d3cd637bd4e6757ae143935316d062393341`, exports it with the GLiNER package's own
  `export_to_onnx` (opset 19) and quantises it with `quantize_dynamic` (QUInt8). On the
  user's Mac it produced `model_quantized.onnx` with sha256 `60f2f4da…2930` (196,786,385
  bytes), `tokenizer.json` `08bb5853…1ba6` and `tokenizer_config.json` `54121ac6…1566`:
  **exactly the bytes of the 23a community export** (`GG-QandV`, made on Linux with torch
  2.13), and two runs gave the same bytes. Only `gliner_config.json` differs (the
  transformers version string), and the runtime does not read it: `maxWidth` 12,
  `maxWords` 768 and `maxTokens` 512 are in the manifest. So 23a's measurements stand for
  the pinned file, and the v2.1 fallback was not needed.
- **The pins** are in `apps/TakeInitiative.Web/suggestion-model.json` (model id
  `gliner_small-v2.5`, provenance version
  `gliner-community/gliner_small-v2.5@f227d3cd…+onnx-int8`, threshold 0.4, the three files
  with bytes and sha256). `nuxt.config.ts` reads it into `runtimeConfig.public.suggestions`.
- **Getting the files.** `pnpm models:fetch` (`scripts/models/fetch-model.mjs`) writes them
  to `apps/TakeInitiative.Web/public/models/gliner_small-v2.5/{revision}/` (git-ignored),
  with `LICENSE` and `NOTICE` from `scripts/models/gliner_small-v2.5/` (committed), and
  refuses any file whose size or sha256 differs. Its source is, in order: `--from <dir|url>`
  or `SUGGESTIONS_MODEL_FROM`; the local export in `.data/models/export/…` if
  `pnpm models:export` was run; else the manifest's `mirror`, the `GG-QandV` repo at its
  pinned revision, because its bytes are proven identical to our export. So the weights
  that ship are the ones our script builds from upstream, whichever host the bytes came
  through. To stop depending on that repo, upload the export's three files somewhere the
  deployer controls (e.g. a GitHub release) and pass it as `SUGGESTIONS_MODEL_FROM` or
  change `mirror`.
- **Dev**: the web `dev` script runs `fetch-model.mjs --if-missing --soft` first, so the first
  `pnpm dev` downloads ~205 MB once, and never fails the dev server (offline it warns).
- **Deploy**: the web `Dockerfile` copies the manifest and `scripts/models/`, fetches into
  `/models` in its own layer (cached until the manifest changes), then copies them into
  `public/models/` before `nuxt build`; `.output/public/models/` is served by Nitro with
  `cache-control: immutable` (route rule `/models/**`). `--build-arg SUGGESTIONS_MODEL=skip`
  leaves them out. A root `.dockerignore` keeps a local `public/models/` and `.data/` out of
  the build context. CI never fetches: `testWeb.yml` runs typecheck and vitest only.
- **Hugging Face directly**: `NUXT_PUBLIC_SUGGESTIONS_BASE_URL=https://huggingface.co/GG-QandV/gliner_small-v2.5-onnx/resolve/a748820c906f7af707a25bb52411b21b999f8de9`
  (the file names match). The browser still checks every sha256.
- **The runtime WASM** is imported by URL in the worker
  (`onnxruntime-web/ort-wasm-simd-threaded.wasm?url`), so Vite emits it as a hashed build
  asset (14.2 MB) served by the app; there is no `runtimeWasmBaseUrl` setting. The worker
  imports `onnxruntime-web/wasm` (the WASM-only bundle), single-threaded, `executionProviders:
  ["wasm"]` (WebGPU not tried).
- **Worker and composable.** `useExtractor()` holds tab-wide state (`off | idle | needsConsent |
  downloading | loading | ready | error`), `ensure({ consent })`, `extract(noteId, text)`,
  `cancel()`, `remove()`, `setSetting()`. The queue is in the composable (one note in the
  worker at a time; a newer request for a queued note replaces its text). Cancel terminates
  the worker, which drops the download; files are cached only after the sha256 check, so a
  partial file is never stored. After a load, other revisions' cache keys are deleted and
  `navigator.storage.persist()` is asked for. Messages are in `utils/extraction/messages.ts`.
- **Spans.** `utils/extraction/spans.ts` (mask, tidy, distinct, the stop list) and
  `extractor.ts` (the span decoder) are the spike's `lib/text.mjs` and `lib/gliner.mjs` in
  TypeScript. `finalSpans` keeps the longest of overlapping spans, one per folded text, the
  ten most confident. A check run on the fixture with the real weights (not committed; it
  needs the model) gave top-3 precision 0.881, the spike's number, and span F1 0.75 exact
  after `distinct` and the cap (the spike's 0.87–0.90 counts every occurrence).
- **Bundle.** `pnpm build` before (23a) and after: the entry chunk grew by 0.25 KB; the Me
  page gained one 12 KB chunk (the setting, the composable and the pure helpers); the
  runtime and tokenizer are only in `extractor.worker-*.js` (115 KB) with the 14.2 MB WASM.
- **The Me page** has "This device" with "✨ Suggestions on this device": Off / Ask /
  Automatic, the model, its licence and attribution, where the files come from, the space
  used, Download (with progress and Cancel; Retry on an error), Remove from this device,
  and "Try it on a sample sentence" (an invented line) once the model is ready, so the
  runtime can be checked in a browser before 23d.
- **Not done**: no phone or real-browser numbers yet (23a's harness is still the user's).
  The phone default stays "Ask" until they exist.

### As built, 23c

- **Endpoints**: `POST suggestions/match`, `GET suggestions/models`, `POST suggestions/revert`
  (`src/Features/Suggestions/Api/`), and `PUT notes/{id}`'s optional `suggestion`
  (`{ model, version, confidence, start, length, entryId }`). Shapes as in 23c above.
- **Matching** is 19b's: one `EntryMatcher.MatchAsync` call with `LooseEnds.MatchOptions`
  (`Take = 2`, `MinSimilarity = 0.6`), then `LinkSpans.Accepts`. The matcher folds case and
  accents (`lower(unaccent(…))`), so "rellan" matches and the answer carries the entry's own
  name ("Rellan Ashvale"). 23b's "Try it" printed "rellan" because the invented sample sentence
  itself says "met rellan": the web slices spans from the note's text and loses no case.
  The model's `kind` is accepted but not used.
- **The event**: `Actor(MemberId, ModelSuggestion? Model)` as planned (`Actor.Suggested(…)`).
  **Deviation:** `SessionNoteEdited` gained one optional field, `Suggestion`
  (`SuggestedSpan(Start, Length, EntryId)`), rather than a doc comment only, so the projection
  knows which mention the edit added without guessing from a diff. Old events read it as null.
- **The one-span rule** (`SuggestionEdit.Check`): the new text must equal the old with exactly
  `@[span](entry:id)` at `start`, and `MentionParser` must see the old mentions plus that one
  (so a span inside a mention, a link or code is a 400). A span with `[`, `]`, `\`, a backtick
  or a newline, with outer spaces, or splitting a surrogate pair is refused. The recap flag and
  images must not change. The entry must be one the author can see (unknown, hidden and merged
  look the same) or the single `newEntries` entry; a created entry's `EntryCreated` carries the
  same Actor. All of it is 400 with `errors.suggestion`.
- **`SessionNote.SuggestedMentions`** rows are `{ entryId, start, text, model, version,
  confidence }` (`text` added so revert can check the literal `@[text](entry:id)` is still
  there). They are carried through each edit by its common prefix and suffix: rows outside the
  changed stretch stay or shift; rows inside it follow their literal in order only when the
  stretch has as many of that literal before and after, else they are dropped (the mention then
  counts as hand-typed). Not `MentionParser` offsets: Markdig's parse was not needed for this.
- **History**: each version has `model` (`{ name, version, confidence }` or null), flat on
  the version rather than under `actor`. The entry history does not show the model (not asked
  for; `EntryCreated` has it).
- **Revert** appends one plain `SessionNoteEdited` per note it changes and pushes
  `sessionNoteUpserted`. `createdEntries` are read from the caller's listed entries created
  from a note whose `EntryCreated` carries that model and version.
- **Caveats**: `models` and `revert` load the caller's notes in the campaign and filter in
  memory (one member's notes; no index on the new field). A revert that also touches another
  version's identical mention in the same changed stretch drops that row too (it would take
  two mentions with the same entry and text in one note from two versions).
- **Tests**: `SuggestionEditTests` (unit, 10), `SuggestionMatchTests`, `SuggestionAcceptTests`,
  `SuggestionRevertTests`, `SuggestionLeakTests`; 939 API tests pass.

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

1. **GLiNER small v2.5 (community ONNX export) or small v2.1 (`onnx-community`)?** *Default:
   v2.5, on quality; v2.1 is pinned as the fallback (Notes, "Decision, 23a"). GLiNER itself is
   the user's decision (2026-09-28).*
2. **Where the weights are served from.** *Default: self-hosted from the web app, fetched and
   checksum-checked at build time; Hugging Face only when an environment variable says so.*
3. **The default device setting.** *Default: "Ask" everywhere, and never a first download on a
   metered connection (every phone without the Network Information API) without a tap.*
4. **Revert scope.** *Default: each member reverts their own notes only. The alternative is a
   DM revert across the campaign, which breaks invariant 4.*
5. **The quality bar.** *Default: 23a's gates (top-3 precision ≥ 0.6, the latency and size
   limits). If nothing passes on phones, phones default to Off.*
