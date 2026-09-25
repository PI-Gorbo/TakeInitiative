# 15 — Wiki and mentions

## Goal

The Wiki tab becomes the campaign's **wiki**: every entry the viewer can see, by
kind, sorted by how often or how recently it is mentioned. Typing `@` in the
composer links an **entry**, or creates one that inherits the note's visibility.
Every **mention** puts the note on that entry's read-only **timeline**. An entry's
**article** is curated by editing it and by **promoting** text from session notes,
and it can hold **secret blocks** that people outside their visibility never
receive. Entries have **aliases**, can be **merged**, follow their **edit access**,
and a member can **claim** a Character entry as their player character, with
optional **stats**. Visibility is enforced on the server for every read and push,
down to the secret blocks inside an article (invariant 5).

The step ships as seven PRs stacked with `gh stack` on top of this file's docs
PR, which sits on 14e (#205). Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 15a | `v2/15a-entry-model` | Entry model and push | 14's app unchanged in the browser. The API creates, reads and edits entries, and pushes them only to who can see them | [x] |
| 15b | `v2/15b-mention-index` | Mentions and timeline (API) | The same. Notes can mention and create entries, and the API serves timelines and mention counts | [ ] |
| 15c | `v2/15c-wiki-pages` | Wiki home and entry page | The Wiki tab lists entries. An entry page shows its header and timeline, mentions in notes are chips, and "Add a note about X" works | [ ] |
| 15d | `v2/15d-mention-composer` | `@` in the composer | `@` links or creates entries from the composer: a popover on desktop, the mention strip on a phone | [ ] |
| 15e | `v2/15e-articles-api` | Articles, secret blocks, promote (API) | The same in the browser. The API stores articles as blocks, redacts secret blocks per viewer and promotes quotes | [ ] |
| 15f | `v2/15f-article-editor` | Article editor and promote (web) | Articles are shown and edited, with 🔒 and `@`. Promote works from the stream, the timeline and the long-press sheet | [ ] |
| 15g | `v2/15g-merge-claim-stats` | Merge, claim, stats, history | The step's Verify passes | [ ] |

Images (galleries) are step 16, ⌘K search is step 17, combats on an entry are step
18, and connections and loose ends are step 19. This step leaves a seam for each
(Notes) and builds none of them.

## Depends on

**14**. The glossary (§1), the composer's mentions (§3), the mobile mention strip
(§3a), the wiki and entries (§4), the architecture (§9), the invariants (§10) and
the post-MVP seams (§11) in [12-v2-design-session.md](12-v2-design-session.md) are
binding.

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web).

**This PR (docs)**
- `docs/roadmap/12-v2-design-session.md` §1: the new nouns (step 0 below)
- `docs/roadmap/README.md`: link step 15, `in progress`

**15a: add**
- API `src/Features/Campaigns/Models/Audience.cs`: `Audience.Of(visibility, ownerMemberId)`, `Contains(viewer)` and `Groups(campaignId)`. The one shape of "Everyone / DM plus X / only X", shared by notes, entries and blocks
- API `src/Features/Entries/Models/{Entry,EntryKind,EditAccess,EntryVisibility,EntryPermissions}.cs`
- API `src/Features/Entries/Models/Events/{EntryCreated,EntryRenamed,EntryKindChanged,EntryAliasAdded,EntryAliasRemoved,EntryVisibilityChanged,EntryEditAccessChanged}.cs`
- API `src/Features/Entries/EntryAccess.cs` (`RequireVisibleEntry`, `RequireCanEdit`, `RequireCreatorOrDm`), `src/Features/Entries/EntryHub.cs` (`EntryAudience` and the notify extensions)
- API `src/Features/Entries/Api/{GetEntries,PostEntry,GetEntry,PutEntryName,PutEntryKind,PutEntryAliases,PutEntryVisibility,PutEntryEditAccess}/**`
- Tests `Scopes/Integration/Features/Entries/{EntryTests,EntryVisibilityTests,EntryHubTests}.cs`, `Scopes/Unit/EntryAudienceTests.cs`

**15a: modify**
- API `src/boostrap/Bootstrap.cs`: register the `Entry` projection and its indexes
- API `src/Features/Campaigns/CampaignHub.cs`: the entry message names in `CampaignHubMessages`
- API `GlobalUsings.cs`: the `Entries` namespace
- Tests `Scopes/Integration/WebAppClientExtensions.cs`: typed calls for the new endpoints
- Web `utils/api/schema.d.ts`: regenerated

**15b**
- API add `src/Features/Entries/Mentions/{MentionParser,MentionIndex,NewEntries}.cs`
- API add `src/Features/Entries/Api/GetEntryTimeline/**`
- API modify `src/Features/Sessions/Models/SessionNote.cs` (`MentionedEntryIds`), `Bootstrap.cs` (its GIN index)
- API modify `src/Features/Sessions/Api/{PostSessionNote,PutSessionNote}/*.cs`: `newEntries[]`
- API modify `src/Features/Entries/Api/GetEntries/GetEntries.cs`: per-viewer mention counts
- Tests add `Scopes/Unit/MentionParserTests.cs`, `Scopes/Integration/Features/Entries/{MentionTests,TimelineTests}.cs`
- Tests add `Fixtures/mentions.json`: the parser cases, shared with the web's tests
- Web `utils/api/schema.d.ts`: regenerated

**15c**
- Web add `utils/api/entry/*.ts`; modify `composables/useApi.ts`, `utils/api/types.ts`
- Web add `utils/entries.ts` (the entry directory: resolving an id through merges, permissions, kind labels and icons, sorting), `utils/entryCache.ts`, `utils/queries/entries.ts`
- Web add `components/Wiki/{EntryList,EntryListItem,KindFilter,EntryHeader,EntryHeaderEditor,AliasEditor,EntryTimeline,NewEntryDialog}.vue`
- Web move `pages/app/campaigns/[campaignId]/wiki.vue` to `wiki/index.vue`; add `wiki/[entryId].vue`
- Web modify `utils/markdown.ts` (the mention chip), `components/Session/NoteMarkdown.vue`, `composables/useCampaignHub.ts`, `layouts/campaign.vue` (the Wiki tab stays current on an entry page)
- Web modify `components/Composer/Composer.vue` and `pages/app/campaigns/[campaignId]/index.vue`: `?about={entryId}`
- Web add `tests/unit/{entries,entryCache,mentionMarkdown}.test.ts`

**15d**
- Web add `utils/mentions.ts`: the composer's mention text (`@[text]` plus a link map) to and from the stored form, the active `@` query, matching, and the reveal check
- Web add `components/Composer/{MentionStrip,KindChips,RevealDialog}.vue`
- Web modify `components/Composer/Composer.vue`, `utils/composer.ts` (state, draft, POST body), `components/Session/NoteEditor.vue`, `utils/queries/sessions.ts`
- Web add `tests/unit/mentions.test.ts`

**15e**
- API add `src/Features/Entries/Models/{Article,ArticleBlock,ArticleMerge,ArticleEtag}.cs`
- API add `src/Features/Entries/Models/Events/{EntryArticleEdited,EntryQuotePromoted}.cs`
- API add `src/Features/Entries/Api/{PutEntryArticle,PostEntryQuote}/**`
- API modify `Entry.cs` (the article, `ArticleMentionIds`), `EntryVisibility.cs` (`CanSeeBlock`), `EntryHub.cs` (`entryArticleChanged`), `GetEntry` (the redacted article), `MentionIndex.cs`
- Tests add `Scopes/Unit/{ArticleMergeTests,ArticleEtagTests}.cs`, `Scopes/Integration/Features/Entries/{ArticleTests,SecretBlockTests,PromoteTests}.cs`
- Web `utils/api/schema.d.ts`: regenerated

**15f**
- Web add `utils/article.ts` (blocks to and from the editor, 🔒 split and wrap), `utils/promote.ts` (a selection to the note's source text)
- Web add `components/Wiki/{Article,ArticleBlock,ArticleEditor,ArticleBlockEditor,SecretBlockPicker,ConflictDialog}.vue`, `components/Wiki/PromoteDialog.vue`, `components/Wiki/EntryPicker.vue`
- Web modify `utils/noteActions.ts` (`promote`), `components/Session/{SessionNoteCard,NoteActions,NoteActionSheet}.vue`, `components/Wiki/EntryTimeline.vue`, `pages/app/campaigns/[campaignId]/wiki/[entryId].vue`
- Web add `tests/unit/{article,promote}.test.ts`

**15g**
- API add `src/Features/Entries/Models/{Stats,EntryMerge}.cs`
- API add `src/Features/Entries/Models/Events/{EntryMerged,EntryAbsorbed,EntryClaimed,EntryUnclaimed,EntryStatsChanged}.cs`
- API add `src/Features/Entries/Api/{PostEntryMerge,PutEntryClaim,PutEntryStats,GetEntryHistory}/**`
- API modify `Entry.cs`, `EntryHub.cs` (`entryMerged`), `GetEntry` (stats), `MentionIndex.cs` (merged ids)
- Tests add `Scopes/Integration/Features/Entries/{MergeTests,ClaimTests,StatsTests,EntryHistoryTests}.cs`
- Web add `components/Wiki/{MergeDialog,ClaimControl,StatsEditor,EntryHistoryDialog}.vue`
- Web modify `utils/entries.ts`, `utils/entryCache.ts`, `composables/useCampaignHub.ts`, `components/Wiki/EntryHeader.vue`, `pages/app/campaigns/[campaignId]/wiki/[entryId].vue`
- Web `utils/api/schema.d.ts`: regenerated

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/14e-mobile-composer
gh stack add v2/15-step-file
```

Add each sub-step on top with `gh stack add v2/15a-entry-model` and so on.

**Glossary check.** Wiki, Entry, Kind, Article, Timeline, Promote, Secret block,
Mention, Alias, Visibility, Edit access, Player character and Stats are in §1.
This PR adds the nouns the step puts into code and UI:

- **Creator**: the member who created an entry. An entry's `DM` and `Me`
  visibility and its `Only me` edit access are relative to them.
- **Block**: one part of an article: ordinary text, a secret block or a quote.
- **Quote**: a block made by promote. It links back to its session note and has
  that note's visibility.
- **Mention chip**: how a mention is drawn: its text, linking to the entry.
- **Mention strip**: on mobile, the `@` suggestions docked above the keyboard.
- **Mention index** (code: `MentionIndex`): which session notes and blocks mention
  which entry, each with its visibility.
- **Merge**: folding one entry into another.
- **Claim**: a member marking a Character entry as their player character. That
  member is its **claimer**.

### 15a. Entry model and push

1. **One aggregate, one stream per entry** (§9). Stream id = entry id. Every event
   implements `IActorEvent`.

   ```
   EntryKind          Character | Place | Faction | Item | Event | Other   // closed set, stored as a string
   EditAccess         Anyone | OnlyMe                                     // stored as a string

   EntryCreated            { Actor, CampaignId, CreatorMemberId, Name, Kind,
                             Visibility, CreatedFromNoteId? }
   EntryRenamed            { Actor, Name }
   EntryKindChanged        { Actor, Kind }
   EntryAliasAdded         { Actor, Alias }
   EntryAliasRemoved       { Actor, Alias }
   EntryVisibilityChanged  { Actor, Visibility }
   EntryEditAccessChanged  { Actor, EditAccess }

   Entry (inline projection)
     { Id, CampaignId, CreatorMemberId, Name, Kind, Aliases[], Visibility,
       EditAccess, CreatedAt, CreatedFromNoteId?, UpdatedAt,
       Source?, Links[] }              // §11 seams: never written in 15, always null / []
   ```

   - `CreatorMemberId` is on the event, not read from `Actor`, for the same reason
     as a note's `AuthorMemberId` (14a): it stays a member when `Actor` gains a
     model case.
   - `CreatedFromNoteId` is set when the entry was created from a note (15b). It
     is provenance only. It is not the §11 `Source`, which is a reference item,
     a D&D Beyond sheet or an imported message.
   - `Source` and `Links` are declared on the document now (nullable record, empty
     list) so steps 20–22 add events, not a migration. No endpoint reads or writes
     them in 15, and they are left out of the responses.
   - A new entry has `EditAccess = Anyone`.
   - 15e adds the article, and 15g adds merge, claim and stats.
2. **Registration and indexes** (`Bootstrap.AddMartenDB`): `Snapshot<Entry>(Inline)`,
   an index on `(CampaignId, Kind)`, and a GIN index on `Aliases`.
3. **`Audience`** (`Features/Campaigns/Models/Audience.cs`) is the shared form of
   the visibility rule: `Audience.Of(visibility, ownerMemberId)` is `Everyone`,
   `DMs + owner` or `owner only`. `Contains(viewer)` is the in-memory rule and
   `Groups(campaignId)` the push rule. `SessionNoteVisibility.CanSee` and
   `SessionNoteAudience.Groups` are rewritten on top of it (hidden notes stay
   their own case). 14b's `SessionNoteAudienceTests` must keep passing unchanged.
   That is the check that this refactor changes no note behaviour.
4. **Entry visibility** (`EntryVisibility.VisibleTo(viewer)` as a LINQ expression,
   and `CanSee(entry, viewer)`). This is the note table without hiding. The creator
   takes the place of the author:

   | Entry | Creator | DM | Other player |
   |---|---|---|---|
   | `Everyone` | yes | yes | yes |
   | `DM` | yes | yes | **no** |
   | `Me` | yes | **no** | **no** |

   An entry the caller cannot see is a **404**, never a 403. "People without access
   get nothing" (§4). A hidden entry is left out of lists and counts, and in 15b
   its mentions render as plain text.
5. **Permissions** (`EntryPermissions`):

   | Change | Who |
   |---|---|
   | name, kind, aliases (and in 15e–g the article, promote, merge) | a DM; the creator; anyone who can see it when `EditAccess = Anyone` |
   | visibility, edit access | the creator and DMs |

   A caller who can see the entry but not change it gets a 403. A caller who
   cannot see it gets a 404 first (the 14a order: load, read rule, then
   permission).
6. **Endpoints.** All take `{campaignId}` and resolve the caller's member first
   with `RequireMember`.

   | Endpoint | Who | Does |
   |---|---|---|
   | `GET /api/campaigns/{campaignId}/entries` | members | `{ entries[] }`: every entry the caller can see, as `EntrySummaryResponse`. Merged entries are left out (15g). Counts arrive in 15b |
   | `POST /api/campaigns/{campaignId}/entries` | members | body `{ name, kind, visibility }`. Appends `EntryCreated` with the caller as creator |
   | `GET /api/campaigns/{campaignId}/entries/{entryId}` | who can see it | `EntryResponse` |
   | `PUT …/entries/{entryId}/name` | can edit | appends `EntryRenamed`. Unchanged appends nothing |
   | `PUT …/entries/{entryId}/kind` | can edit | appends `EntryKindChanged` |
   | `PUT …/entries/{entryId}/aliases` | can edit | body `{ aliases[] }`, the whole list. Appends one `EntryAliasAdded` or `EntryAliasRemoved` per difference, so history reads per alias |
   | `PUT …/entries/{entryId}/visibility` | creator, DMs | appends `EntryVisibilityChanged` |
   | `PUT …/entries/{entryId}/edit-access` | creator, DMs | appends `EntryEditAccessChanged` |

   Responses:

   ```
   EntrySummaryResponse { id, name, kind, aliases[], visibility, editAccess,
                          creatorMemberId, createdAt, updatedAt }
   EntryResponse        EntrySummaryResponse + 15e { article } + 15g { stats? }
   // 15g adds claimedByMemberId? and mergedFromIds[] to the summary, so the
   // directory can mark player characters and resolve merged ids
   ```

   - **Validation.** A name is trimmed and 1–100 characters long. An alias is too,
     and at most 20 aliases are allowed. Aliases are de-duplicated
     case-insensitively, and an alias equal to the name is dropped. `kind`,
     `visibility` and `editAccess` must be in their enums.
   - **Duplicate names.** `POST entries` with a name or alias that equals
     (case-insensitively) the name of an entry the caller can see is a 409, with
     that entry's id in the error. Entries the caller cannot see are not checked,
     so the 409 leaks nothing. Two DM-only "Glasstaff"s can exist, and merge
     (15g) is the fix.
   - **No delete in 15.** A typo is a rename, and a duplicate is a merge. Deleting
     would orphan mentions, which invariant 6 keeps by id.
   - Responses carry no per-viewer fields, as in 14a. The web works out "can I
     edit" from `creatorMemberId`, `editAccess` and its own role.
7. **Push** (`EntryHub.cs`, the 14b pattern). Each endpoint notifies after
   `SaveChangesAsync`, and nothing is pushed when nothing was appended:

   | Message | Payload | Sent to |
   |---|---|---|
   | `entryUpserted` | `EntrySummaryResponse` | `EntryAudience.Groups(entry)` after the change |
   | `entryRemoved` | `{ entryId }` | the groups that lose the entry on a visibility change (14b's `NotifySessionNoteMoved` rule) |

   `EntryAudience.Groups` is `Audience.Of(entry.Visibility, entry.CreatorMemberId).Groups(campaignId)`.
8. **Tests.** Use `TestCampaign` (a DM and two players).
   - `EntryTests`: create, then read; every PUT, where an unchanged value appends
     nothing; the aliases diff appends one event per alias; validation; the
     duplicate-name 409 only against visible entries; an outsider gets 403
     everywhere; every event carries an `Actor` and a correlation id.
   - `EntryVisibilityTests`: one test per cell of the table in step 4, on `GET
     entries`, `GET entries/{id}` and a PUT (404 when the caller cannot see the
     entry). One test per row of the permission table in step 5, under both edit
     access values.
   - `EntryAudienceTests` (unit): the property check from 14b. For every visibility
     × role × is-creator case, `CanSee` is true exactly when the viewer is in one
     of `Groups`. Also, the compiled `VisibleTo` agrees with `CanSee`.
   - `EntryHubTests`: with `RecordingHubFixture`, the message names, groups and
     order for create, rename and every visibility move. A `DM` entry is never sent
     to `campaign:{id}`.
9. **Web.** Regenerate (`pnpm gen:api`) and commit `schema.d.ts`. No UI change.

### 15b. Mentions, the mention index and the timeline (API)

1. **Parsing** (`MentionParser.Parse(text)`): every `@[text](entry:<guid>)` outside
   inline code and fenced code, giving `(EntryId, Text)` pairs. This matches what
   the web's markdown-it rule (14c) turns into a mention. The case list in
   `Fixtures/mentions.json` (plain, several, nested brackets, escaped `\@`, inside
   code, a malformed id, a non-`entry:` link) is run by both `MentionParserTests`
   and the web's `mentionMarkdown.test.ts`, so the two parsers cannot drift apart.
2. **Mention ids on the note.** `SessionNote` gains `MentionedEntryIds: Guid[]`
   (distinct, in order), set in `Create` and in `Apply(SessionNoteEdited)` from the
   text. It gets a GIN index. Nothing new goes on the events: the text is the
   record (invariant 6), and replaying the stream rebuilds the ids.
3. **`MentionIndex` is a query, not a stored document** (the 14 precedent for
   `SessionStream`). A mention's visibility is its source's visibility. So the
   index asks the source documents, using their own rules: `SessionNote` with
   `SessionNoteVisibility.VisibleTo` here, and `Entry` blocks with
   `EntryVisibility.CanSeeBlock` from 15e. A copy of the visibility on a separate
   row could drift from the source. This one cannot. `MentionIndex` has:
   - `NotesMentioning(campaignId, entryIds, viewer, before, take)`, for timelines;
   - `CountsFor(campaignId, viewer)`, giving `entryId → (count, lastMentionedAt)`
     over the notes the viewer can see. The note rows are loaded (id, ids,
     `PostedAt` only) and grouped in memory, which is fine at this scale;
   - in 15e and 15g, article blocks and merged ids join in.
   Unknown ids, and ids of entries in other campaigns, are kept in the note and
   never match: every query is scoped to the campaign and joined to entries the
   viewer can see.
4. **New entries from a note.** `POST notes` and `PUT notes/{id}` take
   `newEntries[] { id, name, kind }` (at most 10). The web makes the `id`
   (`crypto.randomUUID()`) and writes it into the text as `@[name](entry:<id>)`, so
   the text is never rewritten. In the same `SaveChangesAsync` as the note, so they
   share a transaction and a correlation id, each one appends
   `EntryCreated { CreatorMemberId = author, Visibility = the note's visibility,
   CreatedFromNoteId }`. An id not mentioned in the text is a 400. An id that
   already has a stream is a 409 (a retry after a timeout reuses the same ids, so
   the web shows "Already created" and reloads). The duplicate-name rule from 15a
   applies. Entries are pushed after the note, with `entryUpserted`.
5. **Timeline endpoint.**

   | Endpoint | Who | Does |
   |---|---|---|
   | `GET …/entries/{entryId}/timeline?before=&take=` | who can see the entry | `{ items[] { note: SessionNoteResponse, sessionNumber }, hasOlder }`: notes the caller can see that mention the entry, newest page first, oldest first within a page, `take` 20 by default, 50 at most, `before` a `postedAt` cursor |

   It is read-only (§4). There are no timeline writes: editing a note is the only
   way to change it.
6. **Counts on the wiki list.** `GET entries` becomes `{ entries[] { entry:
   EntrySummaryResponse, mentionCount, lastMentionedAt? } }`. The counts are per
   viewer. A mention in a note the viewer cannot see is not counted, because a
   count would otherwise reveal that the note exists. For the same reason the
   counts are never pushed.
7. **Push.** Nothing new for notes: a timeline changes only when a note changes,
   and the note's own push already reaches exactly its audience. The web works out
   which timelines to refresh from the note's text (15c).
8. **Tests.**
   - `MentionParserTests` (unit): the shared case list.
   - `MentionTests`: posting a note with a mention stores the ids, and an edit
     that removes the mention clears them. `newEntries` creates entries with the
     note's visibility and author, in the note's correlation id. An unmentioned
     new id is a 400, a reused id is a 409, and a Me note makes a Me entry.
   - `TimelineTests`: every note visibility cell (the 14a table) on the timeline.
     A hidden note shows for its author and the DMs only, and a deleted note leaves
     the timeline. Paging works. The counts come from visible notes only: a Player's
     count ignores a DM note, and the DM's does not.

### 15c. Wiki home and entry page

1. **Requests and queries.** `utils/api/entry/*` becomes `useApi().entry`. Aliases
   in `utils/api/types.ts`: `Entry`, `EntrySummary`, `EntryListItem`, `EntryKind`,
   `EditAccess`, `TimelineItem`. Queries: `["entries", campaignId]` (the list, plus
   the **entry directory** that chips and `@` read), `["entry", campaignId,
   entryId]` and `["entryTimeline", campaignId, entryId]` (infinite).
2. **Cache updates in one place** (`utils/entryCache.ts`, the 14c pattern):
   `upsertEntrySummary` (keeps the list's counts), `removeEntry`, and
   `timelineTouchedBy(note)` (entry ids in the note's text, plus loaded timelines
   that hold the note). The hub handlers call them:
   - `entryUpserted` and `entryRemoved` update the list and the loaded entry;
   - `sessionNoteUpserted` and `sessionNoteRemoved` invalidate the timelines the
     note touches.
   The list is refetched when the Wiki tab mounts, on window focus and after a hub
   join, because the counts are never pushed.
3. **Mention chips** (`utils/markdown.ts`). `renderNoteMarkdown(text, env)` takes
   `{ campaignId, resolve(entryId) → { id, kind } | undefined }` through
   markdown-it's `env`. The `entry_mention_*` rules draw
   `<a class="mention" data-entry-id href="/app/campaigns/{cid}/wiki/{id}">text</a>`
   with the kind's icon, where `id` is the resolved (after 15g, post-merge) id. An
   id that does not resolve (unknown, or an entry the viewer cannot see) renders
   as its plain text, with no link and no hint. `NoteMarkdown.vue` sends clicks on
   `a.mention` through `navigateTo`, so they stay client-side.
4. **Wiki home** (`wiki/index.vue`, §4): the kind chips (All plus the six kinds),
   sorting by Most mentioned, Recently mentioned or A–Z (the choice is kept in the
   URL), a filter box over names and aliases, and "New entry" (`NewEntryDialog`:
   name, kind, visibility). The list is one column on a phone and two from `lg`.
   It shows kind, name, aliases and "7 mentions". Loose ends are step 19, and
   nothing reserves space for them.
5. **Entry page** (`wiki/[entryId].vue`, §4):
   - `EntryHeader`: name, kind, "aka …", a visibility badge for 🔒 DM and 🔒 Me,
     and edit access. `[Edit]` opens `EntryHeaderEditor` (name, kind,
     `AliasEditor` as chips, and for the creator and DMs, visibility and edit
     access). Fields are shown only to those who can change them.
   - `EntryTimeline`: each note as its `SessionNoteCard` in a compact variant
     (session number, author, time, text with chips, and 🔒 badges), linking to
     the note (`?note=`). Paged with "Load older", oldest first.
   - "Add a note about Gundren…" goes to `/app/campaigns/{id}?about={entryId}`. The
     composer consumes the parameter once: it starts the text with the mention.
     When the entry is `DM` or `Me`, it also sets the note's visibility to match,
     so the reveal warning (15d) does not fire on the note's own subject. Then it
     removes the parameter. Until 15d the prefill is the stored form
     `@[Name](entry:<id>) `.
   - A 404 shows "That entry is not there, or you cannot see it."
   - The article (15f), connections (19), gallery (16) and combats (18) are not
     drawn at all until their steps.
6. **Tab.** The Wiki tab stays current on `wiki/[entryId]`, since the layout
   compares route names (see Notes).

### 15d. `@` in the composer

1. **Stored form and composer form** (`utils/mentions.ts`, pure). The composer
   stays a plain `<textarea>` (14d), where a GUID would be unreadable. So:
   - In the text box a mention reads `@[Gundren Rockseeker]`. The composer's state
     holds `links: Record<displayText, entryId>` and `newEntries[]`.
   - `toStoredText(text, links)` turns each linked `@[t]` into `@[t](entry:<id>)`.
     `fromStoredText(stored)` does the reverse for editing a note. If one display
     text maps to two ids, the second stays in the stored form.
   - Within one note, one display text means one entry.
   - The draft (14d) now stores `{ text, links, newEntries }` under the same key.
     An old string draft still loads.
2. **The active query.** An `@` at the start or after whitespace or `(`, followed by
   up to 40 characters with no newline, up to the caret. Spaces are allowed, so
   "Gundren Rock" works. The query closes on Esc, on a newline, or when it has no
   match and ends with a space. With the caret inside an unlinked `@[…]`, the
   bracket text is the query, and picking an entry links it without changing the
   text. That is how the display text is overridden (§3's `@[the old dwarf]`):
   pick Gundren, then edit the text inside the brackets and pick again.
3. **Matching** (`matchEntries(query, directory)`): names and aliases, ranked as
   exact, then prefix, then word prefix, then substring, with the most mentioned
   first among equals. At most 8 results. An alias match shows "Gundren
   Rockseeker · aka Rockseeker". When no name or alias equals the query, the last
   suggestion is **Create "…"** with a kind that defaults to `Character`.
4. **Desktop** (from `md`, the `CommandStrip` popover pattern): a popover above the
   text box. ↑/↓ move, Enter or Tab picks, and on a Create row Tab cycles the kind
   and Shift+Tab goes back (§3). Esc dismisses.
5. **Phone** (§3a): `MentionStrip` in the composer's `strip` slot, between the text
   box and the toolbar, so it stays above the keyboard. It holds suggestion chips,
   and `KindChips` appears below it only while a Create row is chosen. The
   toolbar gains an `@` item, first, which inserts `@` (with a space before it
   when needed) and opens the strip. Chips use `mousedown.prevent` (14e) so the
   keyboard stays up. The `/` strip and the `@` strip are never shown together:
   `/` only counts at the start of the text.
6. **Create.** Picking Create adds `{ id: crypto.randomUUID(), name, kind }` to
   `newEntries` and links the text. The chip in the preview is marked "new". At
   post, new entries whose mention has been deleted are dropped, and the rest go in
   the body (15b). The server gives them the note's visibility as it is when the
   note is posted, not when Create was picked.
7. **Reveal warning** (§3, invariant 5), checked on ➤ (and on Save in the note
   editor). `revealCheck(noteVisibility, linkedEntries, viewer)` lists the linked
   entries whose audience does not cover the note's (a `DM` entry in an `Everyone`
   note, or a `Me` entry in a `DM` note). If there are any, `RevealDialog` shows
   "Gundren is hidden from players. Reveal it?" with three choices:
   - **Reveal and post.** Offered only when the caller may change that entry's
     visibility. It sets the entry's visibility to the note's, then posts.
   - **Post without revealing.** Readers who cannot see the entry get plain text.
   - **Cancel.**
   Nothing changes visibility without that tap. The server does not block the post:
   the text is the author's, and the chip is what stays hidden.
8. **Note editor.** `NoteEditor` uses the same `fromStoredText` and `toStoredText`,
   the same strip, and `newEntries` on `PUT notes/{id}`.
9. **Optimistic notes** (14d) are built from the stored form, so the chip shows at
   once. A new entry is added to the directory from the post's response and from
   `entryUpserted`, whichever comes first.

### 15e. Articles, secret blocks and promote (API)

1. **Model.** An article is an ordered list of blocks. Ordinary text stays one block
   until a secret block or a quote splits it.

   ```
   ArticleBlock { Id, Text,                         // markdown, may contain mentions
                  Visibility, OwnerMemberId,         // 🔒 DM / 🔒 Me are relative to the owner
                  Quote?: { NoteId, SessionId, SessionNumber, AuthorMemberId,
                            PromotedByMemberId, PromotedAt } }

   EntryArticleEdited  { Actor, Blocks[] }   // the whole article after the edit
   EntryQuotePromoted  { Actor, Block }      // appended at the end

   Entry += { Article: { Blocks[] }, ArticleMentionIds[] }   // ids across all blocks, GIN index
   ```

   - A block's owner is its writer. For a quote, the owner is the quoted note's
     author, so the quote's audience is the note's audience (§4: "promoting a 🔒
     note creates a secret block with the same visibility"). A hidden `Everyone`
     note, which only its author and the DMs can see, gives a `DM` quote.
   - `EntryArticleEdited` stores the whole merged article, so every version can be
     rebuilt and shown (15g) without replaying diffs. Events never leave the
     server, so storing secret blocks in them is safe.
   - Limits: 200 blocks, and 50,000 characters in total.
2. **Who sees a block** (`EntryVisibility.CanSeeBlock(entry, block, viewer)`): the
   viewer must see the entry **and** be in the block's audience,
   `Audience.Of(block.Visibility, block.OwnerMemberId)`. `GET entries/{id}` returns
   only the blocks the caller can see. The others are absent, with no placeholder
   and no count (invariant 5).
3. **Editing without seeing everything** (`ArticleMerge`, pure, unit tested). `PUT
   …/entries/{entryId}/article` takes `{ etag, blocks[] { id?, text, visibility,
   quote? } }`, which is the caller's view of the article after their edit:
   - Blocks with an `id` are existing blocks the caller can see. Blocks without one
     are new, and the caller becomes their owner.
   - A visible block whose id is missing from the request is removed.
   - Every block the caller **cannot** see is kept, placed directly after the block
     it followed before (or first, if it was first). If that block is gone, it goes
     after the nearest earlier block that is still there.
   - An unknown id, or the id of a block the caller cannot see, is a 400.
   - Only a block's owner and the DMs can change its visibility. `quote` is kept
     from the stored block and cannot be set or changed by a PUT.
   - A block with blank text is removed.
4. **Optimistic concurrency that leaks nothing** (`ArticleEtag`). The etag is a hash
   of the blocks **the caller can see** (ids, text, visibility, order). It is not
   the stream version. If it were, an edit to a DM secret block would change a
   player's etag and give them an unexplained 409, which would reveal that the
   secret exists. So:
   - A PUT whose etag does not match the caller's current view is a 409 ("Someone
     else changed this article. Reload and re-apply.", design §4).
   - The append uses `FetchForWriting<Entry>`, so two racing writes cannot both
     win. On the loser's concurrency exception, the server reloads, checks the
     etag again and re-merges once. It returns 409 only if the caller's view
     really changed.
5. **Promote** (`POST …/entries/{entryId}/quotes`, can edit the entry). Body
   `{ noteId, text? }`. The note must be one the caller can see in this campaign,
   or it is a 404. `text` left out means the whole note (the phone's flow, §3a).
   Otherwise, after collapsing whitespace, `text` must be a substring of the note's
   current text, or it is a 400 ("A quote must be text from the note"). The quote
   is a real excerpt: the note's author wrote it, and the actor who promoted it is
   on the event. Appends `EntryQuotePromoted` with the note's audience (step 1).
   Promoting the same text from the same note twice is allowed, and the web warns.
6. **Mentions in articles.** Each block's mentions come from `MentionParser`.
   `MentionIndex` gains `BlocksMentioning(entryIds, viewer)` (the entries whose
   `ArticleMentionIds` contain the id, filtered in memory with `CanSeeBlock`).
   Nothing in 15 draws it. It is the seam for connections (step 19). `newEntries`
   from 15b is also accepted on `PUT article`, and the new entry gets the
   narrower of the entry's and the block's audience. Where neither is narrower
   (`DM` owned by one member, `Me` owned by another), the new entry is `Me` for
   the caller.
7. **Push** (`entryArticleChanged { entryId }`). It goes to `member:{id}` for each
   member whose **visible** article changed, meaning their etag before and after
   differ, worked out from the campaign's members with the read rule. So an edit
   inside a DM secret block pings DMs and that block's owner, and nobody else. The
   payload carries no content. Clients that have the entry loaded refetch it.
   Changes to the whole entry (renames and so on) keep 15a's `entryUpserted`.
8. **Tests.**
   - `ArticleMergeTests` (unit): re-inserting hidden blocks at the start, middle and
     end, after a deleted neighbour, and after a reorder; an unknown id; a hidden id;
     changing a block's visibility as its owner, as a DM, and as someone else
     (refused); blank blocks.
   - `ArticleEtagTests` (unit): a change to a hidden block leaves the caller's etag
     alone, and a change to a visible block changes it.
   - `ArticleTests`: edit and read back; a stale etag is a 409; two racing PUTs; a
     player's save keeps a DM secret block they never saw, in place; the limits;
     permission follows edit access.
   - `SecretBlockTests`: for every entry visibility × block visibility × role ×
     is-owner case, `GET entries/{id}` contains the block exactly when
     `CanSeeBlock` says so; `entryArticleChanged` reaches exactly the members whose
     view changed. With a recording hub, a player gets nothing when a DM edits a
     DM block.
   - `PromoteTests`: the whole note; a substring; a non-substring is a 400; a note
     the caller cannot see is a 404; quotes of `Everyone`, hidden, `DM` and `Me`
     notes get the note's audience; the quote's link fields.
9. **Web.** Regenerate `schema.d.ts`. No UI change until 15f.

### 15f. Article editor and promote (web)

1. **Reading.** `Article` draws the blocks in order:
   - Ordinary blocks are markdown with chips (`renderNoteMarkdown`, but with
     headings kept: an article is a document, so a `document` option turns 14c's
     heading rule off).
   - Secret blocks are framed "🔒 DM" or "🔒 Me".
   - Quotes are a blockquote with "— Sam, Session 12 ↗", linking to `?note=`.
   An empty article shows "Nothing written yet", with Edit for those who can edit.
2. **Editing** (`ArticleEditor`, one mode for the whole article). Each block is an
   `ArticleBlockEditor` text box with `@` (15d's strip and popover, with the same
   `links` state per block), the formatting toolbar, and a 🔒 button (§3a):
   - with a selection, it splits the block into three, and the middle becomes a
     secret block;
   - without one, it makes the current block secret.
   `SecretBlockPicker` chooses 🔒 DM or 🔒 Me, or back to ordinary, and is offered
   only to the block's owner and the DMs. Blocks move up and down and can be
   deleted. "+ Text" and "+ 🔒 Secret" add blocks. Adjacent ordinary blocks are
   joined on save (`utils/article.ts`).
3. **Saving** sends the caller's view and the etag. On a 409, `ConflictDialog`
   shows the caller's text of each changed block to copy, then reloads (design §4:
   reload and re-apply). An `entryArticleChanged` push while the editor is open
   shows "This article changed" rather than replacing the text under the user.
4. **Promote.**
   - **Desktop, from a selection.** Selecting text inside one note's
     `NoteMarkdown` shows an "Add to wiki" button by the selection.
     `utils/promote.ts` maps the rendered selection back to the note's markdown:
     it builds a plain-text view of the source with an offset map (dropping `*`,
     `_`, `` ` ``, the mention and link syntax, and list markers), finds the
     selection in it, and widens to whole mentions.
   - **Menu, sheet and timeline.** The note actions menu, the long-press
     `NoteActionSheet` (§3a) and the timeline's `[Promote]` offer the whole note.
     On a phone that is the only way (design §3a).
   - Both open `PromoteDialog`. It has a text box pre-filled with the source
     excerpt, which can only be trimmed usefully (the server rejects anything that
     is not a substring), and `EntryPicker` (the `@` matching, plus Create with the
     note's visibility). From the timeline, the entry is already picked. The dialog
     says "Players won't see this quote" when the note is `DM`, `Me` or hidden.
   - After promoting, a toast links to the entry, and on a phone it opens the
     article editor at the new quote to trim it (§3a).
   - `NoteAction` gains `promote`, for anyone who can see the note.
5. The Campaign stream is unchanged apart from the new action and the selection
   button.

### 15g. Merge, claim, stats and history

1. **Merge** (`POST …/entries/{entryId}/merge`, body `{ intoEntryId }`). The caller
   must be able to edit both entries, and both must be in this campaign and not
   already merged.
   - **Visibility guard.** Merging is refused (409, "Change visibility first") unless
     everyone who can see the target can already see the merged entry. That means
     `Audience(into) ⊆ Audience(from)`, checked with the campaign's current members.
     Otherwise the merged entry's name (which becomes an alias) and its article
     would reach people who could not see them. Merging a DM "Glasstaff" into an
     Everyone "Iarno" means revealing Glasstaff first, on purpose. Merging an
     Everyone entry into a DM one is allowed, and the dialog warns "Players will no
     longer see Gundren".
   - **Events**, appended in one `SaveChangesAsync`:
     - `EntryMerged { Actor, IntoEntryId }` on the merged entry;
     - `EntryAbsorbed { Actor, FromEntryId, FromName, FromAliases[], FromBlocks[],
       FromMergedIds[] }` on the target.
     The target's projection adds the name and aliases as aliases. It appends an
     ordinary block "Merged from Gundren", followed by the merged entry's blocks
     with their visibility and owners unchanged, so secret blocks stay secret. It
     also adds `FromEntryId` and `FromMergedIds` to `MergedFromIds`, so chains
     (A into B, then B into C) resolve in one step.
   - **Redirect.** The merged entry's document keeps `MergedIntoId`. `GET
     entries/{id}` for it returns the target's `EntryResponse` (the web replaces
     the URL), or a 404 when the caller cannot see the target. Lists leave merged
     entries out.
   - **Mentions resolve without rewriting text** (invariant 6). `MentionIndex`
     queries for `entry.Id ∪ entry.MergedFromIds`. The web's directory maps every
     merged id to its target, so an old chip links to the target. Its text stays
     what the author wrote.
   - A claimed entry can be merged only into an unclaimed one or one with the same
     claimer, and the claim moves with it.
   - **Push**: `entryMerged { fromEntryId, intoEntryId }` to the target's audience,
     then `entryUpserted` of the target. `entryRemoved` goes to groups that could
     see the merged entry but not the target.
2. **Claim** (`PUT …/entries/{entryId}/claim`, body `{ memberId? }`, where null
   unclaims). `EntryClaimed { Actor, MemberId }` and `EntryUnclaimed { Actor }`.
   - Only a `Character` entry can be claimed. A member can claim an unclaimed one
     they can see, for themselves. A DM can assign any visible Character to any
     member, or unclaim it. A claimer can unclaim their own.
   - A member can claim several entries (§1).
   - Changing the kind of a claimed entry away from `Character` is a 409 until it
     is unclaimed.
   - The claimer is on `EntrySummaryResponse` (`claimedByMemberId`, with
     `mergedFromIds`), so the wiki list can mark player characters.
3. **Stats** (`PUT …/entries/{entryId}/stats`, body `{ initiativeRoll?, maxHp?,
   ac? }`, where all null clears them). `EntryStatsChanged { Actor, Stats? }`.
   - Stats exist only on `Character` entries.
   - `initiativeRoll` and `maxHp` are dice expressions checked with
     `IDiceRoller.Check`. A plain number is a valid expression, and step 18 rolls
     `maxHp` when a combatant is added (§8: "HP: rolled when the combatant is
     added"). `ac` is 0–99.
   - **Writes:** on a claimed entry, the claimer and the DMs. On an unclaimed one,
     DMs only (§4).
   - **Reads:** on a claimed entry, everyone who can see it. On an unclaimed one,
     DMs only: `stats` is absent for players. A monster's HP and AC are combat
     secrets (invariant 8), and step 18's `PlayersSee` decides what players see in
     a fight.
4. **History** (`GET …/entries/{entryId}/history`, who can see the entry). Every
   event, oldest first, as `{ at, actorMemberId, change }`, where `change` is a
   tagged union per event type. It is **redacted per viewer**:
   - an article version lists only the blocks the viewer can see;
   - an edit that changed no block they can see is left out;
   - `EntryAbsorbed` shows the merged entry's name only if the viewer can see the
     target.
   Reverting is re-saving an old version through `PUT article` with the current
   etag. The web offers "Restore this version" in `EntryHistoryDialog`. It uses the
   same merge, so hidden blocks survive a restore.
5. **Web.**
   - `MergeDialog` (from the header's menu): pick the target with `EntryPicker`,
     with a preview of what moves and the "Players will no longer see…" warning.
   - `ClaimControl`: "Claim as my character", "Unclaim", or for DMs, a member picker.
   - `StatsEditor`: three fields, with the dice error shown inline.
   - `EntryHistoryDialog`.
   - The hub handles `entryMerged`, and `entryCache.applyMerge` maps the merged id
     to the target in the directory.
6. **Tests.**
   - `MergeTests`: the alias, the appended article with its secret blocks kept,
     the redirect, timeline and counts including the merged entry's mentions, a
     chain of two merges, the visibility guard (refused and allowed cases),
     permission on both entries, and claim handling.
   - `ClaimTests`: the table in step 2.
   - `StatsTests`: the write and read rules, dice validation, and non-Character
     entries refused.
   - `EntryHistoryTests`: redaction of hidden block edits and of the absorbed name,
     and restoring an old version keeps a hidden block.

## Verify

1. `dotnet test` and `pnpm build` pass, `vitest` passes, `schema.d.ts` is fresh, and
   CI is green on every PR in the stack.
2. `pnpm dev`, then three browser profiles at a phone size (390 × 844): A (the
   owner, DM), with B and C joined as Players.
   1. In the composer, B types `We met @Gund`, picks **Create "Gundren"**, chooses
      Character with the kind chips, and posts. A and C see a Gundren chip. The
      Wiki tab lists Gundren · Character · 1 mention.
   2. A posts with `/dm` a note that creates `@Glasstaff`. C's wiki has no
      Glasstaff, and `GET entries/{id}` for it is a 404 for C.
   3. A posts an Everyone note mentioning Glasstaff. The reveal warning shows.
      A picks "Post without revealing", and C sees the note with "Glasstaff" as
      plain text.
   4. Gundren's page shows both notes on its timeline for A, and one for C. "Add a
      note about Gundren…" opens the composer with the mention.
   5. B renames Gundren to "Gundren Rockseeker". The old notes still read
      "Gundren" and link to the renamed entry.
   6. B adds the alias "Rockseeker". `@Rock` suggests Gundren Rockseeker (aka
      Rockseeker).
   7. A promotes a selection of B's note to Gundren's article on desktop. On a
      phone, C long-presses a note, picks Promote to wiki, and trims the quote in
      the article editor. Both quotes link back to their notes.
   8. A adds a 🔒 DM secret block. B and C never receive it, whether by reading,
      by a push, or through history. C edits the article and saves, and A's secret
      block is still there, in place.
   9. B and C edit the same article. The second save gets the reload-and-re-apply
      dialog.
   10. B sets Gundren to Only me. C can no longer edit it, and A still can.
   11. C creates "Gundren" again, and A merges it into "Gundren Rockseeker". C's
       old chip links to the target, the name is an alias, and the timeline holds
       both entries' notes.
   12. B claims their Character entry and sets stats. C cannot edit them, and for
       an unclaimed NPC with stats, C sees no stats.
3. In Postgres, every `mt_events` row of the entry streams has a `correlation_id`
   and an `actor`. A note that created an entry shares its correlation id with that
   `entry_created`.

## Notes / gotchas

- **Commit scopes:** `api`, `web`, `docs` (and the other hook scopes from 14).
  Every PR that changes an API contract regenerates (`pnpm gen:api`, which runs
  `gen:openapi` first) and commits `schema.d.ts`. `openapi.json` is not tracked.
- **Reset the dev database after 15b.** `SessionNote` gains `MentionedEntryIds`, and
  notes projected before 15b have none. Marten can rebuild the inline projection,
  but v2 has no users, so reset it:
  `docker compose -p takeinitiative -f compose.dev.yml down -v`.
- **Why the mention index is not a stored document.** §9 lists `MentionIndex` as a
  read model, and so it is: a named query over mention ids projected onto its
  sources (`SessionNote.MentionedEntryIds`, `Entry.ArticleMentionIds`). A stored
  row per mention would need its visibility copied from the note or block and
  kept in step on every hide, visibility change and merge. Reading the source's
  own rule cannot drift. If ⌘K (17) or connections (19) need SQL-side joins, a
  materialised table can be added then from the same events.
- **Hidden things are absent, including their traces.** Four places could leak
  where a naive design would:
  - mention counts (counted per viewer);
  - the article etag (hashed per viewer);
  - article pushes (sent only to members whose view changed);
  - history (redacted per viewer).
  A chip for an entry the viewer cannot see is plain text. That is the author's
  text, which the viewer can already read.
- **Why the composer shows `@[Name]` and not the stored form.** It keeps a plain
  textarea (14d) readable. The cost is the rule that one display text means one
  entry per note, and `utils/mentions.ts` is where that lives.
- **New entries are created with the note, not when Create is picked.** An entry
  then inherits the note's visibility as posted (§3), shares its correlation id,
  and an abandoned draft leaves no orphan entry. Client-made ids keep the text
  exactly as written, with no server-side rewriting.
- **Merge never widens visibility.** The `Audience(into) ⊆ Audience(from)` guard is
  how "nothing is revealed automatically" holds for merge. Revealing is a
  deliberate visibility change first.
- **The layout's tab match.** `layouts/campaign.vue` highlights a tab by
  `route.name ===`. `wiki/[entryId]` has its own route name, so the Wiki tab needs
  a prefix match. Moving `wiki.vue` to `wiki/index.vue` keeps the name
  `app-campaigns-campaignId-wiki`.
- **Seams for later steps.**
  - `Entry.Source` and `Links` (§11) are on the document, unwritten.
  - `MentionIndex.BlocksMentioning` is ready for connections (19).
  - Loose ends (19) are "a note with no `MentionedEntryIds`", "`Kind = Other`",
    and "mentioned, empty article". Each one is a query on fields this step adds.
  - Stats and claim feed combatants (18).
  - `matchEntries` is the client half of ⌘K's entry section (17), which moves the
    matching server-side with trigram.
- **Not in 15:** deleting entries, un-merging (history shows a merge, and there is
  no undo), images and galleries (16), the combats list (18), connections and loose
  ends (19), and real-time co-editing (§12).
- **15a, as built** (PR on `v2/15a-entry-model`):
  - **The rule functions for 15b onward.** Reads: `EntryVisibility.VisibleTo(member)` (a LINQ expression for every entry query) and `EntryVisibility.CanSee(entry, member)` (single loads). Pushes: `EntryAudience.Groups(entry)`, and `EntryAudience.Of(entry)` for the `Audience` itself. Edits: `EntryPermissions.CanEdit` and `CanChangeAccess`, used through `EntryAccess.RequireVisibleEntry` (404), `RequireCanEdit` and `RequireCreatorOrDm` (403). Pushes go through `hub.NotifyEntryUpserted(entry)` and `hub.NotifyEntryMoved(before, after)`.
  - **`Audience`** is a record with a `Reach` (`Everyone`, `DmsAndOwner`, `OwnerOnly`) and an `OwnerMemberId`. `SessionNoteAudience.Of(note)` maps a hidden `Everyone` note to the `DM` audience, and `SessionNoteVisibility.CanSee` and `SessionNoteAudience.Groups` are now one-liners on top of it. `SessionNoteVisibility.VisibleTo` stays hand-written, because Marten needs an expression. 14b's `SessionNoteAudienceTests` pass unchanged. Secret blocks (15e) can use `Audience.Of(block.Visibility, block's owner)`. The merge guard (15g) can compare two `Audience`s' `Reach`.
  - **DTOs.** `EntrySummaryResponse` (lists and `entryUpserted`) and `EntryResponse` (`GET entries/{id}` and every write) have the same fields today. **`EntryResponse` is flat, not derived from the summary.** A derived record with no fields of its own generates `Summary & Record<string, never>` in `schema.d.ts`, which makes every field `never`. 15e and 15g add their fields to it directly. `GetEntriesResponse { entries }` and `EntryRemovedMessage { entryId }` are the other shapes.
  - **Duplicate 409.** A `POST` name equal (ignoring case) to the name or an alias of a visible entry is a 409. The body is the usual `errors` map: `errors.name[0]` is the message and `errors.existingEntryId[0]` is the id (`PostEntry.ExistingEntryIdKey`). Rename does not check duplicates.
  - **Aliases.** They are diffed by exact string, so changing only an alias's case is a removal and an addition. Order-only changes append nothing. Removals are appended before additions. A rename to a name equal (ignoring case) to an existing alias also appends `EntryAliasRemoved` for that alias, so an alias never equals the name. The name and alias rules are `EntryNameRules` in `PostEntry.cs` (`EntryName()`, `NormalizeAliases`, `IsCalled`). 15b's matching can reuse `IsCalled`.
  - **Seams.** `Entry.Source` is an `EntrySource(Provider, ExternalId, Url)` and `Links` is `EntryLink(Url, Label?)[]`. Neither is read, written or sent. `EntryCreated.CreatedFromNoteId` defaults to null, and 15b sets it. `GET entries` orders by name until 15b adds counts.
  - **Verify, as run in 15a** (API only, so no browser):
    - `dotnet test` passes 132/132, and a second run also passed. That is 74 from 14 plus 58 new: `EntryAudienceTests` 21, `EntryTests` 12, `EntryVisibilityTests` 16 and `EntryHubTests` 9.
    - `nuxi typecheck` is clean, `vitest` passes 94/94, `nuxt build` succeeds, and `schema.d.ts` is regenerated.
    - `entries15a.mjs` passed 30/30 against the API on 5010, with a DM and two players on a real `CampaignHub`. It checked creation per visibility, lists and 404s per viewer, the duplicate 409 against visible entries only, edits, `Only me`, three visibility moves, and that each viewer's push-fed cache equals a fresh `GET`.
    - The earlier scripts still pass: `smoke14a`, `hub14b` 28/28, `stream14c` 111/111, `composer14d` 142/142, `filters14e` 233/233 and `pages13d`.
    - Verify 3: every `entry_*` row in the dev database's `mt_events` has a `correlation_id` and an `Actor`.
