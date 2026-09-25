# 12 — v2 design

**Status:** agreed in the design session of 2026-09-24/25. This document is the v2
design; steps 13+ implement it. The earlier agenda's proposals are recorded under
[Superseded](#superseded) at the bottom.

## Goal

Rebuild TakeInitiative from scratch (data resets, v2 starts empty) around four
player-first experiences: **combat**, **note taking**, **⌘K search** and
**exploring connections**. DM-centred tooling comes after.

---

## 1. Glossary: the law. Code, API and UI use these words

| Term | Meaning | Not called |
|---|---|---|
| **Campaign** | Top-level container. Also the name of the first tab: the session stream, plus session, member and settings management for DMs. | — |
| **Member** | A user in a campaign. **Role**: `DM` or `Player`. There can be several DMs; the **owner** promotes and demotes. | "player" when it means a user; today's three copies of membership |
| **Session** | One real-world play session. Numbered, dated, with an optional title. | chapter, episode, journal |
| **Session note** (UI short form: "note"; code: `SessionNote`) | One post in a session's stream: markdown text plus optional images. Only its **author** can edit it. | message, post, journal entry |
| **Recap** | A session note flagged `/recap`. Shown under its session's divider. | summary |
| **Wiki** | The campaign's knowledge base: all entries. Built by integrating session notes. | knowledge base, codex |
| **Entry** | One named thing in the wiki. | entity, page, topic, tag |
| **Kind** | An entry's category, from a closed set: `Character`, `Place`, `Faction`, `Item`, `Event`, `Other`. | type |
| **Article** | An entry's editable content (markdown, can contain mentions). | description, body |
| **Timeline** | The read-only list on an entry of every session note that mentions it. | backlinks (in UI) |
| **Promote** | "Add to wiki": copies a selection of a session note into an entry's article as a quote that links back to the note. | integrate, pin |
| **Secret block** | A block in an article with its own visibility (🔒 DM / 🔒 Me). | — |
| **Mention** | An `@` link to an entry, in a session note or an article. | tag, reference |
| **Alias** | An alternative name that resolves to an entry. | — |
| **Connection** | Two entries mentioned together. It is explained by **evidence**, the snippets where they co-occur. | relation, edge |
| **Visibility** | Who can see something: `Everyone`, `DM` (all DMs plus the author), `Me`. Applies to session notes, entries and secret blocks. | audience, privacy |
| **Edit access** | Who can edit an entry: `Anyone` (who can see it) or `Only me`. DMs can always edit. | — |
| **Loose ends** | Unidentified things that are waiting to be linked (§5). | inbox, triage |
| **Player character** | A Character entry claimed by a member. A member can claim several. | PC in the UI |
| **Stats** | An optional stat line on a Character entry: initiative roll, max HP, AC. | — |
| **Reference** | Rules content from outside the campaign (SRD, 5eTools) that ⌘K can find. It is never part of the wiki until it is added. | compendium, bestiary |
| **Source** | Where an entry or session note came from: a reference item, a D&D Beyond sheet, or an imported Discord message. | origin |
| **Suggestion** | A mention or entry proposed by a model. It has no effect until a member accepts it. | auto-tag, prediction |
| **Import** | Bringing outside notes (e.g. a Discord export) in as session notes. | sync, migration |
| **Combat** | One encounter. Status: `Draft`, `Active` or `Finished`. | planned/draft combat as a separate thing |
| **Combatant** | One row in a combat. It can link to an entry. | Staged/Initiative/Planned character |
| **Round / Turn / Condition** | As in 5e. A condition is a text label with an optional note. | — |

Rule: a new noun gets added to this table before it appears in code.

## 2. Core loop: capture in sessions, integrate into the wiki

A note is **posted at a point in a session**, where it happened. There is no
choosing a place, only which session and who can see it. Discord was the point of
reference, but there are no channels.

```
 Session notes ──@mention──▶ entry Timeline (automatic, read-only, raw record)
                               │
                               └─ Promote ──▶ entry Article (curated, editable)
 Entry page ── "Add a note about X" ──▶ new session note in the current session
```

## 3. Campaign tab: sessions and the composer (text UX)

The Campaign tab shows one stream of every session, separated by session
dividers. It opens at the latest session. Filters: `All · Text · Images · Recaps
· Combats · Mine`.

```
━━━━━━━━  Session 12 · Sat 20 Sep · "The Triboar Trail" · 🧵 3 loose ends  ━━━━━━━━
 📜 RECAP  Sam — We left Neverwinter with @Gundren's wagon…
 Sam  7:42pm   We met @Gundren Rockseeker on the road to @Phandalin. **10gp each.**
 Alex 7:50pm 🔒DM   I pocketed the @Strange Black Pearl before anyone saw.
 ⚔ Combat · Goblin Ambush · 3 rounds · @Klarg, 4× @Goblin            [open]
 Priya 8:15pm  [map.jpg] Map of @Cragmaw Hideout, found on @Klarg
 Priya 8:20pm  [img.jpg]  ⚠ Tag what's in this?
━━━━━━━━  Session 13 · Sat 27 Sep  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

**Composer:**
```
│ Posting to: Session 13 ▾      Visible to: Everyone ▾                 │
│ Sildar said @Glasst▏                                                  │
│   ＋ Create "Glasstaff"  [Character ⇥]     ← Tab cycles the kind      │
```
- `@` opens one autocomplete over entry names and aliases. An unknown name offers
  **Create**. The new entry takes its visibility from the note (🔒 DM note → DM
  entry). The display text can be overridden: `@[the old dwarf](Gundren)`.
- A mention is stored as `@[text](entry:<id>)`. Renames and merges never rewrite
  the text.
- Markdown is supported. `/` commands: `/recap`, `/dm`, `/me`, `/session 11`.
- If you mention a DM-visible entry in an Everyone note, you get a warning: "This
  entry is hidden from players. Reveal it?" Nothing is revealed automatically.

**Sessions:**
- Any member can start the next session.
- The **current session is the latest one**. There is no auto-close, so a recap
  written the next morning stays in the session it describes.
- If the last session note is more than **3 days** old, the composer suggests
  "Start Session 14?". One tap accepts. Ignoring it posts to the current session.
- Picking an older session posts back in time, with an "added 3 days later" marker.
- Every session note and every combat belongs to exactly one session. A new
  campaign starts with Session 1.

**Editing and moderation:** only the author edits or deletes a session note, and
edits show "(edited)" with history. A DM can **hide** any note (for example an
accidental spoiler). The author is told and can still see it.

**Images:** the caption is the note's text, so mentions work. Posting without a
caption is allowed but nudged, and the image becomes a loose end.

**Combat screen:** has a slim composer that posts a normal note to the current
session. It is not tied to the combat.

## 3a. Mobile: PWA, with every command reachable by touch

- **PWA:** installable, using Ripple's `@vite-pwa/nuxt` config
  (`injectManifest`, no precaching, no offline). There is one responsive layout,
  with no separate mobile components. It respects safe-area insets and uses touch
  targets of at least 44px.
- **Every `/` command has a button.** On mobile the composer toolbar sits just
  above the keyboard. On desktop it sits below the text box.
```
 ┌──────────────────────────────────────────────────────┐
 │ S13 ▾   👁 Everyone ▾                                  │  ← session / visibility (= /session, /dm, /me)
 │ Sildar said @Glasst▏                                   │
 ├──────────────────────────────────────────────────────┤
 │ [Glasstaff Character] [+ Create "Glasst…"]  ‹scroll›  │  ← mention strip (replaces the popup on mobile)
 │ [Character][Place][Faction][Item][Event][Other]        │  ← kind chips, shown only when creating
 ├──────────────────────────────────────────────────────┤
 │  @   📷   🖼   B   I   •   📜Recap                ➤    │  ← toolbar: mention, camera, gallery, bold, italic, list, recap
 └──────────────────────────────────────────────────────┘
       (on-screen keyboard)
```
- **Autocomplete adapts to the screen.** On desktop, a popover appears at the
  caret and Tab cycles the kind. On mobile, a **suggestion strip is docked above
  the keyboard**, so the keyboard never hides it. Kind is picked with chips.
  Positioning uses the `visualViewport` API so the composer and strip follow the
  keyboard.
- **The @ button** inserts `@` and opens the strip, for keyboards where `@` is
  hard to reach.
- **Note actions on mobile:** long-press a note to open an action sheet: Promote
  to wiki, Edit (author only), Hide (DM), Copy link. Promoting on mobile takes
  the whole note and lets you trim it in the article editor, because precise text
  selection on a phone is fiddly. Desktop also allows promoting a selection.
- **Share into the app:** the PWA registers as a Web Share Target for images.
  Sharing a map from the phone's gallery opens the composer with the image
  attached, posting to the current session. This is built in step 16.
- **⌘K on mobile:** the 🔍 button in the header opens a full-screen search sheet
  with the input focused. Results use the same sections, and actions become
  tappable rows.
- **Secret blocks and article editing:** the same toolbar, with a 🔒 button that
  wraps the selection or the current block.

## 4. Wiki and entries

```
 Gundren Rockseeker   Character · aka "Rockseeker"   Edit access: Anyone   [Edit]
 ───────────────────────────────────────────────────────────────────────────
 Dwarf prospector. Brother of @Tharden.
 > Hired us to escort a wagon to @Phandalin      — Sam, Session 12 ↗   (promoted)
 ┌ 🔒 DM ─────────────────────────────────────────┐
 │ Captured by @Klarg, taken to @Cragmaw Castle.   │   (other viewers never receive this)
 └─────────────────────────────────────────────────┘
 CONNECTIONS  @Tharden (3) · @Phandalin (4) · @Klarg (2)          [graph ↗]
 TIMELINE     S12  Sam    We met @Gundren on the road…            [Promote]
              S13  Priya  🖼 Letter from @Gundren                   [Promote]
 GALLERY      [img] [img]          COMBATS  Goblin Ambush (S12)
 [ Add a note about Gundren… ]   ← posts to the current session with @Gundren prefilled
```
- **Promote:** select text in a timeline note, choose "Add to wiki", then pick the
  entry. The selection is added to the article as a quote with a source link.
  Promoting a 🔒 note creates a **secret block** with the same visibility.
  Secrets can also be written straight into an article as secret blocks.
- **Entry visibility:** each entry has its own setting (Everyone / DM / Me),
  inherited from the note that created it. People without access get nothing: no
  entry, no search hit, no graph node.
- **Edit access** (the name, kind, aliases, article and merge all follow it):
  `Anyone` (the default) or `Only me`. DMs can always edit. The creator can change
  it. Every change is an event with an actor, so history is viewable and changes
  can be reverted. Concurrent article edits use optimistic versioning: on a
  conflict you reload and re-apply.
- **Merge** ("Gundren" into "Gundren Rockseeker"): the loser's name becomes an
  alias, its mentions resolve to the winner, its article is appended under
  "Merged from Gundren", and its id redirects.
- **Player characters:** a member claims a Character entry. **Stats** on a claimed
  entry are editable by its claimer and the DMs. Stats on unclaimed entries (NPCs,
  monsters) are DM-only. Stats supply the defaults when an entry joins a combat.
- **Wiki home:** `/campaigns/:id/wiki` browses entries by kind, sorted by most or
  most recently mentioned. **Loose ends (n)** sits at the top. An entry lives at
  `/wiki/:entryId`.

## 5. Loose ends

These are raised in the **Wiki** (a campaign-wide count) and on **each session
divider** (the ones from that session). A loose end is:
- an image note with no mention
- a session note with no mentions
- an entry whose kind is `Other`
- an entry that has been mentioned but has an empty article

Anyone can resolve one: add a mention (images and notes are resolved by their
author), set the kind, or write or promote into the article. Loose ends are
derived, never stored as flags, so they clear themselves once resolved.

## 6. Connections: evidence snippets, no relation schema

Two entries are **connected** when they are mentioned together in the same session
note or in the same article block. Each connection is *explained* by the snippets
where they co-occur. The text is the label. Visibility is automatic because you
only receive the snippets you can see. Typed relations can be added later without
a migration.
```
 Gundren ↔ Tharden   (3)
   "Brother of @Tharden."                          Gundren article
   "@Gundren and @Tharden found the mine together"  S14 · Sam
   🔒 "@Tharden was killed by @Nezznar"             S15 · DM   (DM only)
```
- **Seen at** is the connections between a Character and a Place. For example,
  "who has been to Phandalin".
- **Fought together** comes from combatants that are linked to entries.
- **v1 UI:** a Connections panel on every entry, plus a force-graph page inside
  Wiki that filters by kind, runs at depth 1–2, uses weight = number of pieces of
  evidence, and opens the evidence when an edge is clicked.

## 7. ⌘K search

```
 ⌘K  gund▏
 ENTRIES   🧙 Gundren Rockseeker  Character · 7 mentions
 NOTES     "We met Gundren Rockseeker on the road…"  Sam · S12
 IMAGES    🖼 "Letter from Gundren"  Priya · S13
 ACTIONS   ＋ New note mentioning "gund"   ⚔ Start combat   ▶ Start Session 14
```
- Covers entries (names and aliases, fuzzy), article text, note text and captions,
  sessions, combats and actions. Prefixes: `@` searches entries only, `>` searches
  actions only.
- Built on Postgres full-text search (`tsvector`) plus `pg_trgm`. No paid services.
- **Visibility is applied inside the SQL, including secret blocks.**

## 8. Combat v2: simplified

The audit found about 6.5k lines of code for about 7 real concepts:
- **Dead:** Paused (never emitted), PlayerJoined/Left, CombatTimingRecord,
  InitiativeStrategy, CharacterOriginDetails and CurrentPlayers.
- **Duplicated:** a History list duplicates Marten's event stream.
- **Tangled:** five character records and a ~100-line dice-array tiebreak merge.
- **Unsafe:** hidden NPCs leak because they are only filtered in the browser.

**Model:**
```
Combat    { Id, CampaignId, SessionId, Name, Status: Draft|Active|Finished,
            Round, TurnIndex, Combatants[] }
Combatant { Id, Name, EntryId?, OwnerMemberId?, Initiative?: int, Tiebreak: int,
            Hp?, MaxHp?, Ac?, Hidden, PlayersSee: Exact|Band|Nothing, Conditions[] }
```
- **Merges:**
  - `PlannedCombat` becomes a `Draft` combat.
  - The five character records become one **Combatant**. `Initiative == null`
    means "waiting to roll".
  - Stages, Quantity splitting and CopyNumber go.
- **Adding combatants:** `@Goblin ×4` gives Goblin 1–4, with defaults from Stats.
- **Initiative:** one int plus a hidden random `Tiebreak`. The DM can drag to
  reorder. Late joiners are just waiting combatants, and the next roll slots them
  in by sort.
- **HP:** rolled when the combatant is added. **PlayersSee is set per
  combatant**, defaulting to `Exact` for player characters and `Band` (Healthy /
  Bloodied / Down) for everything else. The DM can flip it any time. AC is visible
  only when it is `Exact`. This replaces both campaign display settings.
- **Permissions:** DMs create, start, finish and control NPCs. Drafts are DM-only.
  Players add their own player character and end their own turn.
- **Several combats can run at once** (`ActiveCombatId` is dropped).
- **Events (7):** `CombatCreated`, `CombatantsAdded`, `CombatantEdited`,
  `CombatantRemoved`, `InitiativeRolled` (rolls every combatant that is waiting),
  `TurnEnded`, `CombatFinished`. The history view reads the stream directly.
- **Endpoints (about 8):** create, add combatants, edit combatant, remove
  combatant, roll, end turn, finish, get.
- **Server-side redaction:** SignalR sends the full state to the DM group and a
  redacted state to the player group.
- **Campaign tab:** shows a combat card in its session. While a combat is live,
  there is a "Join combat" banner and the Combat tab pulses.

## 9. Architecture

- **Event-sourced Marten streams, one per aggregate:** Campaign (members, roles),
  Session, SessionNote, Entry (name, kind, aliases, article, stats, claim, edit
  access, visibility) and Combat. **Inline projections** keep reads consistent.
  This supersedes Proposal 1 (one stream per campaign): it brought contention and
  async lag, and ordering by session plus timestamp is enough at this scale.
- **Provenance:** correlation, causation and header metadata are on from day one.
  Every event carries `Actor { MemberId }`, shaped so a `Model { name, version,
  confidence }` case can be added (§11a)
  later.
- **Read models:**
  - `SessionStream`: notes and combat cards per session.
  - `EntryView`: article, timeline, gallery, combats.
  - `MentionIndex`: source (note or article block) → entry, with visibility. It
    drives timelines, connections, loose ends and search.
  - `SearchDoc`.
- **SignalR:** one `CampaignHub` with groups `campaign:{id}`, `campaign:{id}:dm`
  and `member:{id}`. Each change is sent only to groups allowed to see it. Combat
  uses per-combat DM and player groups.
- **Images:** `IBlobStore` over the S3 API. MinIO runs in `compose.dev.yml`; prod
  uses any S3-compatible bucket (e.g. R2).
- **Frontend:**
  - Nuxt, shadcn-vue, Tailwind, TanStack Query and the PWA config from Ripple.
  - Mobile-first. Bottom tabs **Campaign · Wiki · Combat**, with ⌘K and a search
    button everywhere.
  - **Types are generated from OpenAPI.**

## 10. Invariants

1. **The glossary is law.**
2. **Capture is at a point in a session. The wiki is built from session notes**
   (timeline) and curated through articles (promote or edit).
3. **Every session note and every combat belongs to exactly one session.** The
   current session is the latest one.
4. **Only a note's author edits it.** Entries follow their edit access, and DMs
   can always edit.
5. **Visibility is enforced on the server for every read and push**: the stream,
   entries, secret blocks, search, connections, loose ends and SignalR. Hidden
   things are absent, not greyed out. Nothing is revealed automatically.
6. **Mentions are stored by entry id.** Renames and merges never rewrite text.
7. **Connections are derived from co-mention evidence.** No stored relation
   records in v1.
8. **Hidden combat data never leaves the server** for players.
9. **Every event carries an Actor.** Correlation metadata is on from day one.
10. **No paid services in v1.**
11. **Mobile is first-class.** Every command has a touch control, and no popup is
    ever covered by the on-screen keyboard.

## 11. Post-MVP: integrations (designed now, built after step 19)

The MVP only reserves the two seams these features need. After that, each one is
purely additive:
- ⌘K goes through an `ISearchProvider` interface. The Wiki is the first provider.
- Each entry has `Source?: { provider, externalId, url }` and `Links[]`.

**Reference search in ⌘K.** Wiki results come first. Results from reference
sources appear under their own **REFERENCE** heading.
```
 ⌘K  goblin▏
 ENTRIES     👺 Goblin  Character · 12 mentions
 REFERENCE   Goblin          Monster · CR 1/4 · SRD 5.2       [view]  [+ wiki]
             Goblin Boss     Monster · CR 1 · MM (5eTools)    [↗ 5etools] [+ wiki]
             Goblin Pack Leader …
```
- **SRD 5.2 (CC-BY)** is bundled. Results open a read-only stat-block card.
- **5eTools** is **search-only**. A preprocessing script turns its raw JSON into
  a small index: name, category (monster, spell, item, …), source book, CR or
  level, the fields combat needs (HP, AC, DEX), and a deep link to 5etools.
  **The app never displays 5eTools content.** A result links out to the site. The
  index is built locally per deployment and is not committed. The parked
  Bestiary branches are mined for parsing knowledge and then deleted.
- **+ wiki:** creates an entry of the right kind with `Source` set. For
  monsters, it fills in Stats (HP, AC, initiative from DEX), so `@Goblin ×4`
  works in combat. The article starts empty, with a link to the source.

**D&D Beyond.**
- A player character entry can carry a D&D Beyond sheet URL, stored as a `Link`.
  The link out always works.
- **Refresh from D&D Beyond** (a manual button) reads that sheet's public
  character JSON and fills in name, portrait, max HP, AC and initiative bonus.
  The endpoint is unofficial, undocumented and only works for public sheets. If
  it breaks, the button fails gracefully with an error and the link stays.
- No live sync.

## 11a. Post-MVP: automated categorisation and import

**Goal:** make it cheap to bring in outside notes and links (the Discord channels
first), and have them identified and categorised mostly automatically.

**Model choice:** small **zero-shot entity extraction** models that run **in the
browser**:
- **GLiNER**: zero-shot named-entity recognition. You pass it labels at inference
  time, so the labels are simply our kinds: `Character`, `Place`, `Faction`,
  `Item`, `Event`. There's no training step and no custom label mapping.
- **Laya**: named by the user as a second small in-browser candidate. Evaluate it
  against GLiNER in step 23 before choosing.

Running on the client keeps invariant 10 (no paid services) intact: no inference
server and no API bill. The model is loaded lazily, only when someone opens
suggestions or an import, so the normal app bundle stays small.

**How it fits:**
- **Suggestions, never facts.** The model produces **suggestions**: "this span
  looks like a Place", or "this matches the existing entry @Phandalin". They are
  shown in **Loose ends** and inline on the note ("✨ 3 suggestions"). Accepting
  one records a normal mention by the member. The event's provenance also records
  the model, its version and its confidence (the `Actor.Model` seam in §9). So:
  - "Human-asserted only" is a filter, not a separate store.
  - A bad model run can be reverted by model and version without touching
    anything a human wrote.
- **Matching before creating.** A detected span is first matched against entry
  names and aliases, using the same trigram search as ⌘K. A new entry is proposed
  only when nothing matches, which keeps duplicates down.
- **Loose ends get smarter.** A session note with no mentions stops being just
  "unlinked" and becomes "unlinked, 2 suggestions". Uncaptioned images still need
  a human.

**Discord import (and other sources):**
```
 Import ▸ Discord export (.json)  →  preview
 ────────────────────────────────────────────────────────────────────
 #notes   142 messages   → Sessions 1–9   (grouped by the 3-day gap rule)
 #maps     23 images      → image notes, captions kept
 #recaps    9 messages   → flagged as recaps
 Authors: sam#1234 → Sam ✓   priya → Priya ✓   bot → skip
 ✨ Suggestions: 61 mentions, 18 new entries (review before import ▸)
 [ Import ]
```
- Uses the Discord **export file** only, with no bot and no API credentials.
  Channels are mapped to note types: text notes, image notes, or recaps.
- Messages are grouped into sessions with the same **3-day gap** rule, and the
  preview shows exactly where each one will land.
- Discord users are mapped to members, and unmapped authors are skipped. Each
  imported note keeps a `Source` link back to its original message.
- Imported notes default to `Everyone` visibility (they were already shared) and
  are posted as the importing member, with `Source` preserving the original
  author.
- Links in the export are kept as markdown. Link previews are out of scope.

## 12. Out of scope for v1

automated categorisation and import (§11a is post-MVP) · server-side LLMs · typed relations · DM prep tools beyond Draft combats ·
integrations (§11 is post-MVP) · channels ·
named-member visibility · custom entry kinds · map pins · real-time co-editing ·
notifications · offline · migrating v1 data (v2 starts empty).

## 13. Steps 13+ (step files written one at a time)

| # | Step | Goal |
|---|---|---|
| 13 | v2 skeleton | New branch. Campaign, members and roles, auth, OpenAPI type generation, PWA shell with three tabs |
| 14 | Sessions + session notes | Composer, markdown, visibility, filters, back-posting, gap prompt, live over SignalR |
| 15 | Wiki + mentions | `@` composer, entries, articles, timeline, promote, secret blocks, aliases, merge, edit access |
| 16 | Images | S3 blob store, image notes, captions, galleries |
| 17 | ⌘K search | FTS plus trigram, visibility-aware, actions |
| 18 | Combat v2 | Simplified model, combatants from entries, per-combatant PlayersSee, combat card |
| 19 | Connections + loose ends | Evidence panel, graph page, loose ends in the wiki and on sessions |
| — | **MVP line** | |
| 20 | SRD reference | Bundled SRD 5.2 provider, stat-block card, + wiki with Stats |
| 21 | 5eTools index | Preprocessing script, search-only provider, deep links; delete the Bestiary branches |
| 22 | D&D Beyond link | Sheet URL on player characters, manual refresh of core stats |
| 23 | In-browser suggestions | Evaluate GLiNER vs Laya; lazy-loaded model, suggestions in loose ends and on notes, `Actor.Model` provenance, revert by model version |
| 24 | Discord import | Export-file importer, session grouping preview, author mapping, suggestions review |

---

## Superseded

Kept so the earlier thinking isn't re-derived.

- **Proposal 1 — one event stream per campaign.** Replaced by one stream per
  aggregate with inline projections: it cost stream contention and async
  projection lag, and ordering by session plus timestamp is enough at this scale.
- **Proposal 3 — `EntityCreated` / `ReferenceAdded` and `#` autocomplete.** Renamed
  to Entry and Mention, with `@` as the only trigger. Relation records are dropped
  in favour of co-mention evidence (§6).
- **Channels (Discord-style "post to a place").** Dropped: content is posted at a
  point in a session and integrated into the wiki. Filters replace channels.
- **"Journal" as the stream's name.** Dropped: the first tab is also where DMs
  manage the campaign, so it is called **Campaign**, and its unit is the
  **session note**.
- **Pinned notes as an entry summary.** Replaced by the editable **article** plus
  **promote**.
- **The old combat model** (PlannedCombat and stages, five character records,
  dice-array tiebreaks, a separate History list, Paused). Replaced by §8.
- **Open questions 1–9 in the old agenda.** All answered above. Question 9 (the
  Bestiary branches) is resolved by step 21.
