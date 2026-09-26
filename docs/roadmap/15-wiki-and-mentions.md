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
| 15b | `v2/15b-mention-index` | Mentions and timeline (API) | The same. Notes can mention and create entries, and the API serves timelines and mention counts | [x] |
| 15c | `v2/15c-wiki-pages` | Wiki home and entry page | The Wiki tab lists entries. An entry page shows its header and timeline, mentions in notes are chips, and "Add a note about X" works | [x] |
| 15d | `v2/15d-mention-composer` | `@` in the composer | `@` links or creates entries from the composer: a popover on desktop, the mention strip on a phone | [x] |
| 15e | `v2/15e-articles-api` | Articles, secret blocks, promote (API) | The same in the browser. The API stores articles as blocks, redacts secret blocks per viewer and promotes quotes | [x] |
| 15f | `v2/15f-article-editor` | Article editor and promote (web) | Articles are shown and edited, with 🔒 and `@`. Promote works from the stream, the timeline and the long-press sheet | [x] |
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
- As built, also: add `composables/useMentionPicker.ts` and `components/Composer/MentionLinks.vue`; modify `utils/entries.ts` (`aboutPrefill` moved out), `utils/queries/entries.ts`, `components/Session/SessionNoteCard.vue`
- Web add `tests/unit/mentions.test.ts`

**15e**
- API add `src/Features/Entries/Models/{Article,ArticleBlock,ArticleMerge,ArticleEtag}.cs`
- API add `src/Features/Entries/Models/Events/{EntryArticleEdited,EntryQuotePromoted}.cs`
- API add `src/Features/Entries/Api/{PutEntryArticle,PostEntryQuote}/**`
- API modify `Entry.cs` (the article, `ArticleMentionIds`), `EntryVisibility.cs` (`CanSeeBlock`), `EntryHub.cs` (`entryArticleChanged`), `GetEntry` (the redacted article), `MentionIndex.cs`
- Tests add `Scopes/Unit/{ArticleMergeTests,ArticleEtagTests}.cs`, `Scopes/Integration/Features/Entries/{ArticleTests,SecretBlockTests,PromoteTests}.cs`
- Web `utils/api/schema.d.ts`: regenerated
- As built, also: add API `Models/ArticleView.cs` (`ArticleView`, `ArticleVersion`, `ArticleHistory`); modify `Mentions/NewEntries.cs` (a general `AppendNewEntries`), `GetEntryTimeline` (`articleMentions`), `GetEntries` (article counts), `Bootstrap.cs` (the GIN index) and every entry write (the response is per viewer); add Tests `Scopes/Integration/InterferingSaveFixture.cs` and `Features/Entries/ArticleMentionTests.cs`; modify Web `tests/unit/entryCache.test.ts` (the new required fields)

**15f**
- Web add `utils/article.ts` (blocks to and from the editor, 🔒 split and wrap), `utils/promote.ts` (a selection to the note's source text)
- Web add `components/Wiki/{Article,ArticleBlock,ArticleEditor,ArticleBlockEditor,SecretBlockPicker,ConflictDialog}.vue`, `components/Wiki/PromoteDialog.vue`, `components/Wiki/EntryPicker.vue`
- Web modify `utils/noteActions.ts` (`promote`), `components/Session/{SessionNoteCard,NoteActions,NoteActionSheet}.vue`, `components/Wiki/EntryTimeline.vue`, `pages/app/campaigns/[campaignId]/wiki/[entryId].vue`
- Web add `tests/unit/{article,promote}.test.ts`
- As built, also: add `components/Wiki/PromoteSelection.vue` (the desktop "Add to wiki" button) and `utils/api/entry/{putEntryArticle,postEntryQuote}Request.ts`; modify `components/Session/{NoteMarkdown,SessionStream}.vue`, `components/Composer/{ComposerToolbar,RevealDialog}.vue`, `utils/markdown.ts` (the `document` option), `utils/queries/entries.ts`, `composables/{useApi,useCampaignHub}.ts`, `utils/api/types.ts`, `tests/unit/{markdown,noteActions}.test.ts`. `NoteActions.vue` and `NoteActionSheet.vue` only gained the icon

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
- **15b, as built** (PR on `v2/15b-mention-index`):
  - **What a mention is.** `MentionParser.Parse(text)` parses the text as CommonMark with raw HTML off, using Markdig (a new API package, BSD-licensed), which is the dialect of the web's markdown-it. A mention is an unescaped `@` directly before an inline link whose destination is `entry:` (lower case) plus a GUID in its 36-character form (hex digits in either case). Images, autolinks, links in code spans and code blocks, `\@[…](entry:…)`, `@ […]` and `[…](entry:…)` with no `@` are not mentions. `Mention.Text` is the link text as plain text: emphasis markers dropped, code spans kept as their content, and line breaks as spaces. `MentionParser.EntryIds` is the distinct ids in order of first mention. `Fixtures/mentions.json` has 12 cases, and `MentionParserTests` runs them all.
  - **For 15c's `mentionMarkdown.test.ts`.** The 14c markdown-it rule is looser than the fixture: it makes every `entry:` link a mention. To pass the shared cases, the rule must: require the previous child to be a `text` token ending in `@` (an escaped `\@` is a `text_special` token, and the rule runs before `text_join`, so it is excluded as it is today); match the href with `/^entry:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i` (a case-sensitive `entry:`); and skip `autolink`/`linkify` links. The text is the `text`, `text_special` and `code_inline` content between open and close, with a space for each break, trimmed. A throwaway markdown-it script with those rules passed all 12 cases. Read the file with a relative import from `apps/TakeInitiative.Api.Tests/Fixtures/mentions.json`. The test project copies it to its output.
  - **`SessionNote.MentionedEntryIds`** is set in `Create` and `Apply(SessionNoteEdited)`. The GIN index is on `(data ->> 'MentionedEntryIds')::jsonb`. **Reset the dev database** (done for this PR's smoke run).
  - **`MentionIndex`** (`src/Features/Entries/Mentions/MentionIndex.cs`) has `NotesMentioning(session, campaignId, entryIds, viewer, before, take, ct)` → `MentionedNotesPage(Notes oldest first, HasOlder)` and `CountsFor(session, campaignId, viewer, ct)` → `entryId → MentionCount(Count, LastMentionedAt)`. A note that mentions an entry twice counts once. **Deviation (how, not what):** "mentions any of these ids" is a raw SQL fragment, `(d.data ->> 'MentionedEntryIds')::jsonb ?| text[]`, and `EXPLAIN` shows a bitmap scan on the GIN index. Marten's own translation of `MentionedEntryIds.Any(id => ids.Contains(id))` unnests every note in the table in a CTE, before the campaign filter. Two more traps: C# 14 binds `array.Contains(x)` in an expression to `MemoryExtensions.Contains`, which Marten rejects, and `MatchesJsonPath(sql, stringArray)` spreads the array over `params`. 15g passes the merged ids as `entryIds`.
  - **`SessionNote.PostedAt` is truncated to microseconds** (a fix to 14a's projection). The first CI run failed on Linux, where .NET ticks are 100 ns. There, `CountsFor`'s projected `lastMentionedAt` (cast through `timestamptz`) differed from the note's `postedAt` in the document. For the same reason, a timeline cursor could skip a note posted within a microsecond. macOS clocks are microsecond-precise, so this did not show locally.
  - **New entries.** `POST notes` and `PUT notes/{id}` take `newEntries?: { id, name, kind }[]`. The validator (400) checks for at most 10 entries, a non-empty id, the 15a name rule, and distinct ids and names (ignoring case). The endpoint then checks, in this order:
    - an id not mentioned in the text is a 400 with `errors.newEntries`;
    - an id with a stream is a 409 with `errors.alreadyCreatedEntryId[0]`;
    - a name that is the name or an alias of an entry the author can see is a 409 with `errors.existingEntryId[0]` (that entry) and `errors.newEntryId[0]` (the clashing new one).

    Nothing is saved on any error. Entries are pushed with `entryUpserted` after the note's own push. **Refinement:** on `PUT`, a hidden `Everyone` note creates `DM` entries, the note's real audience (`NewEntries.VisibilityFrom`), so creating an entry reveals nothing. A `PUT` with new entries but unchanged text appends only the entries.
  - **Endpoints and shapes (for 15c).**
    - `GET /api/campaigns/{campaignId}/entries` → `GetEntriesResponse { entries: EntryListItemResponse[] }`, still ordered by name. Each item is `EntryListItemResponse { entry: EntrySummaryResponse, mentionCount: number, lastMentionedAt?: string | null }`. The counts are per viewer and never pushed, so `entryUpserted` still carries a bare `EntrySummaryResponse`. After a push, the web keeps the cached count, or refetches the list.
    - `GET /api/campaigns/{campaignId}/entries/{entryId}/timeline?before=&take=` → `EntryTimelineResponse { items: EntryTimelineItem[], hasOlder }`, where `EntryTimelineItem { note: SessionNoteResponse, sessionNumber }`. Items are oldest first within a page, and the first call returns the newest page. For older notes, pass `before` = the first item's `note.postedAt`. `take` defaults to 20 and is 1 to 50 (400 outside that). An entry the caller cannot see is a 404.
  - **Mentions of entries the viewer cannot see.** The note text reaches every viewer of the note unchanged (invariant 6), so a viewer can read `@[Glasstaff](entry:<id>)` for an entry they cannot see. The web draws a mention as a chip only when the id is in the viewer's entry directory (from `GET entries` and the pushes). Otherwise it draws the link text as plain text, with no link, no "unknown" styling and no request for the id (glossary: Mention chip). The same holds for unknown ids and, until 15g's redirects, merged ones.
  - **Verify, as run in 15b** (API only, so no browser):
    - `dotnet test` passes 171/171 on two runs: the 132 from 15a plus `MentionParserTests` 14, `MentionTests` 8 and `TimelineTests` 17. 15a's list assertions now read `.Entry`.
    - `nuxi typecheck` is clean, `vitest` passes 94/94, `nuxt build` succeeds, and `schema.d.ts` is regenerated. No web code reads the entries list yet.
    - `mentions15b.mjs` passed 44/44 against the API on 5010, with a DM and two players on a real `CampaignHub`, and it covers Verify 2.1–2.5 at the API level. It checked:
      - pushes, with the note before its entries, each reaching its audience;
      - a DM-note entry and a Me-note entry, with 404s for the others;
      - an Everyone note mentioning a DM entry, which a player reads while having no entry, no timeline and no count;
      - timelines and counts per viewer, including a hidden note and a deleted note;
      - all three error paths and the 11-entry limit;
      - a rename that leaves the text alone;
      - an edit that creates an entry, and an edit that drops a mention;
      - paging with `take=2`.
    - Verify 3: every entry created from a note shares a correlation id with an event in that note's stream (5/5), and no `entry_*` event lacks a correlation id or an actor.
    - The earlier scripts still pass: `smoke14a`, `hub14b` 28/28, `stream14c` 111/111, `composer14d` 142/142, `filters14e` 233/233, `pages13d` and `entries15a` 30/30. `entries15a` now reads the list's `{ entry }` items.
- **15c, as built** (PR on `v2/15c-wiki-pages`):
  - **Requests and queries.** `useApi().entry` has `list`, `get`, `create`, `timeline`, `putName`, `putKind`, `putAliases`, `putVisibility` and `putEditAccess` (`utils/api/entry/*`). `utils/api/types.ts` adds `Entry`, `EntrySummary`, `EntryListItem`, `EntryList`, `EntryKind`, `EditAccess`, `TimelineItem` and `EntryTimeline`. `utils/queries/entries.ts` has the three queries (`getEntriesQuery`, `getEntryQuery`, `getEntryTimelineQuery`, keys as specified), the mutations, and the query-client glue the hub and the mutations share: `applyEntrySummary`, `applyEntryRemoved`, `invalidateTimelinesTouchedBy` and `invalidateEntries`. The entry and timeline queries do not retry a 404.
  - **The entry directory** (`utils/entries.ts`). `entryDirectory(list)` gives `{ byId, items }`, where `byId` is keyed by the lower-case id. It is memoised per list object, so every chip shares one. `resolveEntry(directory, id)` is the one place 15g adds merge redirects. `useEntryDirectory(campaignId)` (in `utils/queries/entries.ts`) wraps the `["entries", campaignId]` query. That query has `staleTime: Infinity` and `refetchOnWindowFocus: "always"`, and the Wiki tab refetches it on mount. `invalidateEntries` runs after a hub join, a reconnect, and the viewer's own role change.
  - **Cache** (`utils/entryCache.ts`): `upsertEntrySummary` (a new entry starts at 0 mentions until the next read), `removeEntry`, `mergeEntrySummary` (from 15e the article stays as loaded), `timelineTouchedBy`, `timelineHoldsNote` and `timelineItems`. **Addition:** the note mutations (post, edit, visibility, hide, delete) also call `invalidateTimelinesTouchedBy`, so a timeline refreshes even when the hub is down.
  - **Mention chips.** The rule in `utils/markdown.ts` now matches the shared fixture, which `tests/unit/mentionMarkdown.test.ts` loads (all 12 cases, parsed and drawn). A chip is `<a class="mention" data-entry-id data-kind href="/app/campaigns/{cid}/wiki/{id}">` with the kind's icon in a `span.mention-icon`. `renderNoteMarkdown(text, env?)`: with no `env`, every mention is its plain text. `noteMentions(text)` and `mentionedEntryIds(text)` are the web's `MentionParser`. **Deviation:** an `entry:` link that is not a mention (no `@`, a malformed id, an autolink) is drawn as its plain text, not as a dead link. `NoteMarkdown.vue` now takes `campaignId`, reads the directory itself, and sends unmodified clicks on `a.mention` through `navigateTo`.
  - **Kind icons** are emoji (`ENTRY_KINDS`: 👤 Character, 📍 Place, 🛡️ Faction, 🗝️ Item, 📅 Event, 📄 Other), so the same icon works in the markdown HTML and in Vue.
  - **Wiki home** (`wiki/index.vue`): `WikiKindFilter`, a filter box over names and aliases (not kept in the URL), a sort menu, and New entry. The kind and sort are in the URL (`?kind=place&sort=recent`), and the defaults (All, Most mentioned) are no parameter. Most mentioned breaks ties by the latest mention, then A–Z. Recently mentioned puts never-mentioned entries last. `NewEntryDialog` opens with the filter text as the name and goes to the new entry. A duplicate-name 409 shows the server's message and an "Open it" link (`existingEntryIdFrom`).
  - **Entry page** (`wiki/[entryId].vue`): `EntryHeader` / `EntryHeaderEditor` (with `AliasEditor`), `EntryTimeline`, and "Add a note about X…". **Addition:** `components/Wiki/ChoiceChips.vue` is a touch-sized radio chip group, used for the kind, the visibility and the edit access. The editor sends one PUT per changed field: the name first, then kind, aliases, edit access and visibility. It warns when a visibility change narrows who sees the entry. Edit access reads "Only me" to the creator and "Only Sam" to everyone else.
  - **Timeline card.** `SessionNoteCard` has a `timeline: { sessionNumber }` variant, and `session` is now optional. It shows `S12` linking to `?note=`, the date instead of the time, and the 🔒, edited and hidden badges. It is read-only there, with no menu and no long-press: 15f adds Promote.
  - **`?about=`.** The Campaign page resolves the id through the directory and passes `about` to `Composer`. The composer's `aboutPrefill` prepends `mentionMarkup(name, id) + " "` (skipped if the text already starts with it), sets the visibility for a `DM` or `Me` entry, focuses the text box with the caret at the end, and emits `aboutUsed`, and the page then drops the parameter. An id not in the directory is dropped silently. `mentionMarkup` escapes `` \ ` * _ [ ] < > ~ & `` in the name, so the text reads back unchanged.
  - **Tab.** `layouts/campaign.vue` matches Wiki and Combat by name prefix. The Campaign tab still matches exactly.
  - **Script fix:** `entries15a.mjs` had a race: no settle after the DM's kind change, so its push could land after the next mark. It now waits. The API is unchanged.
  - **Verify, as run in 15c** (no browser):
    - `dotnet test` passes 171/171. The API is unchanged.
    - `nuxi typecheck` is clean, `nuxt build` succeeds, and `vitest` passes 158/158: the 94 from before plus `mentionMarkdown` 32, `entries` 22 and `entryCache` 9. `markdown.test.ts`'s two 14c seam tests now use real GUIDs, and one test was added.
    - `wiki15c.mjs` passed 64/64 against the API on 5010, with a DM and two players on a real `CampaignHub`. It loads the web's own `markdown.ts`, `entries.ts` and `entryCache.ts` through Node's type stripping, and checks:
      - per-viewer chips, with a DM entry drawn as plain text for players;
      - that each viewer's push-fed list equals `GET entries`;
      - timelines per viewer, 404s for hidden entries, and paging with the web's page parameter;
      - `timelineTouchedBy` on post, edit and delete pushes;
      - `aboutPrefill` for `Everyone` and `DM` entries;
      - that the web's permission rules agree with the server for every viewer under both edit access values;
      - a rename and an alias that keep the cached count;
      - the duplicate 409 id;
      - a move to `Me` that removes the entry for the others.
    - `pages15c.mjs` passed 24/24 against `nuxt dev`: the new routes load, and every new or changed SFC and module compiles.
    - The earlier scripts pass: `smoke14a`, `hub14b` 28/28, `stream14c` 111/111, `composer14d` 142/142, `filters14e` 233/233, `pages13d`, `entries15a` 30/30 (three runs after its fix) and `mentions15b` 44/44.
  - **For 15d.**
    - **Where chips render.** Chips are drawn in `utils/markdown.ts` (the `entry_mention_*` renderer rules) and only through `NoteMarkdown.vue`. A preview that marks new entries can pass its own `env.resolve`, returning the pending new entries too.
    - **The directory.** It is `useEntryDirectory(campaignId)` over `getEntriesQueryKey(campaignId)` = `["entries", campaignId]`. `directory.items` carries `entry.name`, `entry.aliases` and `mentionCount` for ranking.
    - **Matching.** `entriesCalled(directory, text)` is the exact, case-insensitive name-or-alias check that decides the Create row. `mentionMarkup` writes the stored form. `applyEntrySummary(queryClient, campaignId, summary)` adds a new entry from a response.
    - **The composer.** `aboutPrefill` writes the stored form into `state.text`, so 15d's `fromStoredText` must run on it, or `aboutPrefill` must be switched to `@[Name]` plus a link. The composer's `about` prop and `aboutUsed` event are the seam.
- **15d, as built** (PR on `v2/15d-mention-composer`):
  - **`utils/mentions.ts`** (pure) holds every rule:
    - **Forms.** `MentionText { text, links, newEntries }`. `links` is keyed by the raw bracket text, escapes included. A picked name is written with `escapeMentionText`, which is `mentionMarkup`'s escape set, so `toStoredText` only appends `(entry:<id>)` and `fromStoredText` only drops it. Both are lossless: every stored text round-trips, and so does a second entry under one display text, which stays in the stored form. An escaped `\@[…]` is never a mention.
    - **`mentionBody(state)`** gives `{ text, newEntries }`. It filters `newEntries` with `mentionedEntryIds`, the web's copy of the API parser. So a deleted Create, or one inside code, is never sent, and the API's 400 cannot happen.
    - **The query.** `activeMention(text, caret, links)` gives a `typed` or a `bracket` query. An unclosed `@[Gund` also counts as typed.
    - **Matching.** `matchEntries` folds case, accents and runs of spaces. `mentionSuggestions(query, directory, newEntries, preferIds)` puts this text's pending new entries first, marked `isNew`. It adds Create only when no name, alias or pending name equals the query, and only up to 100 characters (the API's limit). It closes a query that ends in a space and matches nothing.
    - **Edits.** `pickEntry`, `createEntry` and `insertMentionTrigger` (the `@` button; a selection becomes the query) each return the new text, links and caret.
    - **Warnings and errors.** `revealCheck` and `revealMessage` do the reveal warning. `newEntryErrorFrom` sorts the three API errors, and `relinkNewEntry` handles a duplicate.
    - **Drafts and placeholders.** `parseDraft` and `serializeDraft` store the draft as JSON. A 14d string draft still loads, even one that starts with `{`, and unused links are pruned. `pendingEntrySummary` makes the directory placeholder for a new entry.
    - `tests/unit/mentions.test.ts` has 48 tests.
  - **Picking the display text.** A pick writes the entry's name, even when an alias matched. **Addition:** in a bracket query, the entries this text linked before its bracket text was edited (`orphanedLinkIds`) are suggested first. Without that, the §3 override ("pick Gundren, edit the text, pick again") would search for "the old dwarf" and not find Gundren.
  - **`useMentionPicker({ state, textarea, campaignId, enabled? })`** wires the rules to one `<textarea>`:
    - The caret is read on input, keyup, click, select, focus and `selectionchange`.
    - It returns `suggestions`, `highlighted`, `kind`, `creating`, `anchor`, `directory`, `onKeydown`, `choose`, `createAs`, `cycleKind` and `trigger`.
    - Keys: ↑/↓ move and Enter picks. Tab picks an entry, and on a Create row Tab and Shift+Tab cycle the kind. Esc closes the query until another `@` starts. **Deviation:** Enter is left to the text on a touch screen, where Enter is a new line (14d).
    - A phone tap on Create first chooses it and shows `KindChips`. A kind chip then creates the entry.
  - **Where the suggestions show.** `MentionStrip.vue` draws them.
    - On a phone it is chips. In the composer it sits after the `/` strip in the strip area, which the pinned composer keeps above the keyboard. The `/` strip wins while it is open (`enabled`), so the two never show together.
    - With `docked`, it pins itself above the keyboard (`useKeyboardInset`, `bottom: inset`). The note editor uses this, since it is not pinned (invariant 11).
    - From md it is a popover at the caret: `anchor` measures where the query starts using a hidden mirror element. `placement` is `above` for the composer, at the bottom of the screen, and `below` for editors in a scrolling list.
    - Only the composer's toolbar gained the `@` item, first. The note editor has its own `@` button.
  - **Deviation: the "new" chip.** The composer has no preview (14d), so the new component `MentionLinks.vue` shows what the text links to, under the text box. New entries are marked "new", and a click cycles a new entry's kind. The row is hidden while suggestions are open.
  - **Reveal.** `RevealDialog.vue` exposes `confirm(noteVisibility, items) → Promise<boolean>`, which is true to post. "Reveal and post" does the visibility PUTs (`putEntryVisibilityMutation`), then resolves.
    - **Refinement:** it is offered when *any* listed entry can be revealed, and a line names the others, which stay hidden. A failed PUT posts nothing.
    - The note editor checks against the note's real audience, so a hidden `Everyone` note counts as `DM`.
  - **Posting.**
    - `buildPostBody` sends the stored form and `newEntries` (only when there are some).
    - The character count and the limit use the stored length, which is what the API checks.
    - `postNoteMutation` adds the new entries to the directory as placeholders in `onMutate` (`addPendingEntries`, which never replaces a pushed entry), so the optimistic note's chips show at once. On error it removes them (`removePendingEntries`). When the post settles it reads `["entries", id]` again. The POST response carries no entries.
    - `putNoteMutation` adds the placeholders in `onSuccess`.
  - **Errors.**
    - `alreadyCreated` means the earlier try went through. The web shows "That note was already posted.", does not give the text back, and invalidates the streams and entries.
    - `duplicate` gives the text back, relinked to the existing entry, with a toast asking to post again.
    - Any other error keeps the 14d behaviour.
  - **Deviation: `NoteEditor` owns its PUT.** Its props are now `{ campaignId, note, viewer }`, and it emits `saved` and `cancel`, so it can relink after a duplicate 409. `SessionNoteCard.saveEdit` is gone. An unchanged stored text with no new entries saves nothing.
  - **Deviation: `aboutPrefill` moved** from `utils/entries.ts` to `utils/mentions.ts`. It now takes and returns `{ text, links }`, and writes `@[Name]` plus a link. If that display text already links another entry, it writes the stored form.
  - **Verify, as run in 15d** (no browser, so the popover's position, the strip above a real keyboard and the Tab feel were not seen):
    - `dotnet test` passes 171/171. The API is unchanged.
    - `nuxi typecheck` is clean, `nuxt build` succeeds, and `vitest` passes 204/204: the 158 from 15c, minus 3 `aboutPrefill` tests moved out of `entries.test.ts`, plus `mentions` 48 and `composer` +1.
    - `mentions15d.mjs` passed 56/56 against the API on 5010, with a DM and two players on a real `CampaignHub`. Every note in it is written through the web's own `mentions.ts`, `composer.ts` and `composerCommands.ts`. It covers Verify 2.1 to 2.3 and 2.6, plus:
      - the display-text override, and re-picking after it;
      - Reveal and post, and the Me-in-DM warning;
      - the three error paths, with relinking;
      - a Create inside an edit;
      - the draft round trip;
      - the new `aboutPrefill`;
      - that the placeholder matches the pushed entry;
      - that each viewer's push-fed list equals `GET entries`.
    - `pages15d.mjs` passed 19/19 against `nuxt dev`.
    - The earlier scripts pass: `smoke14a`, `hub14b` 28/28, `stream14c` 111/111, `composer14d` 142/142, `filters14e` 233/233, `pages13d`, `entries15a` 30/30, `mentions15b` 44/44, `wiki15c` 64/64 (updated for the moved `aboutPrefill`) and `pages15c` 24/24.
  - **For 15e/15f: `@` outside the composer.** The pieces are independent of the composer:
    ```ts
    const state = reactive<MentionText>({ ...fromStoredText(block.text), newEntries: [] });
    const picker = useMentionPicker({ state, textarea, campaignId });
    // in the textarea's keydown: if (picker.onKeydown(e)) return;
    ```
    ```html
    <div class="relative"> <!-- the popover is placed from the textarea's top-left -->
      <textarea ref="textarea" v-model="state.text" @keydown="…" />
      <ComposerMentionStrip :picker="picker" :listId="id" placement="below" docked />
      <ComposerMentionLinks :state="state" :directory="picker.directory.value" />
    </div>
    <button @mousedown.prevent @click="picker.trigger()">@</button>
    <ComposerRevealDialog ref="reveal" :campaignId="campaignId" />
    ```
    - **Saving.** `mentionBody(state)` gives `{ text, newEntries }`. Before saving, run `revealCheck(blockAudience, text, picker.directory.value, viewer)` through `reveal.confirm(...)`, where a 🔒 block's audience is its visibility. Map errors with `newEntryErrorFrom` and `relinkNewEntry`.
    - **One picker per textarea.** Each article block keeps its own `MentionText`.
    - **Create in articles.** The picker always offers Create. Either `PutEntryArticle` (15e) accepts `newEntries` the way the note endpoints do, or 15f adds an `allowCreate` option to `useMentionPicker`, which is a one-line filter on `suggestions`.
    - **Promote (15f)** copies stored text. Pass it through `fromStoredText` before it is shown in a textarea.
- **15e, as built** (PR on `v2/15e-articles-api`):
  - **Model.** `Article { Blocks }` and `ArticleBlock { Id, Text, Visibility, OwnerMemberId, Quote? }` with `ArticleQuote { NoteId, SessionId, SessionNumber, AuthorMemberId, PromotedByMemberId, PromotedAt }` (`Models/ArticleBlock.cs`). `Entry` gains `Article` and `ArticleMentionIds` (GIN index). `EntryArticleEdited { Actor, Blocks[] }` holds the whole merged article, and `EntryQuotePromoted { Actor, Block }` appends one. `Microseconds.Truncate` keeps `PromotedAt` at Postgres's precision. Limits: `Article.MaxBlocks` 200 and `Article.MaxCharacters` 50,000.
  - **Addition: article events leave `Entry.UpdatedAt` alone.** It is on the summary that everyone who can see the entry receives, so an edit inside a secret block would otherwise show up as a new `updatedAt`. A fifth side channel, next to the four in "Hidden things are absent".
  - **Reads.** `EntryVisibility.CanSeeBlock(entry, block, viewer)` = the entry rule and `Audience.Of(block.Visibility, block.OwnerMemberId)`. `ArticleView.VisibleBlocks` is the one filter every read, etag, push and history uses. `EntryResponse` gains `article: ArticleResponse { etag, blocks: ArticleBlockResponse[] }`, where `ArticleBlockResponse { id, text, visibility, ownerMemberId, quote?: QuoteResponse }`. `EntryResponse.From(entry, viewer)` is now per viewer, so **every** entry write (name, kind, aliases, visibility, edit access, POST) answers with the caller's view. `EntrySummaryResponse` and `entryUpserted` carry no article.
  - **`PUT …/entries/{entryId}/article`** (`PutEntryArticle`), who can edit the entry. Body `{ etag, blocks: { id?, text, visibility }[], newEntries? }`, the caller's whole view after the edit, in order. Answers `EntryResponse`. Errors:
    - 409 `errors.etag[0]` = "Someone else changed this article. Reload and re-apply." when `etag` is not the caller's current view. The body carries no article content.
    - 400 `errors.blocks` for an unknown or unseen id (the same message and key for both) and for an id twice.
    - 403 for a visibility change by someone other than the block's owner or a DM; 403 when the caller cannot edit the entry; 404 when they cannot see it.
    - 400 for more than 200 blocks or 50,000 characters **in the caller's view**. **Deviation:** the limits count only what the caller can see, so they reveal nothing about hidden blocks. The stored article can therefore go past them by the size of what the caller cannot see.
    - `newEntries` as for notes, with the same keys: 400 `errors.newEntries` (id not mentioned in any block), 409 `errors.alreadyCreatedEntryId`, 409 `errors.existingEntryId` + `errors.newEntryId`. The new entry's creator is the caller, `createdFromNoteId` is null, and its visibility is the narrowest level of the entry and every block that mentions it (`PutEntryArticle.NewEntryVisibility`): any `Me` gives `Me`, else any `DM` gives `DM`, else `Everyone`. Because the caller can see the entry and all those blocks, that level relative to the caller is inside every one of those audiences, which also covers "DM owned by one member, Me by another".
    - Text is trimmed, a blank block is removed, and `quote` cannot be sent (unknown JSON fields are ignored): a quote keeps its source, while its text can be trimmed. An unchanged view appends and pushes nothing.
  - **Merge** (`ArticleMerge.Merge`, pure): hidden blocks go back after the nearest earlier stored block still present, or first. A reorder carries a hidden block with its neighbour.
  - **Etag** (`ArticleEtag`): the first 16 bytes of SHA-256 over the visible blocks' id, visibility and length-prefixed text, in order, as 32 hex characters. Two viewers with the same view share an etag.
  - **Races.** The write uses `FetchForWriting<Entry>`. On a `ConcurrencyException` the session's pending changes are ejected and the whole check (visibility, permission, etag, merge, new entries) runs once more, so a race against a change the caller cannot see succeeds. **Deviation (small):** losing the race twice is a 409 even when the caller's view did not change. `InterferingSaveFixture` (a Marten listener that commits a write just before the endpoint's save) makes both paths deterministic in the tests. Promote appends with no version check: it commutes with other promotes, and an article edit racing it fails its own check and sees the quote.
  - **`POST …/entries/{entryId}/quotes`** (`PostEntryQuote`), who can edit the entry. Body `{ noteId, text? }`. Answers **`EntryQuoteResponse { entry: EntryResponse, blockId }`** (addition: `blockId` is the new quote, which 15f's phone flow opens in the editor; the caller can always see it). The note must be visible to the caller in this campaign (404 otherwise, the same as a missing note). `text` left out quotes the whole note. Otherwise, with whitespace runs collapsed on both sides, it must be part of the note's text, or it is 400 `errors.text[0]` = "A quote must be text from the note." The stored text is `text` trimmed, in the note's stored (markdown) form. The block is owned by the note's author, with `NewEntries.VisibilityFrom(note)` (a hidden `Everyone` note gives `DM`). The limits are checked on the caller's view plus the quote.
  - **Push.** `entryArticleChanged { entryId }` (`EntryArticleChangedMessage`) to `member:{id}` for each campaign member who can see the entry and whose etag changed (`ArticleAudience.WhoseViewChanged`), in one send. No `entryUpserted`. With new entries, `entryUpserted` for each goes first, so a refetched article's chips resolve.
  - **Mentions in articles.** **Deviation (from "nothing in 15 draws it", per the 15 plan):** article mentions feed the counts and the timeline, filtered per viewer.
    - `MentionIndex.BlocksMentioning(session, campaignId, entryIds, viewer, ct)` → `BlockMention(Entry, Block)[]`, by `?|` on `ArticleMentionIds` (the 15b fragment, now generic over the field) and filtered with `CanSeeBlock`. An article's mentions of its own entry are left out.
    - `CountsFor` adds one per visible block that mentions the entry. `MentionCount.LastMentionedAt` is now nullable: blocks have no time, so it stays the latest note's `postedAt`, and it is null when only articles mention the entry.
    - `EntryTimelineResponse` gains `articleMentions: { entryId, blockIds[] }[]` (every page, by entry name). The timeline's `items` are still notes only (glossary: Timeline).
  - **History seam for 15g.** `ArticleHistory.For(entry, versions, viewer)` over `ArticleVersion(At, Actor, Blocks)` keeps only the visible blocks of each version and drops versions whose visible etag did not change. `SecretBlockTests` rebuilds the versions from the event stream the way 15g's endpoint will.
  - **Known gap.** A quote keeps the audience its note had when it was promoted. If the author later narrows or deletes the note, or a DM hides it, the quote stays. Its owner (the author) and the DMs can narrow it with `PUT article`. A live link from quote to note would need a cross-stream update, and is left for later.
  - **Verify, as run in 15e** (API only, so no browser):
    - `dotnet test` passes 271/271 on three runs: the 171 from 15d plus `ArticleMergeTests` 26, `ArticleEtagTests` 8, `ArticleTests` 17, `SecretBlockTests` 36 (27 table cases), `PromoteTests` 11 and `ArticleMentionTests` 2.
    - `nuxi typecheck` is clean, `vitest` passes 204/204 (`entryCache.test.ts` fixtures gained the new required fields, and a check that `mergeEntrySummary` keeps the article), `nuxt build` succeeds, and `schema.d.ts` is regenerated.
    - `articles15e.mjs` passed 35/35 against the API on 5010, with a DM and two players on a real `CampaignHub`: who is pinged for each edit and promote, no trace of a DM secret or DM quote in the other players' reads or pushes, a stale etag that still saves, a DM conflict with no content, the hidden-id 400, the owner rule, both promote paths, a Me-block entry, and per-viewer article counts and timeline.
    - The earlier scripts pass: `smoke14a`, `hub14b`, `stream14c` 111/111, `composer14d` 142/142, `filters14e` 233/233, `pages13d`, `entries15a`, `mentions15b` 44/44, `wiki15c` 64/64, `mentions15d` 56/56, `pages15c` 24/24 and `pages15d` 19/19.
    - Verify 3: every `entry_*` row in the dev database's `mt_events`, including `entry_article_edited` and `entry_quote_promoted`, has a `correlation_id` and an `Actor`.
  - **For 15f.**
    - Load with `GET entries/{id}`: `article.blocks` is the reader's view, and `article.etag` goes back with the save. Save by `PUT article` with every block the editor shows, in order: `id` for existing blocks, no `id` for new ones. Send `newEntries` from each block's `MentionText` (`mentionBody`), merged. Adjacent ordinary blocks can be joined by sending one block with the first id and dropping the other. After a 200, replace the loaded entry with the response.
    - A 409 with `errors.etag` is the conflict: show the user's text, then `GET` again. `newEntryErrorFrom` reads the three `newEntries` errors unchanged.
    - Only offer 🔒 on blocks whose `ownerMemberId` is the viewer, or to DMs (anything else is a 403). An ordinary block is `Everyone` with no `quote`.
    - Hub: on `entryArticleChanged { entryId }`, invalidate `["entry", campaignId, entryId]`, or show "This article changed" while its editor is open. `mergeEntrySummary` already keeps the loaded article on `entryUpserted`.
    - Promote: `POST quotes { noteId, text? }` → `{ entry, blockId }`. Send the note's stored text for a selection (`utils/promote.ts` maps it). A quote links with `quote.noteId`, `quote.sessionNumber` and `quote.authorMemberId`.
- **15f, as built** (PR on `v2/15f-article-editor`):
  - **Pure logic.**
    - `utils/article.ts`: `EditorBlock { key, id | null, visibility, ownerMemberId, quote, mention: MentionText }`, `toEditorBlocks`, `newBlock`, `moveBlock`, `removeBlock`, `setBlockVisibility`, `wrapSecret`, `articleSaveBody`, `articleUnchanged`, `editedBlocks`, `relinkArticleNewEntry`, `articleRevealCheck`, `quotesNote`, `blockKind`, `secretLabel`, `secretAudience`, `canChangeBlockVisibility`, `EDIT_BLOCK_PARAM` and `entryHref`.
    - `utils/promote.ts`: `plainView`, `normalizeForMatch`, `selectionToSource`, `isExcerptOf`, `promoteTargets` and `noteAudience`.
    - `tests/unit/article.test.ts` has 27 tests and `promote.test.ts` 17.
  - **Reading** (`WikiArticle`, `WikiArticleBlock`). Blocks draw through `NoteMarkdown` with the new `document` prop, which turns 14c's heading rule off (`NoteMarkdownEnv.document`). A secret block has a dashed gold frame labelled 🔒 DM or 🔒 Me, with "Visible to the DMs and Sam" as its title. A quote is a blockquote with "— Sam, Session 12 ↗" linking to `?note=`. A quote of a 🔒 note gets both. An empty article reads "Nothing written yet", with Edit for editors.
  - **Editing** (`WikiArticleEditor`, one `WikiArticleBlockEditor` per block).
    - Each block has its own `MentionText` and `useMentionPicker`, 15d's `ComposerMentionLinks`, and `ComposerToolbar` with `@`, B, I, List and 🔒. `ComposerToolbar` gained `showPost` (the ➤ button is off here).
    - From md, the toolbar sits under every block and `@` is the popover. On a phone, only the focused block shows its toolbar, docked above the keyboard (`useKeyboardInset`) with the mention strip inside it, and the editor sets `useComposerPinned` so the tab bar hides (invariant 11).
    - Blocks move up and down and can be deleted. "+ Text" and "+ 🔒 Secret" add blocks. ⌘/Ctrl+Enter saves. Cancel asks "Discard changes?" once when something changed.
  - **🔒 rules** (`wrapSecret`). 🔒 is offered on ordinary blocks only. Secret blocks and quotes change visibility through `SecretBlockPicker` (Ordinary or Everyone, 🔒 DM, 🔒 Me), which shows only to the block's owner and the DMs.
    - With a selection, the block splits into three, and the middle becomes the viewer's new 🔒 DM block. A selection never cuts a mention in two. The first non-blank ordinary part keeps the block's id, and the others are new blocks the viewer owns. Each part keeps only the links and new entries it uses.
    - **Deviation:** without a selection, the owner or a DM makes the block secret in place. Anyone else gets the block back as a new secret block they own, with the id dropped. That is the same as selecting all of it, so it is never a 403.
  - **Saving.** `articleSaveBody` sends every block in order, with `id` only for existing ones and the stored text. It leaves blank blocks out, joins adjacent ordinary blocks under the first id in the run, and merges each block's new entries. An unchanged article saves nothing. The reveal check runs per block, where a block's readers are the entry's audience narrowed by the block's visibility. `RevealDialog` gained `verb="save"`. After a 200 the response replaces the loaded entry, and loaded timelines and the list are read again (article mentions).
    - **Known edge:** joining two ordinary blocks that were separate on the server moves the second one's text above any hidden block between them, because the merge re-inserts a hidden block after the block it followed (15e). The viewer cannot know. Nothing leaks.
  - **Conflicts.**
    - A 409 `errors.etag`, and also a 400 `errors.blocks` (a block removed or hidden meanwhile), open `ConflictDialog`. It lists `editedBlocks`, meaning the viewer's new or changed blocks. Copy takes the stored form, so pasted mentions stay linked. However the dialog closes, the editor then reloads from a fresh `GET`.
    - A 403 shows a toast. The `newEntries` errors are handled as in `NoteEditor`.
  - **Pushes.** `entryArticleChanged` calls `applyEntryArticleChanged`, which invalidates `["entry", campaignId, entryId]` and the loaded timelines. There is no editor registry. The editor keeps its own snapshot (`base`) and watches the loaded entry's etag. When it changes while not saving, it shows "This article changed while you were editing" with Reload, which goes through `ConflictDialog` when the viewer changed something. The viewer's own save takes the response as its new snapshot first, so its own push is not a change.
  - **Promote.**
    - `NoteAction` gains `promote` ("Promote to wiki"), first in the menu and the sheet, for anyone who can see the note, except while it is pending. The sheet runs it after closing.
    - `SessionNoteCard` opens `WikiPromoteDialog` lazily, like its history dialog. On a timeline it takes `promoteEntryId`: its only action is then `promote`, and long-press is off. `EntryTimeline` draws `[Promote]` through the actions slot, for editors of the entry.
    - `WikiPromoteSelection` (addition, one per page, in `SessionStream` and `EntryTimeline`) shows "Add to wiki" under a selection inside one note's `.note-markdown`, on desktop with a fine pointer only. It maps the selection through `selectionToSource`.
    - `selectionToSource`: both sides are compared with whitespace runs collapsed, ignoring `*`, `_`, `~`, backticks, backslashes, zero-width joiners and the kind icons that chips draw. So `snake_case`, escapes and emphasis match either way. Markup at the very edges of a selection is dropped, the first occurrence wins, and the result is always a real slice of the source, widened to whole mentions and links.
    - `WikiPromoteDialog` has `WikiEntryPicker` (`promoteTargets`: 15d's matching over the entries the viewer can edit, plus Create "…" when no visible entry has that name or alias). Create posts the entry first, with the note's audience (`noteAudience`: a hidden `Everyone` note gives `DM`), then promotes into it. These are two requests: a failed promote leaves the new entry, empty.
    - The dialog also has the quote's text box, checked with `isExcerptOf`. It says "Players won't see this quote" for `DM`, `Me` and hidden notes, and warns when the picked entry's article already quotes the note (`quotesNote`, read with `getEntryQuery`). An untouched whole note is sent without `text`.
    - On a phone the dialog has no text box: the whole note goes in, and it navigates to `/wiki/{entryId}?edit={blockId}`. The entry page uses the parameter once to open the editor at that block, then drops it. On desktop, a toast links to the entry. The dialog sits at the top on a phone, and its height stops above the keyboard.
  - **Addition: the timeline draws 15e's `articleMentions`** as "Also mentioned in the articles of" plus links, resolved through the directory.
  - **Script fix:** `composer14d.mjs`'s four exact `noteActionsFor` lists now start with `promote`.
  - **Verify, as run in 15f** (no browser, so the docked toolbar, the selection button's position and the phone flow were not seen):
    - `dotnet test` passes 271/271. The API is unchanged, so `schema.d.ts` is unchanged.
    - `nuxi typecheck` is clean and `nuxt build` succeeds. `vitest` passes 249/249: the 204 from before plus `article` 27, `promote` 17 and `markdown` 1. `noteActions`' lists gained `promote`.
    - `article15f.mjs` passed 64/64 against the API on 5010, with a DM and two players on a real `CampaignHub`. Every article in it is built through the web's own `article.ts`, `promote.ts` and `mentions.ts`. It covers:
      - the first block, a 🔒 split by the DM and the pings for it, and an edit inside the secret that pings only the DM;
      - a player's save that keeps the DM's secret in place, and the join under the first id;
      - 🔒 on another member's whole block (a new 🔒 Me block, 200, hidden from the others), and the 403 that the web never offers;
      - the stale-save 409 with `editedBlocks`, then reload and re-apply, and the 400 `errors.blocks`;
      - a rendered-chip selection mapped to source and promoted, the quote's link fields, a whole-note promote trimmed in the editor, and a rewrite refused;
      - a 🔒 DM note's quote, which pings only the DM and is absent for players, and the 404 on an unseen note;
      - `@` Create inside a block, and the `articleMentions` it adds;
      - the reveal check, `promoteTargets`, and Promote first in the note actions;
      - an unchanged save that is a no-op for every viewer.
    - `pages15f.mjs` passed 34/34 against `nuxt dev`, including the entry page with `?edit=`.
    - The earlier scripts pass: `smoke14a`, `hub14b` 28/28, `stream14c` 111/111, `composer14d` 142/142 (after the fix above), `filters14e` 233/233, `pages13d`, `entries15a` 30/30, `mentions15b` 44/44, `wiki15c` 64/64, `mentions15d` 56/56, `pages15c` 24/24, `pages15d` 19/19 and `articles15e` 35/35.
  - **For 15g.**
    - **The entry page** is `pages/app/campaigns/[campaignId]/wiki/[entryId].vue`. From top to bottom:
      - the header, `WikiEntryHeader`, which swaps for `WikiEntryHeaderEditor` while `editing`;
      - the article, `WikiArticle`, which swaps for `WikiArticleEditor` while `articleEditing`. `openArticleEditor(blockId?)` opens it, and `?edit=` does too;
      - `WikiEntryTimeline`, then "Add a note about…".
    - Permissions come from `canEdit` and `canChangeAccess` there, and names from `memberName`.
    - **Header actions.** `EntryHeader.vue` has one `[Edit]` button that emits `edit`. Merge, claim and history belong beside it, as a menu or buttons. `ClaimControl` and `StatsEditor` fit under the header.
    - **Restore.** Build the version's blocks with `toEditorBlocks`, then save with `articleSaveBody(currentEtag, …)` through `putEntryArticleMutation`. A version's block ids that no longer exist would be a 400 `errors.blocks`, so send those blocks without an id (new blocks the restorer owns). Hidden blocks survive through the server's merge. Opening `WikiArticleEditor` on the restored blocks would need a `blocks` prop. Today it starts from `entry.article`.
    - **The hub.** `applyEntryArticleChanged` is in `utils/queries/entries.ts`, next to where `entryMerged` goes. `entryHref(campaignId, entryId)` builds entry links. `EntryPicker` and `promoteTargets` are ready for `MergeDialog`, which should drop the Create row.
