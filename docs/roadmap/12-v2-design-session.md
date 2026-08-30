# 12 — v2 design session (GATED)

## Goal

Agree the **design invariants** for v2 together, before any step files are
written. The output of this session is a filled-in Invariants section at the
bottom of this document, plus steps 13+.

## This is not a plan to execute

Everything below is **material to argue with**, not decisions already taken. It is
here so the thinking done during the original planning session isn't lost or
re-derived — not to pre-commit the design.

Pre-writing Stage 3 as executable steps is exactly how this scope-creeps into
something nobody wanted. Nothing here gets built until the invariants are agreed.

## Depends on

Stage 2 complete, and ideally a few sessions of actually *using* the revived app —
the point of ordering it this way is that real use should inform the design.

---

## Proposal 1: the campaign is a single linear event stream

A campaign is one Marten event stream. Sessions, combats and notes are **spans and
entries within it**, not separate aggregates.

```
Campaign stream (streamId = campaignId)
├── CampaignCreated
├── SessionStarted        { sessionId }
│   ├── NoteWritten       { sessionId, noteId, markdown }
│   ├── CombatStarted     { sessionId, combatId }
│   │   ├── CharacterStaged   { sessionId, combatId, ... }
│   │   ├── InitiativeRolled  { sessionId, combatId, ... }
│   │   ├── NoteWritten       { sessionId, combatId, noteId, ... }   ← interleaved
│   │   ├── TurnEnded         { sessionId, combatId, ... }
│   │   └── CombatFinished    { sessionId, combatId }
│   ├── NoteWritten       { sessionId, noteId, ... }
│   └── SessionEnded      { sessionId }
```

**The argument for it:** notes taken *during* a combat, correctly ordered against
that combat's own events, only fall out naturally when they share one ordered log.
Split combat into its own stream and you are merging by wall-clock timestamp
forever — and timestamps lie under clock skew, batched writes and retries. One
stream makes "the campaign is the story" literal; session recaps, entity backlinks
and live combat state all become *views* of it.

**Proposed hard rule:** every event carries `SessionId` and `CombatId`
**explicitly**, even though stream position implies them. Events must be
self-describing, never position-dependent. That is what keeps rebuilds honest.

**Three consequences, previously accepted — worth re-examining:**

1. **Read models are eventually consistent.** One stream fanning out to many
   documents means the grouping key is not the stream id, so these are
   `MultiStreamProjection`s, which Marten strongly recommends running **async**
   via the daemon. The daemon is already registered (`AddAsyncDaemon(DaemonMode.Solo)`).
2. **One hot stream per campaign.** Concurrent appends — multiple simultaneous
   combats — contend on Marten's per-stream optimistic concurrency. In practice
   one DM at one table, so contention is near zero; plain appends plus a retry
   wrapper, and avoid `AppendExclusive` for routine writes.
3. **Streams get long.** A long campaign is plausibly 50k+ events. Fine for
   incremental async projections. **Never `AggregateStreamAsync` the campaign
   stream** — always read projected documents.

**Proposed projections** (all `MultiStreamProjection`, async):

| Projection | Grouped by | Produces |
|---|---|---|
| `CampaignProjection` | campaignId | Campaign header, members, session index |
| `SessionProjection` | sessionId | Ordered timeline — notes and combat events interleaved |
| `CombatProjection` | combatId | Live combat state |
| `EntityProjection` | entityId | Entity page + backlinks |
| `GraphProjection` | campaignId | Entity adjacency for the graph view |

---

## Proposal 2: provenance, in two layers

One `POST /api/note` legitimately produces `NoteWritten` plus several
`EntityCreated` plus several `ReferenceAdded`. Without provenance you cannot
answer "why does this entity exist?", "did a human write this or did a model infer
it?", or "undo that one request".

This has to be designed in from day one — retrofitting is painful precisely
because old events simply don't have the fields.

**Layer 1 — infrastructure.** Marten stores `correlation_id`, `causation_id`,
`headers` and `LastModifiedBy` on `mt_events` natively, but **they are off by
default**:

```csharp
opts.Events.MetadataConfig.CorrelationIdEnabled = true;
opts.Events.MetadataConfig.CausationIdEnabled   = true;
opts.Events.MetadataConfig.HeadersEnabled       = true;
```

Set them per-request in middleware. Answers *"which events came from one user
action"* for free, on every event, with no payload changes. Projections read them
by declaring `Apply(IEvent<NoteWritten> e, ...)` instead of `Apply(NoteWritten e, ...)`.

**Layer 2 — domain.** Correlation IDs say *which request*; they don't say *what
kind of thing acted*. A discriminated union on the payload, rather than a
`Source: Manual | Llm` enum, because each case carries different data:

```csharp
Actor = Human  { userId }
      | Llm    { model, promptVersion, confidence }
      | System { reason }        // import, migration, backfill
```

This generalises the LLM decision instead of special-casing it. **Payoff:**
correlation-scoped undo, an honest "auto-detected — confirm?" affordance, and
bulk-reverting one bad LLM run by model + prompt version without touching
anything a human wrote.

---

## Proposal 3: the knowledge graph

Notes are markdown. `#` opens autocomplete over campaign entities; referencing a
name that doesn't exist creates it.

Entity types: Character, Place, Thing, Faction, Item — closed enum initially.

```
NoteWritten      { noteId, sessionId, combatId?, markdown, actor }
NoteEdited       { noteId, markdown, actor }
EntityCreated    { entityId, type, name, actor, method }
EntityRenamed    { entityId, name, actor }
EntityMerged     { fromId, toId, actor }
ReferenceAdded   { noteId, entityId, actor, confidence? }
ReferenceRemoved { noteId, entityId, actor }
```

**`EntityMerged` matters more than it looks.** Free-text naming guarantees
"Gundren" and "Gundren Rockseeker" both end up as nodes, and without a merge event
the graph fragments silently with no way back.

Note `ReferenceAdded` is the **same event** whether a human typed `#Gundren` or a
model inferred it — only `actor` differs. One projection code path, and
"human-asserted only" becomes a filter rather than a separate query.

---

## Proposal 4: frontend

- Nuxt + shadcn-vue + Tailwind — matches both Ripple and the existing app.
- `@vite-pwa/nuxt` on Ripple's exact config: `strategies: 'injectManifest'` with
  `injectManifest: { injectionPoint: undefined }`. Installable, hand-written
  service worker, **no precaching and no offline support** (already decided).
- TanStack Query as the data layer.
- SignalR for real-time. This is where async projections stop mattering: mutation
  → append → daemon projects → SignalR broadcasts → `queryClient.setQueryData`.
  Projection lag is absorbed by the cache layer instead of leaking into
  components. Today's `setCombatQueryData` in `utils/queries/combats.ts` is the
  pattern to carry over.
- Mobile-first, **one responsive layout** — replacing today's parallel
  `FullScreenCombatDetailsCard.vue` / `MobileCombatDetailsTabs.vue`.
- Drop `Campaign.ActiveCombatId` so multiple combats can run at once.
- **Generate types from the API's OpenAPI document.** `utils/types/models.ts` is
  433 hand-maintained Zod schemas mirroring the C# models, and has already drifted
  (see step 07's `CombatState` fix). Hand-maintenance is the root cause.

---

## Proposal 5: likely scope

Sequencing to be decided in the session, not now.

- Campaign spine — stream, sessions, timeline projection, auth
- PWA shell — installable, mobile nav
- Notes + references — markdown editor, `#` autocomplete
- Knowledge graph — `EntityProjection`, backlinks, entity pages
- Combat port — the existing 15 event types and `InitiativeRoller` logic moved
  into the campaign stream. ~6,800 lines that already work; mostly mechanical
  once the stream exists.
- Graph view — adjacency via shared-note co-occurrence
- LLM extraction — emits `ReferenceAdded` with `Actor.Llm`; adds no new events if
  Proposal 2 holds

---

## Open questions

1. **Does the single-stream model survive contact with a real campaign?** The
   concurrency and stream-length arguments are theoretical until there's data.
2. **What is a "session", exactly?** A real-world play session, or a
   user-delimited chapter? Does it auto-close? Can combats span two sessions?
3. **Is `EntityMerged` enough for deduplication**, or is aliasing needed too —
   "Gundren" as an alias *of* "Gundren Rockseeker" rather than a merge?
4. **Who can write notes?** DM only, or every player? That changes both the
   permission model and the graph's shape.
5. **Are entity types a closed enum or user-extensible?** Closed is simpler;
   user-defined types is what Obsidian would do.
6. **Does combat really need to move into the campaign stream**, or could it stay
   its own stream with only its *summary* events posted into the campaign log?
   This is the biggest open architectural question and directly contradicts
   Proposal 1 — worth arguing properly.
7. **What does the DM see on their phone during combat?** The mobile-first claim
   needs a concrete screen before the data model is finalised.
8. **Migration**: is there production data worth carrying into v2, or does v2
   start empty?
9. **The Bestiary branches** — `Bestiary_2025`, `Bestiary_2025_CopyParsing`,
   `Bestiary_2025_project_refactor` hold ~2,800 lines of an unmerged
   5eTools-ingesting API (LiteDB + Playwright scraper, its own `.sln`, a committed
   2.5 MB `.db`), off both `dev` and `main`. Adopt, park, or delete? A monster
   stat-block source would feed the knowledge graph well.

---

## Invariants

> **To be filled in together during the design session.**
>
> These are the properties the system must hold no matter what gets built —
> the things a later change is not allowed to violate. Also record what is
> explicitly **out of scope**, because that is what stops the scope creep.

_(empty)_

---

## Out of scope

> **To be filled in together.**

_(empty)_
