# 19 — Connections + loose ends

## Goal

The wiki learns what it already knows. Two entries are **connected** when a session
note or an article block mentions both, or when both fought in one combat. Every entry
page gets a **Connections** panel (`@Tharden (3) · @Phandalin (4)`), and tapping one
opens its **evidence**: the snippets, blocks and combats where the two co-occur, cut
from only what the viewer can see (design §6). A force-directed **graph** page inside
the Wiki draws the same connections, filtered by kind, at depth 1 or 2 around an
entry, and clicking an edge opens its evidence. **Loose ends** (§5) are counted and
listed: session notes and image notes with no mention, entries of kind `Other`, and
mentioned entries with an empty article. The Wiki shows "Loose ends (n)" at the top,
each session divider shows its own count, and each loose end can be resolved where it
is listed. An unlinked note offers "this says Gundren, link it?" from the entry
matcher. Nothing about either is stored (invariant 7, §5): both are derived per viewer
on every read, so they clear themselves and cannot leak.

"Running" at the end: a DM and a player, on a phone and on a desktop, each see a
different, correct Connections panel and graph for the same campaign. The player
links an untagged note from its loose end with one tap, and the counts on the divider
and in the Wiki drop live. This closes the MVP (README's MVP line).

The step ships as five PRs stacked with `gh stack` on top of this file's docs PR,
which sits on 18f (#238). Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 19a | `v2/19a-connections-api` | Connections, evidence and the graph (API) | 18's app unchanged in the browser. The API answers an entry's connections, a pair's evidence and a campaign graph, per viewer, and the leak tests pass | [x] |
| 19b | `v2/19b-loose-ends-api` | Loose ends and link suggestions (API) | The same in the browser. The API lists loose ends per viewer with suggested links for unlinked notes, and counts them for the Wiki and per session | [x] |
| 19c | `v2/19c-connections-panel` | The Connections panel and evidence sheet | An entry page shows CONNECTIONS, grouped with "Seen at", and a tap opens the evidence. It refreshes live | [x] |
| 19d | `v2/19d-graph-page` | The graph page | `/wiki/graph` draws the force graph, with kind chips, depth 1–2, pan and pinch zoom, and edges that open the evidence | [x] |
| 19e | `v2/19e-loose-ends-ui` | Loose ends in the Wiki and on sessions | "Loose ends (n)" in the Wiki and ⌘K, 🧵 counts on dividers, and resolving in place. The step's Verify passes | [ ] |

Typed relations, a stored edge table and model suggestions (step 23) are not in 19
(Notes, "Not in 19").

## Depends on

**15**: the mention index (`MentionIndex`: `NotesMentioning`, `BlocksMentioning`,
`CountsFor`, `MentioningAny`, `MergeTargets`), the entry read rule (`EntryVisibility`,
`ArticleView.VisibleBlocks`), edit access (`EntryPermissions`) and the entry page.
**18**: `Combat.EntryIds` and `CombatView.VisibleCombatants` for "Fought together", and
`CombatCard` for drawing a combat as evidence. **16** for
`SessionNote.UntaggedImageNote`, and **17** for `EntryMatcher` and the ⌘K action
registry. These parts of [12-v2-design-session.md](12-v2-design-session.md) are binding:

- the glossary (§1), including the nouns this PR adds;
- the loose-ends count on the session divider (§3 sketch);
- the entry page's CONNECTIONS line and `[graph ↗]` (§4), and the wiki home's
  "Loose ends (n)" (§4);
- loose ends (§5) and connections (§6), all of both;
- `MentionIndex` driving connections and loose ends (§9);
- the invariants (§10, especially 5, 6, 7 and 11).

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web), except where they start at the repo root.

What exists today: nothing draws connections or counts loose ends. The seams are
`MentionIndex.BlocksMentioning` (15e, used only by the timeline), `Combat.EntryIds`
with its GIN index (18a), `SessionNote.UntaggedImageNote` (16b, used only by a test),
`EntryMatcher.MatchAsync` (17a, called with one span by ⌘K's Entries section), the
16c "⚠ Tag what's in this?" hint on `Session/SessionNoteCard.vue`, and comments naming
step 19 in `wiki/[entryId].vue` and `SessionNote.cs`.

**This PR (docs)**
- `docs/roadmap/19-connections.md`: this file
- `docs/roadmap/12-v2-design-session.md` §1: the new nouns (step 0 below)
- `docs/roadmap/README.md`: link step 19, `in progress`
- `docs/roadmap/HANDOVER.md`: "Next work" points here

**19a**
- API add `src/Features/Connections/{ConnectionIndex,ConnectionPairs,EvidenceSnippet}.cs`
- API add `src/Features/Connections/Api/{GetEntryConnections,GetConnectionEvidence,GetConnectionGraph}/*.cs`
- API modify `src/Features/Entries/Mentions/MentionIndex.cs` (a `NoteMentions` read shared with connections, doc comments), `GlobalUsings.cs`
- Tests add `Scopes/Unit/ConnectionPairsTests.cs`, `Scopes/Unit/EvidenceSnippetTests.cs`
- Tests add `Scopes/Integration/Features/Connections/{ConnectionTests,EvidenceTests,GraphTests,ConnectionLeakTests}.cs`
- Web `utils/api/schema.d.ts`: regenerated

**19b**
- API add `src/Features/LooseEnds/{LooseEnds,LooseEndKind,LinkSpans}.cs`
- API add `src/Features/LooseEnds/Api/{GetLooseEnds,GetLooseEndCounts}/*.cs`
- API modify `src/Features/Sessions/Models/SessionNote.cs` (`Unlinked`, next to `UntaggedImageNote`), `src/Features/Entries/Models/ArticleView.cs` (`IsEmptyFor(entry, viewer)`)
- Tests add `Scopes/Unit/LinkSpansTests.cs`
- Tests add `Scopes/Integration/Features/LooseEnds/{LooseEndTests,LooseEndCountTests,LooseEndLeakTests,LinkSuggestionTests}.cs`
- Web `utils/api/schema.d.ts`: regenerated

**19c**
- Web add `components/Wiki/{EntryConnections,EvidenceSheet,EvidenceRow}.vue`
- Web add `utils/connections.ts` (grouping, labels, "Seen at"), `utils/api/connection/{getEntryConnectionsRequest,getConnectionEvidenceRequest}.ts`, `utils/queries/connections.ts`
- Web modify `pages/app/campaigns/[campaignId]/wiki/[entryId].vue` (the panel between the article and the timeline), `composables/useCampaignHub.ts` (invalidate `["connections", campaignId]`), `utils/api/types.ts`
- Web add `tests/unit/connections.test.ts`

**19d**
- Web add `pages/app/campaigns/[campaignId]/wiki/graph.vue`
- Web add `components/Wiki/{ConnectionGraph,GraphControls}.vue`, `utils/graph.ts` (layout input, edge width, depth filter), `utils/api/connection/getConnectionGraphRequest.ts`
- Web modify `package.json` (`d3-force`, `d3-zoom`, `d3-selection`, and their `@types`), `components/Wiki/EntryConnections.vue` (`[graph ↗]`), `pages/app/campaigns/[campaignId]/wiki/index.vue` (a Graph button), `utils/searchActions.ts` ("Wiki: Graph")
- Web add `tests/unit/graph.test.ts`

**19e**
- Web add `pages/app/campaigns/[campaignId]/wiki/loose-ends.vue`
- Web add `components/LooseEnds/{LooseEndList,LooseEndRow,LinkSuggestions}.vue`, `utils/looseEnds.ts` (`linkSpan`, row labels), `utils/api/looseEnd/{getLooseEndsRequest,getLooseEndCountsRequest}.ts`, `utils/queries/looseEnds.ts`
- Web modify `pages/app/campaigns/[campaignId]/wiki/index.vue` ("Loose ends (n)" at the top), `components/Session/{SessionDivider,SessionStream}.vue` (🧵 n), `components/Session/SessionNoteCard.vue` (the 16c hint links to its loose end), `utils/searchActions.ts` ("Loose ends (n)"), `composables/useCampaignHub.ts` (invalidate `["looseEnds", campaignId]`)
- Web add `tests/unit/looseEnds.test.ts`; modify `tests/unit/searchActions.test.ts`
- `docs/roadmap/19-connections.md`, `README.md`, `HANDOVER.md`: tick and close the step

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/18f-combat-search
gh stack add v2/19-connections-plan
```

Add each sub-step on top with `gh stack add v2/19a-connections-api` and so on.

**Glossary check.** Connection, Loose ends and Entry matcher are in §1 already. This
PR adds the nouns the step puts into code and UI:

- **Evidence**: the notes, article blocks and combats that explain one connection. A
  note or block is shown as a snippet; a combat as its combat card. Not "sources" or
  "proof".
- **Weight**: how many pieces of evidence a connection has, as the viewer sees it. It
  is the `(3)` after a connection and the thickness of a graph edge.
- **Fought together**: evidence that two entries were both visible combatants in one
  started combat.
- **Seen at**: the Connections panel's label for a Character's connections to Places
  (and "Seen here" on a Place, for its Characters). A grouping, not a relation.
- **Graph**: the Wiki's force-directed drawing of connections (`/wiki/graph`). Not
  "map" (that is a picture in a note) or "network".
- **Link suggestion**: on an unlinked note's loose end, a span of its text that the
  entry matcher matched to an entry ("this says Gundren, link it?"). Not a model
  **suggestion** (§11a, step 23), and it has no effect until the author accepts it.

### 19a. Connections, evidence and the graph (API)

1. **What counts** (`ConnectionPairs`, pure). The input is a list of **sources**, each
   a set of entry ids plus a reference back to it:
   - a **note** the viewer can see: its `MentionedEntryIds`;
   - a **block** the viewer can see (`ArticleView.VisibleBlocks`) of a listed entry `E`
     the viewer can see: the ids `MentionParser.EntryIds(block.Text)` finds, **plus
     `E` itself**, so "Brother of @Tharden." in Gundren's article connects Gundren and
     Tharden (§6's sketch);
   - a **combat** whose `Status` is not `Draft`: the `EntryId`s of
     `CombatView.VisibleCombatants(combat, viewer)`, so a hidden combatant or a Draft
     never connects anything for a player (invariant 8).

   Each id is resolved through `MentionIndex.MergeTargets` (a merged id means its
   target, 15g), ids that are not a listed entry the viewer can see are dropped, and
   the set is de-duplicated. A source with *n* ≥ 2 ids gives each of its *n(n−1)/2*
   pairs one piece of evidence, so a source counts **once per pair** however many
   times it repeats a mention, and an entry is never connected to itself (a note
   mentioning both a winner and its merged loser gives nothing). A pair is keyed
   `(min, max)`, so connections are undirected.
2. **Reading the sources** (`ConnectionIndex`). All reads are scoped to one campaign
   and one viewer and use the rules the sources already have:
   - notes: `SessionNoteVisibility.VisibleTo(viewer)`, projecting only `Id`,
     `SessionId`, `MentionedEntryIds` and `PostedAt` (the same shape
     `MentionIndex.CountsFor` reads; share its `NoteMentions` record), with
     `cardinality(MentionedEntryIds) >= 2` in SQL where Marten allows, else in memory;
   - entries: `.Listed(campaignId, viewer)` once, reused for blocks, the drop filter
     and the response;
   - combats: `CampaignId` and `Status != Draft`, with `EntryIds` non-empty.

   Two entry points:
   - `ForEntry(session, campaignId, entry, viewer, ct)`: only the sources that mention
     one of `entry.MentionIds()`, found with `MentionIndex.MentioningAny` on all three
     GIN indexes (`MentionedEntryIds`, `ArticleMentionIds`, `EntryIds`), plus the
     entry's own article blocks. This is the panel's and the evidence's read.
   - `ForCampaign(session, campaignId, viewer, ct)`: every source. This is the graph's
     read. It is O(notes) in memory, the way `CountsFor` is, which is fine at a
     campaign's scale (Notes, "No stored edges").
3. **`GET /api/campaigns/{CampaignId}/entries/{EntryId}/connections`**. The entry is
   `EntryAccess.RequireVisibleEntry` (404 otherwise; a merged id answers for its
   target, like `GET entry`).

   ```jsonc
   { "connections": [ {
       "entry": EntrySummaryResponse,     // the other end
       "weight": 4,                       // notes + blocks + combats
       "notes": 2, "blocks": 1, "combats": 1,
       "lastAt": "…"                      // the newest note's PostedAt or combat's StartedAt; null for blocks only
   } ] }
   ```

   Sorted by `weight` descending, then `lastAt` descending, then name. No cap: an
   entry has tens of connections, not thousands.
4. **`GET /api/campaigns/{CampaignId}/entries/{EntryId}/connections/{OtherEntryId}`**:
   the evidence. Both entries must be visible (404 otherwise). An unconnected visible
   pair is a 200 with no evidence, so the answer never says why.

   ```jsonc
   { "from": EntrySummaryResponse, "to": EntrySummaryResponse,
     "evidence": [
       { "kind": "Block",  "block":  { "entryId", "entryName", "blockId", "visibility", "snippet" } },
       { "kind": "Note",   "note":   { "noteId", "sessionId", "sessionNumber", "authorMemberId", "visibility", "postedAt", "hasImages", "snippet" } },
       { "kind": "Combat", "combat": { "card": CombatCard, "sessionNumber": 12 } }
     ] }
   ```

   Blocks first (curated), then notes newest first, then combats newest first. The
   snippet (`EvidenceSnippet`, pure) is the source's markdown with its mentions left
   as `@[text](entry:id)`, so the web draws chips (and plain text for an entry the
   viewer cannot see, 15c's rule). It is cut to a window of about 280 characters around
   the first place both entries' mentions fall, on word boundaries, with `…` at a cut.
   A block's `visibility` is the secret block's own, and a note's is the note's, so the
   web can show 🔒. `CombatCard.For` draws the combat with the viewer's redaction.
5. **`GET /api/campaigns/{CampaignId}/connections/graph?focus={entryId}&depth=1|2&kinds=Character,Place`**.

   ```jsonc
   { "nodes": [ { "entry": EntrySummaryResponse, "mentionCount": 7, "depth": 0 } ],
     "edges": [ { "a": "…", "b": "…", "weight": 3 } ],
     "truncated": false }
   ```

   - With `focus`: that entry (depth 0), its neighbours (1) and, at depth 2, theirs.
     Edges are every pair among the returned nodes, not just the tree edges, so a
     triangle is drawn as one.
   - Without `focus`: every entry with at least one connection.
   - `kinds` filters nodes (and so their edges) and never removes the focus. A depth-2
     node is reached only through a node that passes the filter.
   - The node cap is 300, keeping the heaviest by total weight, with
     `truncated: true`. `mentionCount` is from `MentionIndex.CountsFor` over the
     entries already loaded, and sizes the node.
   - `depth` must be 1 or 2 (400 otherwise); the default is 1 with a focus.
6. **Tests.**
   - `ConnectionPairsTests` (unit): pair counting, once per source, merge resolution,
     self-pairs, the article's own entry joining its blocks, dropping unknown ids.
   - `EvidenceSnippetTests` (unit): the window, word boundaries, both mentions inside,
     mentions far apart (the first of each is kept, with a `…` between).
   - `ConnectionTests`, `EvidenceTests`, `GraphTests` (integration): the endpoints'
     shapes, orders, depth, kinds, the cap and a merged focus.
   - `ConnectionLeakTests`, the ones that matter. A player gets no connection, weight,
     evidence row or graph node or edge from:
     - a `DM` or hidden note that mentions two `Everyone` entries;
     - a 🔒 DM secret block in an `Everyone` article;
     - a `DM` entry (no node, no connection to it, and its id absent from the JSON);
     - a hidden combatant, a Draft combat, or a combatant whose entry is `DM`;
     - another member's `Me` note or entry.

     And the weight the player sees equals the number of evidence rows they get, for
     every pair (a count over rows they cannot see would reveal them, 15's rule).

### 19b. Loose ends and link suggestions (API)

1. **What a loose end is** (`LooseEnds`, §5), per viewer. Each kind is listed only to
   members who can resolve it, because a loose end is a to-do (Notes, "Who sees a
   loose end"):

   | Kind | Rule | Listed to | Session |
   |---|---|---|---|
   | `UntaggedImageNote` | `SessionNote.UntaggedImageNote` | the note's author | the note's |
   | `UnlinkedNote` | no images and `MentionedEntryIds` empty (`SessionNote.Unlinked`) | the note's author | the note's |
   | `OtherKind` | a listed entry with `Kind == Other` | members who can edit it (`EntryPermissions.CanEdit`) | its `CreatedFromNoteId`'s session, if that note still exists |
   | `EmptyArticle` | a listed entry with a mention count ≥ 1 for the viewer and no visible block with non-blank text (`ArticleView.IsEmptyFor`) | members who can edit it | as above |

   - Hidden notes are still their author's loose ends. A deleted note is none.
   - An entry that is both `Other` and empty is two loose ends: resolving one leaves
     the other.
   - Merged entries are not listed (`Listed` drops them).
2. **Link suggestions** (`LinkSpans`, pure, then `EntryMatcher`). For each
   `UnlinkedNote` (text only; an image with a caption is `UnlinkedNote`'s image twin
   and gets the same):
   - `LinkSpans.From(text)` proposes spans: runs of one to three words that start with
     a capital letter or match a word of at least four letters, skipping markdown
     syntax, existing mentions and the first word of a sentence when it is a common
     word ("The", "We", "Then": a short stop list). At most 40 spans a note, with
     their character offsets.
   - All spans of all listed notes go to **one** `EntryMatcher.MatchAsync` call
     (`Take = 1`, `MinSimilarity = 0.6`), capped at 500 spans a request. The matcher
     already applies the viewer's visibility and drops merged entries.
   - Overlapping matches keep the longest span, then the higher similarity. A note
     gets at most three suggestions, and one entry once per note.

   ```jsonc
   "suggestions": [ { "start": 12, "length": 7, "text": "Gundren", "entry": EntrySummaryResponse, "similarity": 0.92 } ]
   ```
3. **`GET /api/campaigns/{CampaignId}/loose-ends?sessionId={id}`**:

   ```jsonc
   { "items": [
       { "kind": "UnlinkedNote", "sessionId": "…", "sessionNumber": 12,
         "note": SessionNoteResponse, "suggestions": [ … ] },
       { "kind": "OtherKind", "sessionId": null, "sessionNumber": null,
         "entry": EntrySummaryResponse, "mentionCount": 3 }
     ] }
   ```

   Notes newest first, then entries by mention count descending. No paging: the list
   is one member's to-dos. `sessionId` narrows it to one divider's loose ends.
4. **`GET /api/campaigns/{CampaignId}/loose-ends/counts`**:
   `{ "total": 7, "bySession": { "<sessionId>": 3 } }`, with the same rules and no
   matcher call, so it is cheap enough to refetch on every push. The Wiki's badge,
   the dividers and the ⌘K action read it. Entries with no session count in `total`
   only.
5. **Resolving needs no new write endpoint.** Linking is the author's
   `PUT notes/{id}` with the span rewritten to `@[text](entry:id)` (19e does it on the
   web, keeping the text the author wrote, invariant 6). Setting the kind is
   `PUT entries/{id}/kind`. Writing the article is the article editor or promote. The
   loose end disappears because its rule stops matching.
6. **Tests.**
   - `LinkSpansTests` (unit): capitals, multi-word names, offsets with mentions and
     markdown around them, the stop list, the cap.
   - `LooseEndTests`: each kind appears and clears when resolved; hidden notes;
     deleted notes; the `CreatedFromNoteId` session; an entry both `Other` and empty.
   - `LooseEndCountTests`: `counts` equals the list grouped, for three viewers.
   - `LooseEndLeakTests`: a player never gets another member's note, a `DM` entry, an
     entry they cannot edit (`Only me`), or a suggestion of a `DM` entry; a player's
     `EmptyArticle` is per viewer (an entry whose only block is a 🔒 DM secret is empty
     to the player and not to the DM); counts never include what the list omits.
   - `LinkSuggestionTests`: "we met gundren on the road" suggests Gundren
     Rockseeker by its alias; one matcher round trip for many notes.

### 19c. The Connections panel and evidence sheet

1. **Queries** (`utils/queries/connections.ts`): `getEntryConnectionsQuery(campaignId,
   entryId)` and `getConnectionEvidenceQuery(campaignId, entryId, otherId)`, keyed under
   `["connections", campaignId, …]`. `useCampaignHub.ts` invalidates that prefix on
   `sessionNoteUpserted`, `sessionNoteRemoved`, `sessionNoteHidden`, `entryUpserted`,
   `entryRemoved`, `entryArticleChanged`, `entryMerged` and `combatChanged`: nothing new
   is pushed, because connections are derived (Notes, "No pushes of their own").
2. **`Wiki/EntryConnections.vue`**, between the article and the timeline on
   `wiki/[entryId].vue`, headed CONNECTIONS with `[graph ↗]` on the right (a link to
   `/wiki/graph?focus={id}`, live in 19d; hidden until then).
   - One line of chips, `@Tharden (3)`, heaviest first, wrapping; the first 12, then
     "+ n more" expands.
   - `utils/connections.ts` groups them. On a Character, its Places come first under
     **Seen at**; on a Place, its Characters under **Seen here**. A connection with any
     combat evidence carries ⚔ on its chip ("Fought together"). Everything else is one
     plain group.
   - Empty: "No connections yet. Mention another entry in a note about {name}." with
     the existing "Add a note about…" link.
   - Chips are 44px tall on a phone (invariant 11).
3. **`Wiki/EvidenceSheet.vue`**: the shadcn `Sheet`, from the bottom on a phone and the
   right on desktop, like `Image/SessionGallerySheet.vue`. Title "Gundren ↔ Tharden
   (3)". `Wiki/EvidenceRow.vue` draws:
   - a block: the snippet with `Session/NoteMarkdown.vue`, 🔒 for a secret block, and
     "Gundren article" linking to `wiki/{entryId}?block={blockId}` (`BLOCK_LINK_PARAM`);
   - a note: the snippet, 🔒 for a non-`Everyone` note, 🖼 for an image note, and
     "S14 · Sam" linking to the note in the stream (`?note=`, `NOTE_LINK_PARAM`);
   - a combat: `Combat/CombatCard.vue` with "⚔ Fought together".

   Each row's link closes the sheet. The other entry's name in the title links to its
   page.
4. **Tests** (`connections.test.ts`): grouping and "Seen at" both ways, the ⚔ marker,
   ordering, the "+ n more" split.

### 19d. The graph page

1. **Route.** `pages/app/campaigns/[campaignId]/wiki/graph.vue`. A static segment
   beats `[entryId]`, and entry ids are GUIDs, so nothing clashes. The layout's Wiki
   tab already matches by prefix (15c). URL state: `?focus=`, `?depth=1|2`,
   `?kinds=character,place` (the `KIND_PARAM` spelling), so a reload or a shared link
   keeps the view. The page has one element root (HANDOVER, "Pages and layouts").
2. **Layout: `d3-force` in the browser, drawn as SVG by Vue** (Notes, "Why d3-force").
   `Wiki/ConnectionGraph.vue` runs `forceSimulation` with `forceLink` (distance
   shrinking with weight), `forceManyBody`, `forceCollide` and `forceCenter`, and
   renders `<line>`s and `<g>` nodes from reactive positions (throttled to one update
   per animation frame). The simulation stops when alpha drops below 0.01. With
   `prefers-reduced-motion`, it runs 300 ticks up front and draws once.
   - Node radius grows with `mentionCount` (square root, clamped). A node carries its
     kind's icon (`ENTRY_KIND_ICONS` in `utils/entries.ts`) and one colour per kind,
     added to `utils/graph.ts` as theme tokens that work in dark mode. The focus is
     outlined in gold.
   - Edge width is `1 + log2(weight)` (`utils/graph.ts`), and an edge with combat
     evidence is dashed.
   - Labels show for the focus, its neighbours, and every node once zoomed in past
     1.5×.
3. **Interaction.** `d3-zoom` on the SVG gives pan, wheel zoom and pinch zoom.
   - Tapping a node selects it: a bar shows its name, kind and weight, with **Open**
     (its entry page) and **Centre here** (sets `focus`).
   - Tapping an edge opens 19c's `EvidenceSheet` for that pair. Edges get an invisible
     12px-wide hit line so a finger can land on them.
   - Nodes can be dragged (the simulation reheats).
   - The keyboard: Tab moves between nodes in weight order, Enter opens the bar.
4. **Controls** (`Wiki/GraphControls.vue`): kind chips (multi-select, all on by
   default; `ChoiceChips.vue` styling), a Depth 1 · 2 toggle shown only with a focus,
   and "Clear focus". With `truncated`, a line says "Showing the 300 most connected
   entries."
5. **Ways in.** `[graph ↗]` on the Connections panel (focus = that entry), a Graph
   button on the Wiki home next to the sort, and ⌘K "Wiki: Graph" in
   `searchActions.ts`.
6. **Tests** (`graph.test.ts`): edge width, the label rule, URL state round-trip,
   and turning the API response into simulation input (ids, not objects, so Vue
   reactivity and d3 do not fight over the same objects).

### 19e. Loose ends in the Wiki and on sessions

1. **Queries** (`utils/queries/looseEnds.ts`): the counts and the list, keyed under
   `["looseEnds", campaignId, …]`, invalidated by `useCampaignHub.ts` on the same
   pushes as 19c.
2. **Wiki home.** A "🧵 Loose ends (n)" row at the top of `wiki/index.vue`, above the
   kind filter, shown when `n > 0`, linking to `wiki/loose-ends`.
3. **The page** `wiki/loose-ends.vue`, with `?session={number}` to narrow it to one
   session (its heading says "Session 12 · 3 loose ends" and offers "All").
   `LooseEnds/LooseEndRow.vue` per kind:
   - **Unlinked note** or **untagged image**: the note (thumbnails for an image note),
     "S12 · 7:42pm", then `LinkSuggestions.vue`: "Link **Gundren** → @Gundren
     Rockseeker?" chips. One tap rewrites the span with `linkSpan` (`utils/looseEnds.ts`:
     `text.slice(0, start) + "@[" + span + "](entry:" + id + ")" + rest`, after
     checking the span still sits at `start`) and sends `PUT notes/{id}` with the
     note's other fields unchanged. **Edit** opens the note in the composer's edit
     mode (`utils/noteEdit.ts`) on the Campaign tab, for linking by hand or adding
     a caption.
   - **Kind is Other**: the entry's name and mention count, with the six kind chips
     inline (`PUT kind`); picking one resolves it.
   - **Empty article**: "Gundren has 7 mentions and no article", with **Write**
     (`wiki/{id}?edit=` opens the article editor, `EDIT_BLOCK_PARAM`) and **Promote
     from timeline** (the entry page scrolled to the timeline).
   - A resolved row stays for a moment with a ✓, then leaves when the list refetches.
   - Empty: "Nothing loose. Everything is linked."
4. **Session dividers.** `SessionDivider.vue` gets `looseEndCount` and shows
   `🧵 3` next to `🖼`, linking to `wiki/loose-ends?session=12`, with an aria-label
   "3 loose ends in Session 12". `SessionStream.vue` passes it from the counts query,
   under every filter (unlike the image count, it does not come from the loaded
   notes).
5. **The 16c hint.** "⚠ Tag what's in this?" on an untagged image note now links to
   its loose end on the page (which has the suggestions) instead of opening the
   editor directly.
6. **⌘K.** A "🧵 Loose ends (n)" action in `searchActions.ts`, shown when `n > 0`,
   keywords `loose`, `untagged`, `unlinked`, `todo`. This is 17's seam.
7. **Tests** (`looseEnds.test.ts`): `linkSpan` (offsets, a span that moved, a span
   inside an existing mention, unicode), row labels; `searchActions.test.ts`: the
   action's visibility and label.
8. **Close the step**: tick this file's PR table, set README's status to `done`, and
   update HANDOVER.

## Verify

1. `dotnet test` and `pnpm build` pass, `vitest` and `npx nuxi typecheck` pass,
   `schema.d.ts` is fresh, and CI is green on every PR in the stack.
2. `pnpm dev` on an existing dev database starts cleanly. No schema change is
   expected in 19; if Marten reports one, it is a mistake.
3. Two browser profiles, A (the owner, DM) at 1280 × 800 and B (a Player) at 390 ×
   844. The wiki has Gundren Rockseeker (alias "Gundren"), Tharden, Phandalin (Place),
   Klarg (`DM`), and "Glasstaff" of kind `Other`. B has claimed Brynn.
   1. A posts "@Gundren and @Tharden found the mine near @Phandalin." (Everyone), and
      a 🔒 DM note "@Tharden was killed by @Klarg". A writes "Brother of @Tharden." in
      Gundren's article.
   2. On Gundren's page A sees `@Tharden (2) · @Phandalin (1)`; B sees the same, live.
      On Tharden's page A sees `@Klarg (1)`; B does not, and B's
      `GET …/connections` has no `klarg`.
   3. A taps `@Tharden (2)`: the sheet shows the article block first, then the note
      with "S1 · A". Its links open the article block and the note in the stream.
   4. Brynn's page (a Character) shows Phandalin under **Seen at** once B posts
      "@Brynn rode into @Phandalin." A finished combat with Brynn and Goblin 1 shows
      ⚔ on Brynn's Goblin chip, and the combat card in its evidence.
   5. A opens `[graph ↗]` from Gundren: depth 1 shows Gundren, Tharden and Phandalin;
      depth 2 adds Klarg for A, never for B. Turning off Place drops Phandalin. Tapping
      the Gundren–Tharden edge opens the evidence. On B's phone, pan and pinch work and
      every node and edge can be tapped.
   6. B posts an image with no caption and a note "we met gundren on the road". The
      Session 1 divider shows 🧵 2 for B and no count for A (the setup's entries were
      made in the Wiki, so they count in the Wiki total only). B's loose-ends page
      lists the two notes and the mentioned entries with empty articles that B can
      edit (Tharden, Phandalin). A's lists Glasstaff (`Other`) and the empty articles,
      Klarg included, and neither of B's notes.
   7. On B's loose-ends page, the note offers "Link gundren → Gundren Rockseeker?". One
      tap links it: the note shows a chip in the stream, the divider drops to 🧵 1,
      and the Wiki count drops, live, without a reload.
   8. A sets Glasstaff's kind to Character from the loose-ends page; the row leaves.
   9. ⌘K `>loose` shows "Loose ends (n)" for both, with their own counts.
   10. On B's phone, the evidence sheet and the loose-ends page are reachable by touch,
       nothing sits under the keyboard or the home indicator.

## Notes / gotchas

- **No stored edges** (invariant 7, and 15's and 17's Notes). Connections and loose
  ends are read from the sources' own id lists with the sources' own visibility rules
  on every request, so a hide, a visibility change, a merge or a new secret block is
  reflected at once, with nothing to keep in step. The graph's campaign-wide read
  loads every visible note's id list, as `CountsFor` already does for the wiki home.
  At thousands of notes that is milliseconds. If it ever is not, a materialised pair
  table can be built from the same events, per audience, without changing the API.
- **No pushes of their own.** Every change that can move a connection or a loose end
  is already pushed (notes, entries, articles, merges, combats) to exactly the members
  allowed to see it. The web invalidates its connection and loose-end queries on
  those, and the refetch applies the viewer's rules. Pushing a connection delta would
  need a per-viewer diff for nothing.
- **Weight equals rows.** The count on a chip and the rows in its sheet come from the
  same `ConnectionPairs` run, and a test holds them equal. That is how a hidden source
  cannot show up as a number (15's rule for mention counts).
- **The article's own entry joins its blocks.** Otherwise "Brother of @Tharden." in
  Gundren's article would connect nothing. It means an article is evidence about its
  own entry and everything it mentions, but not between two entries it mentions in
  different blocks: the unit is the block (§6).
- **Combats count as evidence and add to weight.** Design §6 lists "Fought together"
  as a connection, so a combat is one piece of evidence per pair of visible
  combatants. A fight with 4 goblins linked to one Goblin entry gives Goblin nothing
  with itself. `Draft` combats never count: they are not visible to players, and a
  DM's plan is not a fact.
- **"Seen at" is a grouping, not a query.** §6 names it; the design leaves open
  whether it is a separate list. The default is a heading in the panel for
  Character → Place (and "Seen here" for Place → Character), from the same
  connections. A dedicated query can come with typed relations.
- **Who sees a loose end.** §5 says "anyone can resolve one", but notes are resolved
  "by their author" and entries by whoever may edit them (invariant 4). The default is
  that a loose end is listed only to the members who can resolve it, so the counts
  are a personal to-do list, and a DM's divider count is theirs, not the table's. A DM
  who wants to see a player's unlinked notes reads the stream. Revisit if the table
  wants a shared count.
- **Empty is per viewer.** An article whose only block is a 🔒 DM secret is empty to
  a player: they cannot see it, so to them it is empty, and filling it is useful. The
  DM does not get the loose end. Hidden things stay absent (invariant 5).
- **No dismiss.** Loose ends are derived, never stored as flags (§5), so a note like
  "Pizza's here" stays loose until it gets a mention or is deleted. A stored dismiss
  would be the first flag. Revisit only if the list is noisy in real use.
- **Link suggestions are not model suggestions.** They are the entry matcher's
  trigram hits on capitalised spans, computed on read and never stored. Step 23's
  model suggestions go through the same matcher (§11a) and will add `Actor.Model`
  provenance; accepting a link suggestion here is a plain edit by the author.
- **The session of an entry's loose end** is the session of the note that created it
  (`Entry.CreatedFromNoteId`, 15b). An entry created in the Wiki, or whose note was
  deleted, counts in the Wiki total only.
- **Why d3-force.** The design asks for a force graph that is touch-friendly,
  filterable and clickable on edges. `d3-force` and `d3-zoom` are small, have no
  renderer of their own, and let Vue draw SVG, so nodes and edges are ordinary
  elements with ordinary click and keyboard handlers. Canvas-based graph libraries
  make edge hit-testing, accessibility and dark-mode theming harder, and 300 nodes
  is well within SVG's range. Keep d3's mutable node objects out of Vue's reactivity
  (`markRaw`) and copy positions out once per frame.
- **Graph cap.** 300 nodes keeps the SVG responsive on a phone. A focus with depth 1
  or 2 is always well under that.
- **Seams for later steps.**
  - **Reference (20, 21):** a `Source` on an entry changes nothing here; reference
    items are not entries until added.
  - **Suggestions (23):** "unlinked, 2 suggestions" (§11a) is `LinkSuggestions.vue`
    fed by the model's spans instead of `LinkSpans`. The loose-ends list is where
    they show.
  - **Discord import (24):** imported notes arrive unlinked, so they are loose ends
    with link suggestions from the first read.
- **Not in 19:** typed relations or relation labels; a stored edge table; pushing
  connections; connections to reference items; a campaign-wide gallery; dismissing,
  assigning or bulk-resolving loose ends; model suggestions (23); moving
  `ArticleBlockEditor` onto `<ComposerEditor>` (HANDOVER's follow-ups).
- **As built, 19a.** Where it differs from 19a above:
  - A combat counts once it has started (`StartedAt` set), not merely when it is not a
    `Draft`: a Draft finished without a roll is `Finished` but never happened. For a DM,
    hidden combatants count (they are in `CombatView.VisibleCombatants`); for a player,
    never.
  - `NoteMentions` is now a public record in `MentionIndex.cs` with `Id` and `SessionId`
    and a `Projection` expression. `MentionIndex.CountsFrom(notes, entries, viewer)` is
    `CountsFor`'s rule over rows already read, so the graph reads the notes once and
    sizes its nodes from them.
  - `cardinality(MentionedEntryIds) >= 2` is filtered in memory. `ForEntry` loads the
    listed entries once and picks the articles that mention the entry from them, rather
    than running a second GIN query on `ArticleMentionIds`.
  - `ConnectionIndex.Pair` (pure) builds the sources and runs `ConnectionPairs`, so unit
    tests drive blocks and combats without a database. `ConnectionGraph.Build` (pure)
    shapes the graph and lives in `GetConnectionGraph.cs`. The 300-node cap is tested
    through it with a small cap, not with 301 entries over HTTP.
  - Graph edges also carry `notes`, `blocks` and `combats`, so 19d can dash an edge with
    combat evidence. A node's `depth` is null without a focus. Without a focus, an entry
    is a node when it has a connection to another entry of a wanted kind. Over the cap,
    nodes are kept by focus first, then depth, then total weight to the other kept nodes.
  - `depth` is validated (1 or 2) whether or not there is a focus. `kinds` is any case,
    and a number or an unknown kind is a 400 (keys `depth` and `kinds`).
  - A note's evidence also carries `isHidden`. Blocks come in by entry name, then article
    order.
  - `EvidenceSnippet` finds mention offsets with a regex (the parser has none) and keeps
    each cut out of a mention. Mentions too far apart for one window get half a window
    each, joined by ` … `.
  - `schema.d.ts` was regenerated by running the built DLL with `--export-openapi` from a
    build outside the checkout, so the running API's `bin/` was left alone.
- **As built, 19b.** Where it differs from 19b above:
  - The matcher is asked for the best two per span (`Take = 2`), not one, and
    `LinkSpans.Accepts` keeps a match only if it is exact, fuzzy, or a prefix match on
    whole words of the name ("gundren" for "Gundren Rockseeker", never "gund", never a
    substring like "rock" in "Brockton"). With `Take = 1` a rejected substring hit
    could hide a good fuzzy one.
  - `LinkSpans.From`'s stop list applies to every word, not only a sentence's first, and
    also drops a possessive `'s` ("Halia's map" asks about "Halia"). Words are
    neighbours only when spaces separate them, so punctuation, line breaks and markdown
    break a run. Mentions, link targets, inline code and URLs are never part of a span.
  - Untagged image notes with a caption get suggestions too, from the same call.
  - `SessionNote.MentionsNothing` reads both note kinds in one query;
    `SessionNote.Unlinked` and `UntaggedImageNote` then split them in memory.
  - An entry's session comes from its `CreatedFromNoteId` note only when the viewer can
    still see that note. `EntryMatcher.MatchAsync` is `virtual`, so a test can count
    round trips.
  - Items carry `suggestions: []` on every row and `mentionCount` only on entries.
  - The flake seen in 19a's first run (whole test classes failing on setup) was
    parallel test hosts racing FastEndpoints' process-wide static serializer options.
    Test hosts now start one at a time (`HostStartup`), and the enum converter is set on
    the ASP.NET JSON options in `Program.cs` instead of in `UseFastEndpoints`' config.
- **As built, 19c.** Where it differs from 19c above:
  - `Wiki/EntryConnections.vue` takes the entry's id, name and kind, `nameOf` and the
    page's "Add a note about…" href as props. `[graph ↗]` is not rendered at all yet;
    19d adds it next to the heading.
  - When a "Seen at" / "Seen here" group is shown, the rest are headed "Also
    connected", so they do not read as part of it. With no such group the one group
    has no heading. The 12-chip limit runs across the groups in panel order
    (`limitConnectionGroups`), and `sortConnections` re-sorts what the API sends
    (weight, `lastAt`, name).
  - A chip is the entry's kind icon, its name, `(weight)` and ⚔, with an accessible
    name such as "Tharden, 3 pieces of evidence, fought together".
  - `Wiki/EvidenceSheet.vue` uses reka's dialog parts directly, as
    `Image/SessionGallerySheet.vue` does, with classes for bottom on a phone and a
    right-hand panel from `md`, rather than shadcn's `SheetContent` (whose `side` is
    not responsive). The title's `(n)` is the number of evidence rows once they load,
    and the chip's weight until then. The evidence query runs only while the sheet is
    open.
  - Any route change closes the sheet (row links, mention chips, combat cards, the
    title's link), and focus is not returned to the chip then, so `?block=`'s scroll
    stands. A block row's link reads "{entry}'s article"; 🔒 labels use `secretLabel`;
    a hidden note shows "Hidden".
  - The hub invalidates `["connections", campaignId]` on the eight pushes listed, and
    also on joining, reconnecting and a change to the viewer's own role, like the
    other campaign queries.
- **As built, 19d.** Where it differs from 19d above:
  - The view is `utils/graph.ts`'s `GraphView` (`graphViewFromQuery` / `graphViewToQuery`).
    Defaults are left out of the URL: no `?depth=` without a focus or at 1, no `?kinds=`
    with every kind on. The last kind chip that is on stays on. A new focus (Centre here,
    Clear focus) pushes a history entry; chips and depth replace it.
  - The graph query is `["connections", campaignId, "graph", focus, depth, kinds]`, so the
    hub's existing invalidation refetches it. It keeps the previous graph while a new view
    loads, and the simulation keeps the positions of nodes that stay (a gentle reheat).
  - Labels also show for every node in a graph of 20 or fewer, and, without a focus, for
    the selected node and its neighbours.
  - Node drag is hand-rolled on pointer events (no `d3-drag`); d3-zoom's filter ignores a
    press that starts on a node, unless it is a second finger (pinch). Double-click zoom is
    off, so a double tap on a node does not zoom. Every node has an invisible hit circle at
    least 44px across on screen, whatever the zoom. The edge hit line is 12px on screen
    (`vector-effect: non-scaling-stroke`).
  - The focus is pinned at the centre until dragged. The view fits the graph once it
    settles and again after a focus change, and a Fit button redoes it.
  - Kind colours are HSL values in `KIND_COLOURS` (`utils/graph.ts`), chosen for the app's
    one dark theme; the kind chips carry a dot of the same colour as a legend.
  - An edge's evidence reads from the selected node (or the focus) when it is one end, so
    the sheet's title starts with the entry you were looking at. The edge is turned into
    the sheet's `connection` with `lastAt: null`.
  - An unknown or hidden focus (404) shows "That entry is not in the Wiki, or you cannot
    see it." with "Show the whole Wiki".
  - Also in this PR: `fix(web)` for 19c, where `EvidenceRow.vue` used `<NoteMarkdown>` (it
    is auto-imported as `<SessionNoteMarkdown>`), so evidence snippets did not render.
