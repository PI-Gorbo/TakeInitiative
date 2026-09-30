# 24 — Discord import

## Goal

A DM brings a campaign's Discord history into the app as **session notes** (glossary §1:
**Import**, design §11a). They pick a **DiscordChatExporter JSON export** on their own device,
choose which channels, dates and authors to bring in, map Discord authors to members, and see a
**preview** of exactly which session every message lands in (the 3-day gap rule). On **Import**,
the confirmed messages are posted in batches as notes with their original dates, their images as
**image notes**, and a **Source** on each note linking back to the original message and naming its
original author. Before or after the import, the same in-browser model as step 23 offers
**suggested mentions to review**. Nothing is linked or created until the DM accepts it.

**The file never leaves the device.** Parsing, filtering, grouping and extraction run in the
browser. Only the notes the DM confirmed, and their images, are sent, and only to the app's own
API. There is no Discord bot, no Discord API call and no token anywhere in the app. Skipped
authors' words are never sent, not even as a reply quote.

"Running" at the end: a DM on a desktop exports `#notes`, `#maps` and `#recaps` with
DiscordChatExporter (JSON, with media) and opens **Import from Discord** on a fresh campaign. They
drop the export's zip. The preview reads "#notes 142 messages · #maps 23 images · #recaps 9
messages → Sessions 1–9", with authors "sam → Sam ✓ · priya → Priya ✓ · Avrae (bot) → Skip". They
tap "✨ Review suggestions" and accept "**Rellan** → @Rellan Ashvale · 14 messages" and "+ Create
**Greyhollow Keep** (Place) · 6 messages". They tap **Import**: "Posting 120 of 174 · Session 7",
with Cancel. The Campaign tab then shows Sessions 1–9 with their Discord dates, Priya's messages
bylined "Priya · from Discord", the maps as image notes, and the recaps under their dividers.
Importing the same zip again says "174 already imported · 0 to import".

The step ships as five PRs stacked with `gh stack` on top of this file's docs PR, which sits on
23e (#260):

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 24a | `v2/24a-discord-reader` | The export reader: parse, normalise, filter and group (web, pure) | The app unchanged for users. `utils/import/` reads an invented DiscordChatExporter fixture into messages, converts their text, filters them and groups them into sessions by the gap rule, all unit-tested | [ ] |
| 24b | `v2/24b-import-api` | Import sessions and note batches, idempotent, with Source (API) | 23's app unchanged in the browser. `POST imports/discord/sessions`, `POST imports/discord/notes`, `POST imports/discord/known` and `DELETE imports/discord/{importId}` work for DMs; notes carry `Source` and their original `PostedAt`; a message id is imported at most once per campaign | [ ] |
| 24c | `v2/24c-import-page` | The import page: pick, choose, map, preview, import text (web) | A DM imports a text-only export end to end, with progress, Cancel and resume; imported notes show "from Discord" | [ ] |
| 24d | `v2/24d-import-images` | Attachments as image notes (web) | The same flow imports an export's images (zip or folder) as image notes, within step 16's limits, and lists what it skipped | [ ] |
| 24e | `v2/24e-import-suggestions` | Suggested mentions to review, before and after (web) | "✨ Review suggestions" in the preview, grouped by span; accepted ones are applied after posting with step 23's provenance. The step's Verify passes | [ ] |

Discord's own data package, other sources (Slack, Google Docs), attachments other than images,
reactions, embeds, link previews, merging bursts of messages into one note, renumbering sessions
and an import by a Player are not in 24 (Notes, "Not in 24").

## Depends on

- **14**: sessions and notes: `PostSessionNote` (the shape a batch copies), `SessionNotePosted`
  (`AddedLater`, `AuthorMemberId`), `Session` (`Number`, `StartedAt` from the event timestamp,
  the unique `(CampaignId, Number)` index), `PostStartSession`, and `SessionGap`, whose doc
  comment already names step 24 as a caller. `utils/sessionDates.ts` for divider dates.
- **15**: mentions by id, `NewEntries` on `PUT notes/{id}`, `Actor`, note history.
- **16**: images: `POST images` (20 MB, type read from the bytes: JPEG, PNG, WebP, GIF;
  `MaxUnposted` 20 per member, 409 over it; unposted images swept after 24 hours),
  `ImageAttachments` and `NoteWrite` (attach in the note's transaction), `SessionNote.MaxImages`
  10, and `utils/images.ts` (`prepareImage`, `UPLOAD_TYPES`, `IMAGE_UPLOAD_MAX_BYTES`).
- **19**: loose ends, where unlinked imported notes land for their importer.
- **23**: the extractor (`useExtractor`, the worker, the device setting and download prompt),
  `utils/extraction/spans.ts`, `utils/suggestions.ts` (labels, merge, dismiss),
  `Suggestions/CreateFromSuggestion.vue`, `useAcceptSuggestion`, and `PUT notes/{id}`'s
  `suggestion` with `Actor.Model` provenance and revert. 23's Notes, "Seams for 24", leave the
  pre-import matching question to this step (answered in 24e: on the device).

These parts of [12-v2-design-session.md](12-v2-design-session.md) are binding: the glossary (§1:
**Import**, **Source**, **Suggestion**, **Session**, **Added later**, **Image note**, **Caption**),
§3's sessions and the 3-day gap, §9's provenance, §11a's "Discord import" in full, and the
invariants (§10), especially 3 (every note in exactly one session), 4 (only the author edits),
5 (visibility on the server), 9 (every event carries an Actor), 10 (no paid services) and 11
(mobile).

README lists 16 and 23.

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web), except where they start at the repo root.

What exists today: nothing reads a Discord file. `SessionGap` names step 24; `Entry.Source`
(`EntrySource(Provider, ExternalId, Url)`, step 20) is the shape a note's Source copies; notes
have no source and their `PostedAt` is always the event's timestamp.

**This PR (docs)**
- `docs/roadmap/24-discord-import.md`: this file
- `docs/roadmap/README.md`: link step 24, `in progress`

**24a**
- Web add `utils/import/{discordExport,messageText,importGroups,importFilters,types}.ts`,
  `workers/importReader.worker.ts`, `composables/useImportReader.ts`
- Web add `tests/fixtures/import/discord/{notes.json,maps.json,recaps.json,README.md}` (invented),
  `tests/unit/{discordExport,messageText,importGroups}.test.ts`
- Root add `scripts/import-fixture/build-discord-fixture.mjs` (generates the fixture)

**24b**
- API add `src/Features/Imports/{NoteSource,ImportKeys}.cs`,
  `src/Features/Imports/Api/{PostImportSessions,PostImportNotes,PostImportKnown,DeleteImport}/*.cs`
- API modify `src/Features/Sessions/Models/Events/{SessionStarted,SessionNotePosted}.cs` (optional
  `StartedAt`, `ImportKey`; optional `PostedAt`, `Source`), add `Events/SessionRedated.cs`;
  modify `Models/{Session,SessionNote}.cs` (projection, `Source`, the `SourceKey` index),
  `SessionHub.cs` (`notesImported`), `Api/*/SessionNoteResponse` (the `source`), the Marten
  registration (unique index on `(CampaignId, SourceKey)`)
- Tests add `Scopes/Integration/Features/Imports/{ImportSessionsTests,ImportNotesTests,ImportIdempotencyTests,ImportLeakTests,ImportUndoTests}.cs`
- Web `utils/api/schema.d.ts`: regenerated
- `docs/roadmap/12-v2-design-session.md`: glossary rows (**Original author**, **Import batch**),
  per the rule that a noun is in §1 before it is in code

**24c**
- Web add `pages/app/campaigns/[campaignId]/import.vue`,
  `components/Import/{PickExport,ChannelChoices,AuthorMapping,SessionPreview,ImportProgress,ExportHelp}.vue`,
  `composables/useImportRun.ts`, `utils/import/{authorMapping,importPlan,importBatches}.ts`,
  `utils/api/import/*.ts`
- Web modify `pages/app/campaigns/[campaignId]/index.vue` (the DM's "Import from Discord" entry),
  `utils/searchActions.ts` (a ⌘K action for DMs), `components/Session/SessionNoteCard.vue` (the
  "from Discord" byline and link), `composables/useCampaignHub.ts` (`notesImported`)
- Web add `tests/unit/{authorMapping,importPlan,importBatches}.test.ts`

**24d**
- Web add `utils/import/{exportFiles,attachments}.ts` (zip, folder and loose files; the
  attachment rules), `fflate` as a dependency, imported only in the reader worker
- Web modify `workers/importReader.worker.ts`, `composables/useImportRun.ts` (upload, then post),
  `components/Import/{PickExport,SessionPreview}.vue` (image counts, the skipped list)
- Web add `tests/unit/{exportFiles,importAttachments}.test.ts`

**24e**
- Web add `components/Import/{SuggestionReview,SuggestionGroup}.vue`,
  `utils/import/{localMatch,importSuggestions}.ts`, `composables/useImportSuggestions.ts`
- Web modify `composables/useImportRun.ts` (apply accepted suggestions after posting),
  `components/Import/SessionPreview.vue` (the ✨ line)
- Web add `tests/unit/{localMatch,importSuggestions}.test.ts`
- `docs/roadmap/24-discord-import.md`, `README.md`: tick and close the step

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/23e-suggestions-inline
gh stack add v2/24-discord-plan
```

Add each sub-step on top with `gh stack add v2/24a-discord-reader` and so on.

**Glossary check.** **Import** and **Source** are in §1. New nouns, added to §1 in 24b before
they reach code:
- **Original author**: on an imported note, the name it had where it came from (a Discord
  user), and the member it was mapped to, if any. The note's **author** is still the member who
  imported it (§11a). Not "poster", "sender".
- **Import batch**: up to 50 imported notes posted in one request. Not "chunk", "page".

The UI says "Import from Discord", "messages" only for what is in the file (they are not notes
yet), and "notes" once posted. Discord "channels" appear only as the file's channels, never as a
place in the app (§12: no channels).

### 24a. The export reader (web, pure)

1. **The format: DiscordChatExporter JSON.** DiscordChatExporter (DCE, MIT) is the usual way
   to export a Discord channel. Its JSON export (`-f Json`, or "JSON" in the GUI) has every
   member's messages with ids, timestamps, authors and attachments; with **media** (`--media`,
   "Download assets") it saves the attachments next to the JSON in `<file>_Files/` and writes
   their relative paths into the JSON. One file is one channel (or one thread, or one partition
   of a channel with `--partition`). The shape the reader relies on:

   ```jsonc
   { "guild":   { "id": "…", "name": "…" },
     "channel": { "id": "…", "name": "notes", "type": "GuildTextChat", "category": "…" },
     "messages": [ {
       "id": "1204…", "type": "Default" /* or "Reply", "ChannelPinnedMessage", … */,
       "timestamp": "2025-03-01T19:42:10.123+00:00", "timestampEdited": null,
       "content": "We met Rellan on the road…",
       "author": { "id": "…", "name": "priya", "nickname": "Priya", "isBot": false },
       "attachments": [ { "id": "…", "url": "notes.json_Files/map-1A2B.png", "fileName": "map.png", "fileSizeBytes": 812345 } ],
       "embeds": [], "stickers": [], "reactions": [], "mentions": [],
       "reference": { "messageId": "…", "channelId": "…", "guildId": "…" } } ] }
   ```

   The reader reads only the fields above. It never reads avatars, roles, colours, the users
   who reacted, or the `mentions` list, and never fetches anything. **Discord's own data
   package** (Settings → Data & Privacy) is not supported: it holds only the requester's own
   messages, so it cannot rebuild a table's history (Notes).
2. **The fixture** (`tests/fixtures/import/discord/`): three invented channel files in DCE's
   shape (`notes.json` ~140 messages over nine sittings, `maps.json` with image attachments and
   a 30 MB one and a PDF, `recaps.json`), made by `scripts/import-fixture/build-discord-fixture.mjs`
   and never edited by hand, with a README saying they are invented. They cover replies, an
   edited message, a pinned-message notice, a join notice, a bot with an embed, a sticker-only
   message, custom emoji, `||spoilers||`, a user mention, a message at a DST change, two
   authors with the same nickname, and a thread file. **Checked against a real export in 24a**:
   the builder may add fields a real file has; any shape the fixture lacks goes into it.
3. **Reading** (`discordExport.ts`, run in `workers/importReader.worker.ts` so a large file does
   not block the page): `JSON.parse` of each file (at most 100 MB a file, else "Split the export
   with DCE's `--partition`"); anything not DCE-shaped is refused with "This isn't a
   DiscordChatExporter JSON file." Out comes, per message:

   ```ts
   type ImportMessage = {
     id: string; channelId: string; guildId: string; channelName: string;
     at: string;             // ISO, from `timestamp`
     authorId: string; authorName: string; isBot: boolean; // nickname ?? name
     text: string;           // after messageText (below)
     replyTo?: string;       // reference.messageId, for Reply messages
     attachments: { fileName: string; path: string; bytes: number }[];
     dropped: ("embed" | "sticker" | "reactions")[]; // shown as counts in the preview
   };
   ```

   Only `Default` and `Reply` messages are read; system messages (joins, pins, thread created,
   calls, boosts) are skipped and counted.
4. **Text** (`messageText.ts`, pure):
   - The final content is imported; edits are not replayed and the note is not "(edited)".
   - Markdown stays markdown (bold, italic, lists, links, code). DCE already writes user,
     channel and role mentions as `@name`/`#name` text, and custom emoji as `:name:`; they stay
     plain text and never become our mentions.
   - Discord-only syntax is left as it is: `||spoiler||` stays literal (there is no inline
     spoiler in notes, and a real secret belongs in a `DM` note), `__underline__` renders bold.
   - A literal `@[…](entry:…)` in a message is escaped (`\@`), so no imported text can forge a
     mention.
   - **Replies**: when the replied-to message is also imported (its author is not skipped),
     the note starts with a one-line quote `> ↪ **Priya:** first 80 characters…`. Otherwise
     the reply marker is dropped: a skipped author's words are never quoted.
   - Text over 10,000 characters (only possible by joining, which 24 does not do) is refused
     by the reader, never cut.
   - A message with no text after this, no images and nothing else is skipped as empty.
5. **Filters** (`importFilters.ts`): channels (each with a role, below), a date range (from/to,
   inclusive, in the member's time zone), and authors (imported or skipped). Bots are skipped by
   default. The gap rule is applied **after** filtering, over the imported messages of all
   chosen channels merged in time order, so `#maps` and `#recaps` land in the same sessions as
   `#notes`.
6. **Grouping** (`importGroups.ts`): the gap rule is `SessionGap`'s: a message more than 3
   days after the one before it starts a new group. Each group has its first and last time, its
   message and image counts, its first line, and a stable **key**: `discord:{first message id}`.
   A member can **merge** a group into the previous one and **split** a group before any
   message; a split or merge changes only the keys of the groups it touches.
7. **Channel roles.** Each channel is **Notes**, **Recaps** (every note is posted with
   `IsRecap`) or **Skip**. A channel whose name contains "recap" defaults to Recaps; every other
   channel to Notes. Images are imported from any channel as image notes (24d), so a `#maps`
   channel needs no role of its own.
8. **Tests**: parsing the fixture (counts, system messages skipped, the refusal), each text
   rule, the reply rule with a skipped author, the escape, filters, the gap rule across
   channels and time zones (a DST change must not move a boundary), merge and split keys.

### 24b. Import sessions and note batches (API)

All four endpoints are **DM only** (403 otherwise) and live in `src/Features/Imports/Api/`. An
import is a client-side run with an `importId` (a GUID the page makes); the server keeps no
import record beyond what is on the notes and sessions.

1. **The events**, each with optional fields so old events read as before:
   - `SessionStarted` gains `DateTimeOffset? StartedAt` and `string? ImportKey`. The projection
     uses `StartedAt ?? event.Timestamp`, so an imported session's divider shows its Discord
     date.
   - `SessionNotePosted` gains `DateTimeOffset? PostedAt` and `NoteSource? Source`, where
     `NoteSource(string Provider, string ExternalId, string Url, Guid ImportId,
     OriginalAuthor Author)` and `OriginalAuthor(string Name, Guid? MemberId)`. `Provider` is
     `"discord"`, `ExternalId` the message id, `Url`
     `https://discord.com/channels/{guild}/{channel}/{message}`. The projection sets
     `PostedAt = e.PostedAt ?? event.Timestamp`, keeps `Source`, and a flat
     `SourceKey = "{provider}:{externalId}"`.
   - New `SessionRedated(Actor, DateTimeOffset StartedAt)`: only for the empty Session 1 case
     (3).
   - The Actor on all of them is the importing DM (`Actor.Member`), invariant 9.
2. **Idempotency**: a unique Marten index on `SessionNote (CampaignId, SourceKey)` where
   `SourceKey` is not null. A message id is imported at most once per campaign. A deleted note's
   document is gone, so deleting an imported note and importing again brings it back (that is
   how a DM undoes a single delete).
3. **`POST /api/campaigns/{CampaignId}/imports/discord/sessions`**
   `{ importId, sessions: [ { key, startedAt, title? } ] }`, in time order, at most 200:
   - A key already on a session in this campaign returns that session (resume).
   - New sessions are started after the current one, numbered in order, with `StartedAt` and
     `ImportKey`. A new session's `startedAt` must not be before the current session's
     `StartedAt` (409 "These messages are older than Session 12. Pick an existing session for
     them."), so numbers and dates never disagree. Older history goes into existing sessions
     (24c's defaults), never renumbered ones.
   - **The empty campaign**: when the campaign's only session is Session 1 with no notes and no
     combats, the first new session is Session 1 itself, re-dated with `SessionRedated` and
     given the key. So a fresh campaign imports as Sessions 1–9, not 2–10.
   - Answer: `{ sessions: [ { key, sessionId, number } ] }`. One `sessionStarted` push per new
     session, as today.
4. **`POST /api/campaigns/{CampaignId}/imports/discord/notes`**, one **import batch**:

   ```jsonc
   { "importId": "…", "notes": [ {
       "messageId": "1204…", "channelId": "…", "guildId": "…",
       "sessionId": "…", "postedAt": "2025-03-01T19:42:10.123Z",
       "text": "…", "visibility": "Everyone", "isRecap": false,
       "author": { "name": "Priya", "memberId": "…" /* or null */ },
       "imageIds": [ "…" ] } ] }
   ```

   - At most 50 notes; each validated as `PostSessionNote` is (text rule, 10 images,
     `ImageIdsList`), plus: ids are Discord snowflakes (digits, ≤ 20), `postedAt` not in the
     future, `author.name` 1–80 characters, `author.memberId` a member of this campaign or null,
     the session in this campaign.
   - One transaction (`NoteWrite`), notes appended in `postedAt` order. `AuthorMemberId` is the
     caller (§11a: posted as the importing member). `AddedLater` is **false**: the Source marks
     the note as imported instead. Images attach through `ImageAttachments` exactly as
     `PostSessionNote` does.
   - A message id already imported is skipped, not an error. Answer:
     `{ posted: [ { messageId, noteId } ], skipped: [ { messageId, noteId } ] }`.
   - No `newEntries`: suggestions are applied afterwards through 23's `PUT` (24e).
   - **One push per batch**, not per note: `notesImported { sessionIds }` to the campaign group,
     with no content, so it cannot leak; clients refetch those sessions' streams (visibility is
     then applied by the normal reads). 50 note pushes per batch would flood every open client.
5. **`POST /api/campaigns/{CampaignId}/imports/discord/known`** `{ messageIds: [≤ 1000] }` →
   `{ known: [messageId] }`: which of these are already imported here, under any visibility. It
   returns ids only, never note ids or text; the DM already holds the file.
6. **`DELETE /api/campaigns/{CampaignId}/imports/discord/{importId}`**: undo a run. Deletes the
   **caller's** notes whose `Source.ImportId` is that id (they are the author, invariant 4), with
   their images, as `DeleteSessionNote` does. Sessions the run started are deleted only if they
   are now empty and still the latest, from the top down, so numbering stays gap-free; others
   stay. Answer: `{ notes, sessions }`. A re-dated Session 1 keeps its date.
7. **Reads.** `SessionNoteResponse` gains `source`: `{ provider, url, author: { name, memberId } }`
   or null. It is visible to whoever can see the note (it is part of the note). Search, loose
   ends, timelines, connections and ⌘K need nothing new: imported notes are notes.
8. **Tests.**
   - `ImportSessionsTests`: numbering, a key returned again, the 409 for an older date, the
     empty Session 1 re-dated, a Session 1 with a note not re-dated, Player 403.
   - `ImportNotesTests`: `PostedAt` and `Source` stored, `AddedLater` false, recap flag, images
     attached, validation, the stream ordered by the original times, one push per batch.
   - `ImportIdempotencyTests`: the same batch twice, two batches racing on one message id (the
     index decides), delete then re-import.
   - `ImportLeakTests`: a `DM` and a `Me` imported note never reach a player's stream, search,
     timeline or push; `known` returns ids only.
   - `ImportUndoTests`: only the caller's notes of that run go; empty latest sessions go, a
     session with another note stays.

### 24c. The import page: text (web)

1. **Where.** `pages/app/campaigns/[campaignId]/import.vue`, for DMs (a Player is sent back to
   the Campaign tab). Reached from the Campaign tab's DM actions (next to Members) as "Import
   from Discord", and from ⌘K ("Import from Discord", DM only). One column, mobile-first; each
   stage is a section with a step heading, so a phone scrolls through it.
2. **Pick** (`Import/PickExport.vue`): a drop zone and a file button taking one or more `.json`
   files (24d adds a zip and a folder). `Import/ExportHelp.vue` explains, in a few lines, how to
   make the export with DiscordChatExporter (JSON, with media, one file per channel), that the
   file is read on this device and never uploaded, and links to DCE's own guide. The reader
   runs in the worker with a progress line; errors name the file.
3. **Choose** (`Import/ChannelChoices.vue`): per channel file, its name, message count and date
   span; its role (Notes · Recaps · Skip) and its **visibility** (Everyone · DM · Me; default
   Everyone, §11a, since the messages were already shared in that channel). One date range for
   all.
4. **Authors** (`Import/AuthorMapping.vue`, `utils/import/authorMapping.ts`): each Discord
   author with their message count and a picker: a member, or **Skip**.
   - Prefilled only on an exact, case- and accent-folded match of the Discord name or nickname
     with a member's username; bots default to Skip; everyone else defaults to **Skip** until
     mapped (§11a: unmapped authors are skipped).
   - A mapped author's notes carry `author: { name: <Discord display name>, memberId }`. Several
     Discord accounts may map to one member.
   - The mapping is remembered on this device per campaign (`localStorage`,
     `ti.import.discord.authors.{campaignId}`: Discord user id → member id), so a resume or a
     second channel does not ask again. Discord user ids are not sent to the API.
5. **Sessions preview** (`Import/SessionPreview.vue`, `utils/import/importPlan.ts`): one row
   per group: its dates in the member's time zone, "34 messages · 3 images · 1 recap", its
   first line, and its **target**:
   - **An empty campaign** (no notes anywhere): groups become Sessions 1…N.
   - **Otherwise**, a group newer than the current session's start becomes a new session
     numbered after the current one; an older group defaults to the **existing session that was
     current at its first message** (the last session started before it; Session 1 if none),
     and its notes interleave there by their original times. The preview says "Goes into
     Session 4 (existing)" and the member can pick another existing session or, for a group
     newer than the current session, "New session". It never offers an older new session (24b's
     409).
   - Merge with previous, split before a message (opens the group's messages), and a title per
     new session (optional, empty by default).
   - Counts at the top, as §11a's sketch: "#notes 142 messages → Sessions 1–9 (grouped by the
     3-day gap rule) · #recaps 9 → flagged as recaps · Authors: … · Skipped: 12 system messages,
     4 bot messages, 3 embeds, 2 stickers".
   - **Already imported**: before the preview shows, the page asks `known` (message ids in
     1000s) and marks those messages; the header reads "174 already imported · 38 to import".
6. **Importing** (`composables/useImportRun.ts`, `utils/import/importBatches.ts`,
   `Import/ImportProgress.vue`):
   - One `importId` per run. First `imports/discord/sessions` for the new sessions (in 200s),
     then batches of 50 notes in time order, each after the last succeeds.
   - Progress: "Posting 120 of 174 · Session 7". **Cancel** stops after the batch in flight;
     what is posted stays, and the page says so with an **Undo this import** button (24b.6)
     and a **Resume** button.
   - An error on a batch retries it twice with backoff, then stops with the error and Retry.
     A 409 or 403 stops the run.
   - **Resume** is stateless: pick the same file(s) again (the page remembers their names and
     the choices in `localStorage` per campaign, never message contents); `known` marks what is
     in, and only the rest is posted. The choices can be changed before resuming.
   - While running: a screen wake lock where supported, and a `beforeunload` warning.
   - When done: "Imported 174 notes into Sessions 1–9 · Open Session 1", with Undo.
7. **Imported notes in the stream** (`SessionNoteCard.vue`): an imported note's byline is its
   **original author**: the mapped member's name, or the Discord name when there is no member
   (not possible in 24's defaults, but old data may have it), followed by a small "from Discord"
   that links to the message (`source.url`, new tab). The card's detail and history say
   "Imported by Sam". Edit and delete follow the real author (the importer), as always. The
   Mine filter is the importer's, since they are the author.
8. **Live**: `useCampaignHub` handles `notesImported` by invalidating the named sessions'
   stream queries and the loose-ends counts.
9. **Tests**: author defaults and folding, the plan's targets (empty campaign, older group into
   the covering session, newer into new sessions, never an older new session), batch building
   (50 a batch, time order, known ids left out), resume choices round-trip.

### 24d. Attachments as image notes (web)

1. **Getting the files** (`utils/import/exportFiles.ts`): the pick step also takes
   - a **zip** of the export (the JSON files plus their `_Files` folders), read with `fflate`
     in the worker, entry by entry, so a large zip is not held twice. This is what a phone uses.
   - a **folder** (`webkitdirectory`, desktop browsers), or the JSON files and their media
     files picked together.
   An attachment is found by its relative `url` in the JSON. Its bytes are read only when it is
   about to be uploaded.
2. **What is imported** (`attachments.ts`, pure):
   - Images whose bytes are JPEG, PNG, WebP or GIF (the API's `UPLOAD_TYPES`), at most 20 MB,
     run through 16c's `prepareImage` (the long-edge redraw) like the composer's.
   - Everything else is skipped and listed: other types ("12 files are not images: 3 PDFs, 9
     audio"), too large, missing from the export (exported without media: "23 images are not in
     this export. Export again with media to bring them in."). The note keeps a line
     `📎 map.pdf (not imported)` per skipped file so the text still says there was one.
   - **Never fetched from Discord.** An attachment `url` that is an `https://cdn.discordapp.com/…`
     link (an export without media) is never requested: it would tell Discord who is importing,
     and those links expire anyway.
   - A message with images and no text becomes an image note with an empty caption (a loose end
     for the importer, 16c and 19's rule). A message with more than 10 images (not possible on
     Discord today) keeps the first 10 and lists the rest.
3. **Uploading within step 16's limits** (`useImportRun`): images are uploaded one at a time
   with `POST images` just before the batch that uses them, so a batch never holds more than
   the unposted cap: a batch is cut short when its images would pass 20, counting the member's
   own unposted images (a 409 from `POST images` means "post what is uploaded, then continue").
   Unposted leftovers of a cancelled run are swept after 24 hours as usual.
4. **Preview**: each group shows its image count and thumbnails of its first three images
   (object URLs from the zip, revoked when the page closes); a "Skipped files" list at the end.
5. **Tests**: finding attachments in a zip, in a folder list and in loose files; the type,
   size and missing rules; the note's `📎` lines; batch cutting at the cap; no request is ever
   built for a `cdn.discordapp.com` URL.

### 24e. Suggested mentions to review (web)

1. **Where.** In the preview, when the device setting is not Off: a "✨ Review suggestions"
   section after the sessions. It uses 23's `useExtractor` exactly as the loose-ends page does:
   the download prompt when the model is not on this device, never a download on a metered
   connection without a tap. With the setting Off, the section says "Suggestions are off on this
   device" with a link to the Me page, and the import works without it.
2. **Extracting** (`useImportSuggestions.ts`): the selected messages with text, oldest first,
   one at a time in the worker, masked as 23b does. Progress: "✨ Reading 412 of 1,612
   messages · about 9 minutes left" (from the measured rate), with **Stop and review what's
   found** and Cancel. Results are memoised in memory by message id and text, so changing the
   filters does not re-read a message. Before starting on more than 300 messages the section
   says how long it will take on this device and that suggestions can also be found after the
   import, on the loose-ends page (23d).
3. **Matching on the device** (`utils/import/localMatch.ts`), answering 23's "Seams for 24":
   the messages are not in the API, so their spans are **not** sent to `POST suggestions/match`.
   They are matched against the wiki the DM already has (`useEntryDirectory`: the entries the
   DM can see, with aliases), by a TypeScript port of the entry matcher's categories (exact,
   prefix, word prefix, substring, trigram similarity ≥ 0.6 on `lower(unaccent(…))`, with
   pg_trgm's padding) and `LinkSpans.Accepts` (whole words for prefixes, never a substring).
   The port is tested against the same cases as `SuggestionMatchTests`, so the device and the
   server agree on those. Merged entries are not in the directory.
4. **The review** (`SuggestionReview.vue`, `SuggestionGroup.vue`): suggestions are grouped by
   folded span text, not by message, so 1,600 messages stay reviewable: "✨ **Rellan** →
   @Rellan Ashvale · 14 messages", "✨ **Greyhollow Keep** looks like a Place · 6 messages".
   Each group: **Accept all**, **Pick messages** (the messages with the span highlighted, each
   with a checkbox), **Link to another entry** (the `@` picker) and ✕. A create group opens
   23d's `CreateFromSuggestion` (name, kind chips with the model's kind, "Visible to" read-only
   from the most visible note it will be created from). **Nothing is accepted by default**, and
   the header reads "✨ 61 suggestions · 0 accepted". A span over a `DM` entry in an `Everyone`
   channel shows 15's warning ("This entry is hidden from players"); accepting it reveals
   nothing (invariant 5). Dismissals use 23d's list.
5. **Applying, after posting.** Accepted suggestions are applied once the notes exist, with
   23c's `PUT notes/{id}` and its `suggestion` (one span per `PUT`, the model on the Actor), so
   provenance, the ✨ history line and **revert by model version** work unchanged, and no new
   API is needed. Per note, spans are applied one at a time from the end of the text backwards,
   so earlier offsets stay right. A new entry is created by the first `PUT` that uses it (with
   `newEntries`), starting with its most visible note so the entry's visibility is the widest it
   needs; later notes link its id. Progress: "Linking 40 of 61 mentions". Suggestions are
   applied only to notes posted in this run (the batch answers give their note ids); messages
   imported by an earlier run are left to loose ends (`known` returns no note ids). These
   `PUT`s mark notes "(edited)", as any accepted suggestion does.
6. **After the import**, every unlinked imported note is a loose end of its importer, so 23d's
   "✨ Find suggestions" and 23e's chips work on them as on any of the DM's own notes. Accepted
   choices not applied (a cancelled run) are not kept; they can be found again there.
7. **Tests**: the local matcher against `SuggestionMatchTests`' cases (order, alias, the
   substring refusal, accents), grouping by folded span, the per-note apply order, the
   create-first ordering by visibility, nothing accepted by default.
8. **Close the step**: tick this file's PR table and set README's status to `done`.

## Verify

1. `dotnet test`, `pnpm test`, `npx nuxi typecheck` and `pnpm build` pass, `schema.d.ts` is
   fresh, and CI is green on every PR. The fixture is invented; CI never reads a real export or
   downloads a model.
2. **The file stays on the device.** With DevTools' Network panel open through a whole import,
   the only requests are the app's own: `known`, `sessions`, `notes` batches, `POST images`
   for imported images, the model files (24e, from the app's origin) and the `PUT notes/{id}`
   for accepted suggestions. No request carries a skipped author's text, a Discord user id, or
   goes to `discord.com`, `cdn.discordapp.com` or any other host. No `POST suggestions/match`
   is made before the import.
3. **A fresh campaign, desktop (a DM at 1280 × 800).** Export a small real server (or use the
   fixture zip) with `#notes`, `#maps`, `#recaps`, a bot and a member who is not in the
   campaign.
   1. The preview shows the counts, Sessions 1–N by the gap rule, recaps flagged, the bot and
      the non-member skipped, the PDF and the 30 MB image listed as skipped.
   2. Map the authors, set `#recaps` to Recaps, import. The stream shows Session 1 re-dated,
      each session's divider dated from Discord, notes in their original order and times,
      "Priya · from Discord" linking to the message, no "added later" marker, images as image
      notes, recaps under their dividers.
   3. Import the same export again: "all already imported · 0 to import".
   4. Cancel a run halfway; Resume after re-picking the file posts only the rest. Undo this
      import deletes that run's notes and the empty sessions it started.
4. **An active campaign.** With Sessions 1–12 played in the app, import older history: every
   group defaults into an existing session and interleaves by time; no session 13 appears and no
   number changes. Import newer messages: they become Session 13 and on.
5. **Visibility (a Player at 390 × 844 in another profile).** A channel imported as `DM` is
   absent from the Player's stream, search, timelines and live updates; `Everyone` notes appear
   live after each batch without a reload.
6. **Suggestions.** With the model on the DM's device: the review lists groups, nothing is
   accepted by default; accept one match group and one create; after the import those notes
   show the mentions, their history shows "✨ suggested by gliner_small-v2.5 (…)", and the Me
   page's Accepted suggestions reverts them.
7. **A phone (the DM at 390 × 844).** Pick a zip from Files, map authors, preview and import
   by touch; nothing sits under the keyboard or the home indicator; the tab survives a
   2,000-message file.

## Notes / gotchas

### Why DiscordChatExporter JSON

- It is the de facto export tool (MIT, maintained, CLI and GUI), exports **every** member's
  messages in a channel with stable message ids and ISO timestamps, and with media saves the
  attachments beside the JSON, so an import needs nothing from Discord at run time.
- **Discord's data package** holds only the requester's own messages (and attachment links,
  not files), so it cannot rebuild a table's history. Supporting it is additive later: another
  reader producing `ImportMessage`.
- **No bot, no API, no token in the app** (§11a). How a member runs DCE, and with which token,
  is between them and Discord; the help text points at DCE's own guide and says the app never
  asks for a token.
- DCE's HTML, TXT and CSV formats are refused: JSON is the only one with ids for idempotency
  and attachment paths.

### Why parse in the browser

- **Privacy.** An export holds other people's messages and personal data (names, ids,
  avatars, everything said in the channel). Reading it on the device means only what the DM
  confirmed reaches the server: the chosen channels, dates and authors, as notes. Skipped
  authors' messages, Discord user ids, avatars and reactions never leave the device, and nothing
  goes to a third party (no Discord CDN, no model host by default).
- **Suggestions are in the browser anyway** (23), and they need the text before import.
- **The server stays simple.** No upload of a large file, no temporary storage of other
  people's data, no parsing of untrusted JSON on the server; the batch endpoints validate small
  notes like `PostSessionNote` does.
- **The cost** is memory on the device (a 100 MB JSON file is parsed whole in the worker) and
  that a run must keep the tab open. Batching, resume and the per-file limit cover it.

### Mapping decisions

- **Posted as the importing DM, bylined as the original author** (§11a). Posting as Priya
  would make her the author of notes she never wrote in the app and could not have refused;
  the importer is responsible for them and can edit, delete or undo them (invariant 4).
- **Who can import: DMs only.** An import writes many notes naming other members as original
  authors; that is a campaign-management act (like members and roles), so a Player cannot.
- **Visibility** defaults to `Everyone` per channel (§11a), and a channel can be `DM` or `Me`.
- **One message, one note.** It keeps the Source 1:1 (idempotency, the link back, undo).
  Joining bursts of short messages by one author into one note is a later option.
- **Sessions are never renumbered.** Older history goes into the existing session that was
  current then; only newer groups start sessions. Inserting sessions before existing ones would
  change every `/session 11` and every divider a member remembers.
- **What is dropped**: reactions, embeds (link previews and bot cards; the link itself stays in
  the text), stickers, system messages, edit history, pins. Replies keep a one-line quote only
  when the replied-to author is imported. Threads are imported as their own channel files.
- **Idempotency** is by message id per campaign, enforced by a unique index, so two runs or two
  DMs cannot duplicate a message. Deleting an imported note makes it importable again.

### Seams and limits

- **The unposted image cap (16a: 20 per member)** is why uploads interleave with batches.
  Raising it for imports would need a separate cap; not needed at 50 notes a batch.
- **`PostedAt` is now not always the event time.** Anything that reads "when was this posted"
  (the gap prompt, stream order, "edited" times) reads `PostedAt`, which is the original time for
  imported notes. The gap prompt therefore sees an import of old history as old and may suggest
  a new session; that is correct.
- **Loose ends.** Every unlinked imported note is the importer's loose end, which can be
  hundreds. 19's counts are per viewer's own notes, so only the importer sees them. 24e's review
  is there to bring that number down before it happens.

### Not in 24

Discord's data package and other sources; fetching anything from Discord; attachments other
than images; embeds, reactions and stickers; joining messages into one note; renumbering or
inserting sessions; imports by Players; a server-side parser or upload of the file; matching
unsent spans on the server; keeping accepted-but-unapplied suggestions across a cancelled run;
handing imported notes over to the mapped member as author.

### Decisions for the user

Each has the default this plan uses.

1. **The export format.** *Default: DiscordChatExporter JSON (with media, as a zip or folder);
   Discord's data package later if wanted.*
2. **Who can import.** *Default: DMs only.*
3. **Author on imported notes.** *Default: the importing DM is the author (edits, deletes,
   loose ends, suggestions are theirs); the byline shows the original author, mapped to a
   member. Alternative: hand each note to its mapped member, which makes them author of text
   they never posted.*
4. **Unmapped authors.** *Default: skipped (§11a). Alternative: import them under their
   Discord name with no member, which sends a non-member's words to the server.*
5. **Older history in an active campaign.** *Default: into the existing session current at the
   time, never renumbering.*
6. **One message, one note.** *Default: yes; joining bursts is later.*
7. **Undo.** *Default: "Undo this import" deletes the importer's notes from that run and the
   empty sessions it started at the top.*
8. **Suggestions before import.** *Default: matched on the device against the DM's wiki, never
   sent to `match`; applied after posting with 23's provenance; nothing accepted by default.*
