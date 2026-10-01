# 27 — Links

## Goal

`Entry.Links` stops being a seam and becomes a feature. An entry can carry **links**, of
two kinds:

- **Knowledge base** — a link to a row in step 26's knowledge base, stored as
  `provider` + `itemId` and resolved at read time, so the row's name, detail, book and URL
  always come from the corpus rather than a copy. "The Eye ↗ Beholder · Monster · CR 13 · MM".
- **External** — any URL the member types, with their own label. "↗ D&D Beyond sheet",
  "↗ the map I drew", "↗ our campaign playlist".

The kinds are one list with one shape, because the point of the abstraction is that a third
kind (another knowledge base, a different site) is a new enum case and nothing else.

Links are **events on the Entry stream** (`EntryLinkAdded`, `EntryLinkRemoved`), so they
carry an `Actor`, they show in the entry's history, and they push over `CampaignHub` to the
members allowed to see them. Who may read them reuses the rule the codebase already has for
`Source`, for the reason that rule exists: a knowledge-base link on an unclaimed Character
**is** a stat-block disclosure, and "Mysterious Stranger ↗ Vampire" must not reach a player.

This step also delivers the half of step 22 that matters: a **D&D Beyond sheet** is an
external link with a validated host, shown under the claim line. Step 22 keeps only its
unofficial fetch, which stays post-MVP and off by default.

"Running" at the end: a DM opens the wiki entry "The Eye", taps **Add link ▸ Knowledge
base**, types "behol", picks Beholder, and the entry shows "↗ Beholder · Monster · CR 13 ·
MM p. 28" which opens 5etools in a new tab. A player looking at the same entry sees no such
line, because the entry is an unclaimed Character and that link would give the monster away.
On the player's own character "Thorin", the player pastes their D&D Beyond URL and every
member sees "↗ D&D Beyond sheet". The entry's history reads "Added a link · Beholder
(5eTools)". After a `--prune --force` ingest that drops Beholder, the DM's link renders
"↗ Beholder — no longer in your knowledge base" and still deletes cleanly.

The step ships as five PRs stacked with `gh stack` on top of 26g. Each PR leaves the app
runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 27a | `v2/27a-links-plan` | This plan | Docs only | [x] |
| 27b | `v2/27b-links-model` | The model, the events, the read rule and the read path (API) | 26's app unchanged in the browser. `Entry` projects links, `GET entry` returns the ones the caller may read with knowledge-base rows resolved, and history knows about them. No way to create one yet except in tests | [x] built |
| 27c | `v2/27c-links-api` | Add and remove (API) | `POST entries/{id}/links` and `DELETE entries/{id}/links/{linkId}` work, validated, authorised and pushed | [x] built |
| 27d | `v2/27d-links-web` | The Links section on the entry page | Add, remove and open links on a phone and a desktop, including the stale state | [ ] |
| 27e | `v2/27e-ddb-sheet` | D&D Beyond sheet as a preset | "↗ D&D Beyond sheet" under the claim line, added through a host-validated field. The step's Verify passes | [ ] |

## Depends on

- **26**: the `knowledge_base_item` table, its `stale` column, and `KnowledgeBaseQueries` —
  27b resolves knowledge-base links through it, and 26c's prune protection queries the link
  data this step finally writes.
- **15**: the Entry stream, its events (`EntryStatsChanged` is the shape to copy),
  `EntryVisibility`, `EditAccess`, `GetEntryHistory` and `EntryChangeType`,
  `EntryResponse.From`, and `CampaignHub`'s entry notifications.
- **20**: `EntrySource` and `EntrySources.CanRead`, which this step reuses rather than
  reinvents. `EntryLink(Url, Label)` as declared at `Entry.cs:151` is replaced.
- **25**: the entry page's mobile layout and its "More about X" / desktop right-panel split
  — the Links section has to find a home in both.

[12-v2-design-session.md](12-v2-design-session.md) §11 designed `Links[]` and the D&D Beyond
link; invariants 4 (edit access), 5 (visibility on every read and push), 8 (hidden combat
data) and 9 (every event carries an Actor) all bind here.

## Who may read a link

**Reuse `EntrySources.CanRead` unchanged, for both kinds.** Its doc comment already states
the exact threat:

> An NPC's source is its stat block, so "Mysterious Stranger ← SRD Vampire" must not reach a
> player (invariant 8).

A knowledge-base link is the same disclosure by a different route, so it gets the same rule.
Applying that one rule to external links too turns out to be right rather than merely
convenient:

| Entry | Who reads its links | Why that is correct |
|---|---|---|
| Claimed Character (a player character) | Everyone who can see the entry | The D&D Beyond sheet case. The player wants their sheet shared |
| Unclaimed Character (an NPC or monster) | The DMs only | "The Eye ↗ Beholder" is a combat secret, and so is "The Eye ↗ dndbeyond.com/monsters/vampire" |
| Place, Faction, Item, Event | Everyone who can see the entry | Nothing is being hidden; the entry's own visibility is the gate |

So there is **no per-link visibility toggle**. One rule, already written, already tested,
already the answer to this exact question for `Source`. A per-link override is in "Decisions
for the user" and the default is no.

**Who may write** follows the entry's `EditAccess`, with DMs always able to — the same rule
as the article (invariant 4) — **plus a claimer clause for a claimed Character**.

That last part is a correction. An earlier draft of this plan said "a claimed Character's
`EditAccess` already allows its claimer". It does not: `EntryPermissions.CanEdit` is
`DM || creator || (EditAccess == Anyone && CanSee)`, with no claimer clause at all. It only
appears to work because `Anyone` is the default, and a DM who tightens edit access on an entry
they created would lock a player out of their own character's sheet link — which is the whole
of 27e.

The fix has a precedent in the codebase rather than being invented: `EntryStats.CanWrite` is
already `Kind == Character && CanSee && (DM || claimer == viewer)`. Links follow the same
shape, so "the player who plays this character may maintain its links" is expressed the way
"the player who plays this character may maintain its stats" already is.

```csharp
public static bool CanWrite(Entry entry, Member viewer)
    => EntryPermissions.CanEdit(entry, viewer)
        || (entry.Kind == EntryKind.Character
            && entry.ClaimedByMemberId is { } claimer
            && claimer == viewer.MemberId
            && EntryVisibility.CanSee(entry, viewer));
```

27b and 27c shipped with `RequireCanEdit` alone, as the plan then said. **27d/27e adds the
claimer clause**, with a test for the case that exposed it: a DM creates an NPC, a player
claims it, the DM restricts edit access, and the player can still add and remove their own
sheet link but still cannot edit the article.

## The model

```csharp
[JsonConverter(typeof(JsonStringEnumConverter<EntryLinkKind>))]
public enum EntryLinkKind
{
    /// <summary>A row in the knowledge base (step 26): Provider and ItemId are set, Url and Label are not.</summary>
    KnowledgeBase,
    /// <summary>A URL the member typed: Url and Label are set, Provider and ItemId are not.</summary>
    External,
}

/// <param name="Id">Generated when the link is added, so DELETE has something to name.</param>
public sealed record EntryLink(
    Guid Id,
    EntryLinkKind Kind,
    string? Provider,
    string? ItemId,
    string? Url,
    string? Label,
    DateTimeOffset AddedAt,
    Guid AddedByMemberId);
```

A knowledge-base link **stores no copy of the row**. Name, detail, book, page, URL, artwork
and `stale` are resolved on read from `knowledge_base_item`. That is the whole reason the
corpus moved into Postgres in step 26: a link is a foreign key, and a re-ingest that
corrects a URL or a page number fixes every link to it at once.

`Entry.Links` gains a GIN index so 26c's prune can ask "does any entry link to this row?"
without a scan.

### Events

```csharp
public sealed record EntryLinkAdded(Actor Actor, EntryLink Link) : IActorEvent;
public sealed record EntryLinkRemoved(Actor Actor, Guid LinkId) : IActorEvent;
```

Two events, not three: **editing a label is a remove and an add**. It keeps the projection
trivial and the history honest — the old label is not silently rewritten — at the cost of
two history rows for a typo fix, which is the right trade at this scale.

`EntryChangeType` gains `LinkAdded` and `LinkRemoved`, and `EntryChange` gains the resolved
link. History is visibility-filtered already, so a link a caller may not read is absent from
their history, not greyed out (invariant 5).

## Validation

- **At most 20 links** per entry. A 21st is a 409.
- **External URL**: `http` or `https` only, at most 2,048 characters, must parse as absolute.
  `javascript:`, `data:`, `file:` and everything else are rejected in the validator, not
  filtered in the view. The web renders every link with `rel="noopener noreferrer"` and
  `target="_blank"` regardless.
- **Label**: required for `External`, at most 80 characters, trimmed, and never rendered as
  markdown. Ignored for `KnowledgeBase`, which has no label of its own.
- **Knowledge base**: `provider` must be a registered `IReferenceProvider.Key` and the row
  must exist at add time (404 `errors.itemId` if not). A row that disappears *later* is the
  stale case, not a validation failure.
- **No duplicates**: the same `(provider, itemId)` twice, or the same normalised URL twice,
  is a 409 rather than a silent no-op, because the user's intent was to add something.

## Layouts

### Entry page, phone

Links sit **under the summary's first paragraph, above the tabs' content** for a claimed
character (the sheet is a high-frequency lookup — access pattern 2 in step 25), and inside
**"More about X" ▸ Details** for everything else. A knowledge-base link is not something you
need at the table; a sheet link is.

```
┌──────────────────────────────┐
│ 🧝 Thorin                    │
│ Played by Sam                │
│ ⚔ 1d20+1 · HP 44 · AC 18  ▾ │
│ ↗ D&D Beyond sheet           │   ← claimed Character: always visible
│ ┌────────────┬─────────────┐ │
│ │  Summary   │    Notes    │ │
└──────────────────────────────┘
```

Inside Details, for a DM on an NPC:

```
│ ▾ More about The Eye         │
│   Links                  [+] │
│   ↗ Beholder                 │
│     Monster · CR 13 · MM p.28│
│   ↗ my notes on running it   │
│     docs.google.com        ⋯ │
```

- `[+]` opens a sheet: **Knowledge base** (a search field over 26's corpus) or **A link**
  (URL + label).
- `⋯` on a link offers Copy link and Remove. Remove confirms, because it is an event.
- A stale knowledge-base link renders muted, with "no longer in your knowledge base" in
  place of its detail line, and no `↗`. It is still removable.

### Entry page, desktop (`lg+`)

Links are a block in the sticky right-hand panel, under Details, forced open per 25f, with
the same `[+]` and `⋯`.

## Files touched

New:

- `apps/TakeInitiative.Api/src/Features/Entries/Models/EntryLinks.cs` — `EntryLinkKind`, the
  record, and `EntryLinks.CanRead` / `.For` delegating to `EntrySources`
- `…/Entries/Models/Events/EntryLinkAdded.cs`, `EntryLinkRemoved.cs`
- `…/Entries/Api/PostEntryLink/PostEntryLink.cs` (+ request, validator)
- `…/Entries/Api/DeleteEntryLink/DeleteEntryLink.cs`
- `…/Entries/Links/EntryLinkResolver.cs` — resolves knowledge-base links in one query per
  request, never one per link
- `apps/TakeInitiative.Web/components/Entry/EntryLinks.vue`, `EntryLinkRow.vue`,
  `AddLinkSheet.vue`, `KnowledgeBasePicker.vue`, `DndBeyondField.vue`
- `apps/TakeInitiative.Web/utils/links.ts` — the host allowlist, label rules, the D&D Beyond
  URL shapes, and the display helpers, with unit tests

Modified:

- `…/Entries/Models/Entry.cs` — `EntryLink` replaced; the two events applied in the
  projection; `Links` documented as real
- `…/Entries/Api/GetEntry/EntryResponse.cs` — resolved, filtered links
- `…/Entries/Api/GetEntryHistory/GetEntryHistory.cs` — the two new `EntryChangeType` cases
- `…/Entries/EntryHub.cs` + `CampaignHub` notifications — push to the groups that may read
- `apps/TakeInitiative.Api/src/Features/Reference/KnowledgeBase/KnowledgeBaseQueries.cs` —
  a `ByIds` lookup for the resolver, and the "is this row linked?" query 26c stubbed
- `apps/TakeInitiative.Web/pages/app/campaigns/[campaignId]/wiki/[entryId].vue` — the
  section in both layouts
- `apps/TakeInitiative.Web/utils/api/schema.d.ts` — regenerated
- `docs/roadmap/22-ddb-link.md` — record that 22a is delivered here, and what remains
- `docs/roadmap/README.md` — statuses

## Steps

### 0. Start the stack (this PR)

Write this file; update `README.md` and add the note to `22-ddb-link.md`. `gh stack` on 26g.

### 27b. The model and the read path

1. Replace `EntryLink`; add `EntryLinkKind` and the two events; apply them in the `Entry`
   projection (append and remove by id, and `Links` ordered by `AddedAt`).
2. `EntryLinks.CanRead(entry, viewer)` → `EntrySources.CanRead(entry, viewer)`, with the
   doc comment explaining *why* it is the same rule, so nobody "fixes" it later.
3. `EntryLinkResolver`: take the entry's knowledge-base links, one `ByIds` query, return
   resolved rows plus a `Stale` flag for a row that is missing or `stale = true`.
4. `EntryResponse` returns resolved, filtered links. `GetEntryHistory` gains the two cases.
5. The GIN index on `Links` for the prune query, and wire 26c's stubbed link check to it for
   real.
6. Alba tests: a DM and a player reading an unclaimed Character (absent for the player), a
   claimed one (present for both), a Place (present for both), a missing row resolving stale,
   and history filtering. Append events directly — there is no endpoint yet.

### 27c. Add and remove

1. `POST …/entries/{entryId}/links` with the validator above; `DELETE
   …/entries/{entryId}/links/{linkId}` (404 for an unknown id, idempotent beyond that).
2. Authorise on `EditAccess` + DM, exactly as `PutEntryArticle` does.
3. Push `entryLinksChanged` only to the groups that may read them, the way
   `PutEntryStats` pushes `entryStatsChanged`.
4. Alba tests: each validation rule, the 20 cap, both duplicate cases, a non-editor getting
   403, the scheme rejections, and that a push does not reach a player for an NPC's link.

### 27d. The web

1. `EntryLinks.vue` in both layouts per the sketches, the add sheet, the knowledge-base
   picker (reusing 26f's search endpoint, debounced), and the `⋯` menu.
2. The stale rendering, and Remove's confirm.
3. `utils/links.ts` + `vitest`: the scheme allowlist, label trimming and limits, the display
   host, and the stale state.
4. Optimistic add and remove through TanStack Query, rolled back on error, matching how
   14d's note actions do it.

### 27e. D&D Beyond as a preset

1. On a claimed Character, Details gains a **D&D Beyond sheet** field. It creates a normal
   `External` link with the label "D&D Beyond sheet" and a validated
   `dndbeyond.com/characters/…` URL, so nothing about the model is special-cased.
2. It renders under the claim line per step 22's design and the sketch above.
3. Update `22-ddb-link.md`: 22a is delivered here; 22b and 22c (the unofficial fetch behind
   its off-by-default flag) remain post-MVP.

## Verify

1. Green CI on every PR, `0 Warning(s), 0 Error(s)`, `nuxi typecheck` clean, `schema.d.ts`
   committed from `gen:api` rather than hand-edited.
2. `dotnet test` covers the read rule for all four entry shapes, both write endpoints' rules,
   the cap, the duplicates, the scheme rejections, stale resolution, history filtering, and
   the push audience.
3. By hand, with an ingested knowledge base:
   - A DM adds Beholder to "The Eye" (an unclaimed Character). A player reloads and sees no
     Links section at all.
   - The DM claims it for a player. The player now sees it. Unclaim; it goes.
   - A player adds their D&D Beyond URL to their own character. Every member sees it. A
     `javascript:` URL and a 3,000-character URL are both refused inline.
   - `--prune --force` an ingest that drops Beholder: the link renders stale, and the row is
     kept as `stale` rather than deleted (26c's protection, now with a real link).
   - The entry's history shows both the add and the remove, with who and when.
4. At 390 × 844 the sheet link is visible on a claimed character without scrolling, and the
   add sheet is not covered by the keyboard (invariant 11). At 1440 × 900 Links sits in the
   right panel.

## Notes / gotchas

- **Do not denormalise the knowledge-base row into the link.** It is tempting (one less
  query) and it is wrong: the URL, page and label come from the corpus, and a re-ingest must
  be able to correct them everywhere at once. Resolve on read, one query per request.
- **`EntrySources.CanRead` is load-bearing.** If someone later "simplifies" link visibility
  to `EntryVisibility.CanSee`, every NPC's stat block leaks through its links. The doc
  comment on `EntryLinks.CanRead` must say so.
- **`Entry.Links` was declared but never written**, so there is no migration and no existing
  data to worry about — but the shape changes, and any event stored with the old
  `EntryLink(Url, Label)` shape would fail to deserialise. Confirm none exists before
  merging 27b (`select count(*) from mt_events where type like 'entry_link%'` should be 0).
- **The 26c prune check was stubbed** against a query with no writers. 27b is where it gets
  its first real one, so re-run 26's Verify 3 prune cases after this step.
- **Label is never markdown.** The article renders markdown; a link label does not. Rendering
  it would make a link label an injection surface for no benefit.

### Errors this plan had, found by building it

1. **`IKnowledgeBaseLinks` could not be "one class in the API".** The ingest CLI is the only
   caller of `PruneAsync` and references only the package, so an API-side implementation would
   never have run on the one path the protection exists for. It lives beside the interface,
   wired from the CLI.
2. **`?|` cannot index `Entry.Links`.** It addresses top-level strings only, and `Links` is an
   array of objects. A derived `Entry.LinkedItemKeys` string array carries the index, following
   `ArticleMentionIds`.
3. **`srd52` links would have been permanently stale**, because SRD rows are an in-assembly
   catalogue rather than `knowledge_base_item` rows. The resolver handles both.
4. **The claimer cannot write links**, per the correction above.
5. **History's visibility rule was unspecified.** The codebase has both "as it was" (stats) and
   "as it is now" (source). Links use *now*, because *then* would hide a link from `GET entry`
   and hand it back through the history after an unclaim.
6. **The stale rendering contradicted itself** — the Goal paragraph gives a stale link an `↗`,
   the Layouts section says it has none. Layouts is right: there is nowhere honest to send the
   reader, so `url` comes back null while the last-known name and detail stay, so the member can
   tell which link went.
7. **The 20-cap is a 409 only when the request is otherwise valid.** FastEndpoints validates
   before the handler, so a 21st link that is also malformed is a 400.

Two gotchas worth knowing for any future endpoint, found here:

- **`ProducesProblemFE(403)` breaks `gen:api`.** FastEndpoints already emits a contentless 403,
  and a second declaration produces `"application/problem+json": null`, which
  `openapi-typescript` crashes on — a green `dotnet build` with a broken web pipeline.
- **`NotEmpty()` inside a `When(...)` is still reported as unconditionally required**, so a
  conditional field becomes mandatory in the generated types. `Must(Present)` avoids it.

### Decisions for the user

Each has the default this plan uses.

1. **Per-link visibility.** *Default: no. Links follow `EntrySources.CanRead`, the rule
   `Source` already uses.* A toggle would let a DM show one link on an NPC while hiding
   another, at the cost of a fourth visibility rule to reason about.
2. **Editing a label.** *Default: remove and add, which shows as two history rows.* An
   `EntryLinkLabelChanged` event would read better in history for one more event type.
3. **Links on notes.** *Default: no — entries only.* A note mentions an entry by id
   (invariant 6) and the entry carries the link, so note→knowledge-base already resolves
   transitively. Links on notes would put the same truth in two places.
4. **The cap.** *Default: 20 links per entry.*
5. **Other knowledge bases.** *Default: `5etools` and `srd52` are the only providers a
   knowledge-base link may name.* D&D Beyond as a *knowledge base* (rather than an external
   URL) would need an ingestible dataset, which it does not have.
