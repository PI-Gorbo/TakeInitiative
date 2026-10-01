# 28 — Knowledge-base match suggestions

## Goal

When a wiki entry looks like something in the knowledge base, the app **asks**. An entry
called "Beholder", or "The Eye" with the alias "Beholder", shows one dismissible prompt to
the members who could act on it:

```
✨ Is this the Beholder from the Monster Manual?
   Monster · CR 13 · MM p. 28              [ Link it ]  [ No ]
```

**[Link it]** creates a normal step 27 knowledge-base link, as that member, in one call.
**[No]** records a dismissal so the entry never asks again. Nothing is linked without a tap,
and the app never guesses in the background.

This is the flow the user described: a member mentions a thing, an entry gets created the
normal way, and *then* the entry offers the reference link if one looks likely. If nothing
matches, nothing appears.

**No machine-learning model is involved, and that is the point.** Step 23's GLiNER runs in
the browser because it has to find *spans inside free text*. This step compares *one name
against a corpus of names*, which is exactly what Postgres' `tsvector` plus `pg_trgm`
already do for ⌘K. So it is a query, it runs on the server, it is deterministic, and it
needs no download, no worker and no `Actor.Model` provenance.

"Running" at the end: a DM creates "Beholder" from a note and opens it. The entry offers the
Monster Manual row; one tap links it, and the prompt is replaced by step 27's link line. On
"The Eye" the DM adds the alias "Beholder" and the prompt appears for the alias. The DM taps
No on a Place called "Waterdeep" that matched a magic item, and it never asks again — not
after a reload, not for the other DM. A player looking at an unclaimed NPC never sees a
prompt at all, because the suggestion would give the monster away.

The step ships as three PRs stacked with `gh stack` on top of 27e. Each PR leaves the app
runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 28a | `v2/28a-kb-suggestions-plan` | This plan | Docs only | [ ] |
| 28b | `v2/28b-kb-match-api` | The match query and the dismissal (API) | 27's app unchanged in the browser. `GET entries/{id}/knowledge-base-suggestions` answers candidates, and `POST …/dismiss` silences them | [ ] |
| 28c | `v2/28c-kb-prompt` | The prompt on the entry page | The prompt, Link it, No, and the empty case. The step's Verify passes | [ ] |

## Depends on

- **26**: `knowledge_base_item`, its `tsvector` and trigram index, and
  `KnowledgeBaseQueries`. The matching is one query against that table.
- **27**: the link the prompt creates, `EntryLinks.CanRead` (which decides who sees a
  prompt), and the Links section the prompt sits above.
- **20**: `ReferenceMatcher`'s 0–4 match ladder, so "how good is this match" means the same
  thing here as in ⌘K.
- **23**: only for its **UX vocabulary** — the ✨ affordance, "suggestions, never facts", and
  accept-by-tap. None of its model machinery is used. `PostSuggestionMatch` is the closest
  existing endpoint to copy for shape.

## What counts as a match

Candidates come from the entry's **name and its aliases**, each matched against
`knowledge_base_item.name` with the same ladder ⌘K uses (0 exact, 1 prefix, 2 word prefix,
3 substring, 4 fuzzy via `word_similarity`).

The prompt is deliberately quiet:

- **Only ladder 0 and 1** qualify — exact, or a prefix. A substring or fuzzy match produces
  far more noise than value on a corpus of thousands of names. ⌘K can afford ladder 4
  because the user typed the query and is looking at results; a prompt that appears
  uninvited cannot.
- **The entry's kind must be compatible** with the row's category: a `Character` may match a
  Monster, an `Item` may match an Item or a Spell, an `Other` may match a Spell, and a
  `Place`, `Faction` or `Event` matches nothing. This alone removes most false prompts.
  (`Other` was missing from this list and had to be added in 28b: there is no Spell entry
  kind, so `KnowledgeBaseItemRow.Summary` already files every spell row under `Other` for
  + Wiki. Without the clause a corpus of spells could never be suggested at all, and
  "Fireball ↗ Fireball" is the least ambiguous prompt in the feature.)
- **At most 3 candidates**, best first. If the best is ladder 0 and the next is ladder 1,
  only the exact one shows; the others are behind "3 possible matches" which expands.
- **No prompt at all** when the entry already has a knowledge-base link, when it has been
  dismissed, or when nothing qualifies.

There is no confidence score shown, because a trigram similarity is not a probability and
displaying one would imply more than it means. The row's own detail line — "Monster · CR 13
· MM p. 28" — is what lets the member judge.

## Who sees a prompt

**Exactly the members who could accept it**: `EntryLinks.CanRead` **and** write access to the
entry (`EditAccess` + DM, as in 27c). Both halves matter:

- The read half stops the disclosure. A prompt saying "Is this the Beholder?" on an unclaimed
  NPC is the same stat-block leak step 27's read rule exists to prevent — arguably worse,
  since it names the monster without anyone having linked it.
- The write half stops a useless prompt. Showing a question to someone whose only option is
  to dismiss it is noise.

## Dismissal is an event

A dismissal is user intent — "I looked at this and it is not that" — so it belongs on the
Entry stream like everything else:

```csharp
public sealed record EntryKnowledgeBaseSuggestionDismissed(
    Actor Actor, string Provider, string ItemId) : IActorEvent;
```

- Dismissal is **per candidate row**, not per entry. Dismissing the magic item does not
  silence a later, better monster match.
- It is **campaign-wide, not per member**. One DM deciding "The Eye is not the Beholder" is a
  fact about the entry, not a personal preference, and re-asking the other DM the same
  question is the nagging this event exists to stop.
- `Entry` projects `DismissedSuggestions` as a set of `(provider, itemId)` and the query
  excludes them.
- It shows in history as a minor change ("Dismissed a suggestion · Beholder (5eTools)") so
  the decision is traceable.

Adding a link also implies dismissal of the others for that entry — once it is linked, the
prompt is gone anyway, and the link itself is the record.

## Layouts

### Entry page, phone

The prompt sits **directly above the Links section**, inside Details, so it appears where
its outcome lands — and it is drawn by `EntryLinks.vue` itself, so it follows that section
wherever it goes (high on a claimed Character, inside Details on everything else, and in the
right-hand panel at `lg`) rather than being mounted three times.

The count also shows in the "More about X" header as "✨ N", because a prompt buried in a
collapsed section would never be seen. 28c shows it whenever the prompt is in there, not
only "for an entry with no links at all" as this plan first said: the number of links has
nothing to do with how buried the question is, and an NPC with a D&D Beyond link would
otherwise hide its prompt for no reason. It is left out only when Links — and with it the
prompt — are already high on the page, which is a claimed Character.

```
│ ▾ More about The Eye     ✨1  │
│   ┌──────────────────────────┐│
│   │ ✨ Is this the Beholder   ││
│   │    from the Monster      ││
│   │    Manual?               ││
│   │ Monster · CR 13 · MM p.28││
│   │ [ Link it ]        [ No ]││
│   │ 2 other possible matches▾││
│   └──────────────────────────┘│
│   Links                   [+] │
```

Both buttons are 44px. **No** does not confirm — it is cheap and reversible by adding the
link by hand.

### Entry page, desktop (`lg+`)

The same card at the top of the right-hand panel's Details block.

## Files touched

New:

- `apps/TakeInitiative.Api/src/Features/Reference/KnowledgeBase/KnowledgeBaseSuggester.cs` —
  the kind-compatibility rule and the exclusions; the statement itself is
  `KnowledgeBaseQueries.SuggestAsync`, because that class is "every read of
  `knowledge_base_item`" and a second place for SQL over that table would make it untrue
- `…/Entries/Models/Events/EntryKnowledgeBaseSuggestionDismissed.cs`
- `…/Entries/Models/EntrySuggestions.cs` — `EntrySuggestionDismissal`, and `CanSee`
- `…/Entries/Api/GetEntryKnowledgeBaseSuggestions/` — the endpoint, its response
- `…/Entries/Api/PostEntrySuggestionDismiss/` — the endpoint, its request
- `apps/TakeInitiative.Web/components/Wiki/KnowledgeBaseSuggestion.vue` (the wiki's
  components live in `components/Wiki/`, not `components/Entry/`)
- `apps/TakeInitiative.Web/composables/useKnowledgeBaseSuggestions.ts`
- `apps/TakeInitiative.Web/utils/kbSuggestions.ts` + its unit tests (the prompt's wording,
  the expand/collapse rule, kind compatibility mirrored for the client)

Modified:

- `…/Entries/Models/Entry.cs` — `DismissedSuggestions`, and the event applied
- `…/Entries/Links/EntryLinkResolver.cs` — resolution by `(provider, id)`, so history can
  name a dismissed row the way it names a link's
- `…/Entries/Api/GetEntryHistory/GetEntryHistory.cs` — the new change type
- `apps/TakeInitiative.Web/components/Wiki/EntryLinks.vue` — the prompt above it
- `apps/TakeInitiative.Web/pages/app/campaigns/[campaignId]/wiki/[entryId].vue` — the
  "✨ N" badge on the collapsed section header
- `apps/TakeInitiative.Web/utils/api/schema.d.ts` — regenerated
- `docs/roadmap/README.md` — statuses

## Steps

### 0. Start the stack (this PR)

Write this file; update `README.md`. `gh stack` on 27e.

### 28b. The match query and the dismissal

1. `KnowledgeBaseSuggester.For(entry, take: 3)`: one query over name plus aliases, ladders 0
   and 1 only, kind-compatible categories, excluding dismissed rows, excluding rows already
   linked, ordered by ladder then name length then name.
2. `GET …/entries/{entryId}/knowledge-base-suggestions` — authorised on `EntryLinks.CanRead`
   **and** write access; returns `[]` (not 403) when the caller may not see them, so the web
   has one code path and reveals nothing by status code.
3. `POST …/entries/{entryId}/knowledge-base-suggestions/dismiss` with `{ provider, itemId }`
   — appends the event, idempotent, authorised as the write path (`EntryLinks.CanWrite`
   alone, **not** that and `CanRead`: adding the read check is the trap `CanWrite`'s own
   remarks describe). It answers the suggestions that are **left**, through the same
   redaction the `GET` uses, so the web has one response shape and one code path.
4. Project `DismissedSuggestions`; add the history case.
5. Alba tests: an exact match, a prefix match, a fuzzy match producing nothing, each
   incompatible kind, the linked-already case, dismissal silencing one candidate but not
   another, a player getting `[]` on an unclaimed Character, and a non-editor getting `[]`.

### 28c. The prompt

1. The component, the composable (TanStack Query, invalidated by a link add and by a
   dismissal), the expand for the other candidates, and the "✨ N" badge on the collapsed
   header.
2. **Link it** calls 27c's `POST …/links` and then invalidates both queries, so the prompt
   is replaced by the link line with no extra endpoint.
3. Optimistic dismissal, rolled back on error.
4. `vitest`: the wording for one versus several candidates, kind compatibility, and that
   nothing renders for an empty response.

## Verify

1. Green CI on every PR, `0 Warning(s), 0 Error(s)`, `nuxi typecheck` clean, `schema.d.ts`
   from `gen:api`.
2. `dotnet test` covers every case in 28b.5.
3. By hand, with an ingested knowledge base:
   - Create a Character entry "Beholder". The prompt offers the MM row. Tap **Link it**: the
     prompt goes and step 27's link line appears.
   - Create "The Eye" with no alias: no prompt. Add the alias "Beholder": the prompt appears.
   - Tap **No** on it, reload: no prompt. Sign in as the other DM: still no prompt.
   - Create a Place called "Beholder": no prompt (kind incompatible).
   - As a player, open an unclaimed Character called "Beholder": no prompt, and the network
     response is `[]` rather than a 403.
   - The entry's history shows the dismissal and the link, with who and when.
4. At 390 × 844 the prompt is reachable and both buttons are 44px. At 1440 × 900 it sits at
   the top of the right panel.

## Notes / gotchas

- **Ladder 0–1 only.** It is tempting to reuse ⌘K's full ladder for "better" recall. Don't:
  ⌘K's results were asked for, and a prompt was not. A fuzzy match on a few thousand names
  will offer "Beholder" for "Behold the Sky" and train the user to ignore the ✨.
- **The endpoint returns `[]`, not 403, when the caller may not see suggestions.** A 403
  would tell a player "there is something here you may not see", which is the leak in a
  different form. Invariant 5's "hidden things are absent, not greyed out" applies to status
  codes too.
- **This is not step 23.** If someone later wires GLiNER into this step, they have
  misunderstood it: there is no free text here, only a name. Keep it a SQL query.
- **Dismissal is campaign-wide on purpose** and that is a real trade-off: one DM can silence
  a suggestion for the other. The alternative — per member — means the same question gets
  asked twice and answered twice. It is in "Decisions for the user".
- **Aliases change.** A dismissal keyed on `(provider, itemId)` survives a rename or an alias
  edit, which is what you want: the answer was about the *thing*, not about the spelling.

### Decisions for the user

Each has the default this plan uses.

1. **Dismissal scope.** *Default: campaign-wide.* Per-member stops one DM overriding another
   at the cost of asking everyone separately.
2. **The match ladder.** *Default: exact and prefix only (0–1).* Widening it to word-prefix
   (2) would catch "Beholder" inside "Ancient Beholder" at a real cost in false prompts.
3. **Where else a prompt could appear.** *Default: the entry page only.* It could also go in
   Loose ends as a batch ("12 entries look like reference items"), which is a better DM
   workflow for an existing campaign and more surface to build. Worth doing after MVP if
   linking turns out to be something you do in bulk.
4. **Recording that a link came from a suggestion.** *Default: no — the link is a plain
   member action.* Step 23's `Actor.Model` provenance exists because a *model* asserted
   something; a trigram match is not a model, and claiming model provenance for a SQL query
   would make the "revert by model version" feature meaningless.
