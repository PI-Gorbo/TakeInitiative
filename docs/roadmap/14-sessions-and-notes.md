# 14 — Sessions and session notes

## Goal

The Campaign tab becomes the **session stream**: every session, separated by
session dividers, opening at the current session. Any member writes markdown
session notes in the **composer**, picks the session and the visibility, marks
recaps, and posts back in time. Notes arrive live over `CampaignHub`, and a note
reaches only the members allowed to see it, on every read and every push
(invariant 5). The author edits (with history) and deletes their notes, a DM
hides any note, and the stream filters by All · Text · Images · Recaps · Combats
· Mine. On a phone the composer toolbar sits just above the keyboard.

The step ships as five PRs stacked with `gh stack` on top of this file's docs
PR, which sits on 13d (#199). Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 14a | `v2/14a-session-model` | Session and note model | 13's app unchanged in the browser. The API has sessions and notes, and a new campaign has Session 1 | [x] |
| 14b | `v2/14b-live-notes` | Visibility-aware push | The same, and note and session changes are pushed only to the groups allowed to see them | [x] |
| 14c | `v2/14c-session-stream` | Stream (read and live) | The Campaign tab shows the session stream, live. Members and the join code move to a panel | [x] |
| 14d | `v2/14d-composer` | Composer and note actions | Post, edit, delete, hide, history, back-posting and the gap prompt, on every screen size | [x] |
| 14e | `v2/14e-mobile-composer` | Mobile composer, commands, filters | The step's Verify passes | [x] |

`@` mentions are step 15 and images are step 16. This step leaves a seam for each
(Notes) and builds neither.

## Depends on

**13**. The glossary (§1), the sessions and composer UX (§3, §3a), the architecture
(§9) and the invariants (§10) in [12-v2-design-session.md](12-v2-design-session.md)
are binding.

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web).

**This PR (docs)**
- `docs/roadmap/12-v2-design-session.md` §1: the new nouns (step 0 below)
- `docs/roadmap/README.md`: link step 14, `in progress`

**14a: add**
- API `src/Features/Campaigns/Models/Visibility.cs` (`Everyone | DM | Me`, shared with entries in step 15)
- API `src/Features/Campaigns/CampaignAccess.cs`: `RequireMember` for endpoints (see 14a step 3)
- API `src/Features/Sessions/Models/Session.cs`, `SessionNote.cs`, `SessionNoteVisibility.cs`, `SessionGap.cs`
- API `src/Features/Sessions/Models/Events/{SessionStarted,SessionTitleChanged}.cs`
- API `src/Features/Sessions/Models/Events/{SessionNotePosted,SessionNoteEdited,SessionNoteVisibilityChanged,SessionNoteHidden,SessionNoteUnhidden,SessionNoteDeleted}.cs`
- API `src/Features/Sessions/Api/{GetSessions,PostStartSession,PutSessionTitle,GetSessionStream}/**`
- API `src/Features/Sessions/Api/{PostSessionNote,GetSessionNote,PutSessionNote,PutSessionNoteVisibility,PutSessionNoteHidden,DeleteSessionNote,GetSessionNoteHistory}/**`
- Tests `Scopes/Integration/Features/Sessions/{SessionTests,SessionNoteTests,SessionNoteVisibilityTests}.cs`

**14a: modify**
- API `src/boostrap/Bootstrap.cs`: register the two projections and their indexes
- API `src/Features/Campaigns/Api/PostCreateCampaign/PostCreateCampaign.cs`: start Session 1 in the same save
- Tests `Scopes/Integration/WebAppClientExtensions.cs`: typed calls for the new endpoints
- Web `utils/api/schema.d.ts`: regenerated

**14b**
- API add `src/Features/Sessions/SessionHub.cs`: `SessionNoteAudience` and the `IHubContext<CampaignHub>` notify extensions
- API modify `src/Features/Campaigns/CampaignHub.cs`: the new message names in `CampaignHubMessages`
- API modify every 14a write endpoint: notify after `SaveChangesAsync`
- Tests add `Scopes/Unit/SessionNoteAudienceTests.cs`, `Scopes/Integration/Features/Sessions/SessionHubTests.cs`

**14c**
- Web `package.json`: add `markdown-it` and `@types/markdown-it`
- Web add `utils/markdown.ts`, `utils/sessionStreamCache.ts`, `utils/queries/sessions.ts`
- Web add `utils/api/session/*.ts`, `utils/api/sessionNote/*.ts`; modify `composables/useApi.ts`, `utils/api/types.ts`
- Web add `components/Session/{SessionStream,SessionDivider,SessionNoteCard,NoteMarkdown}.vue`
- Web add `components/Campaign/MembersPanel.vue`: the 13d members and join code UI, moved out of the page
- Web modify `pages/app/campaigns/[campaignId]/index.vue`: the stream
- Web modify `composables/useCampaignHub.ts`: the session and note handlers

**14d**
- Web add `components/Composer/{Composer,SessionPicker,VisibilityPicker,ComposerToolbar,GapPrompt}.vue`
- Web add `components/Session/{NoteActions,NoteEditor,NoteHistoryDialog}.vue`
- Web modify `components/Session/SessionNoteCard.vue`, `pages/app/campaigns/[campaignId]/index.vue`, `utils/queries/sessions.ts`

**14e**
- Web add `composables/{useKeyboardInset,useComposerCommands,useLongPress}.ts`
- Web add `components/Session/{StreamFilters,NoteActionSheet}.vue`
- Web modify `components/Composer/*`, `components/Session/SessionStream.vue`, `layouts/campaign.vue` (hide the tab bar while the composer has focus on a phone)

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/13d-pwa-shell
gh stack add v2/14-step-file
```

Add each sub-step on top with `gh stack add v2/14a-session-model` and so on.

**Glossary check.** Session, Session note, Recap, Visibility and Member are in §1.
This PR adds the nouns the step puts into code and UI:

- **Current session**: the latest session. The composer posts to it unless
  another is picked.
- **Session stream**: the Campaign tab's list of every session's notes, split by
  session dividers.
- **Session divider**: the line that starts a session in the stream: number, date
  and title.
- **Composer**: where a session note is written: the text, the session and
  visibility pickers, and the toolbar.
- **Gap prompt**: the composer's "Start Session N?" suggestion.
- **Added later**: the marker on a note posted to a session that was no longer
  current.
- **Hide**: a DM hides a session note from everyone except its author and the
  DMs. It is not a delete.
- **Edit history**: the earlier versions of an edited session note.
- **Filter**: one of `All · Text · Images · Recaps · Combats · Mine` on the
  session stream. Not a channel.

### 14a. Session and note model

1. **Two aggregates, one stream each** (§9). Stream id = aggregate id. Every event
   implements `IActorEvent`.

   ```
   Visibility                    Everyone | DM | Me        // JsonStringEnumConverter, stored as a string

   // Session stream
   SessionStarted                { Actor, CampaignId, Number }
   SessionTitleChanged           { Actor, Title? }          // null clears it

   Session (inline projection)
     { Id, CampaignId, Number, Title?, StartedAt, StartedByMemberId }

   // SessionNote stream
   SessionNotePosted             { Actor, CampaignId, SessionId, AuthorMemberId,
                                   Text, Visibility, IsRecap, AddedLater }
   SessionNoteEdited             { Actor, Text, IsRecap }
   SessionNoteVisibilityChanged  { Actor, Visibility }
   SessionNoteHidden             { Actor }
   SessionNoteUnhidden           { Actor }
   SessionNoteDeleted            { Actor }                  // the projection deletes the document

   SessionNote (inline projection)
     { Id, CampaignId, SessionId, AuthorMemberId, Text, Visibility, IsRecap,
       PostedAt, AddedLater, EditedAt?, IsHidden, HiddenByMemberId?, HiddenAt? }
   ```

   - `AuthorMemberId` is on the event, not read from `Actor`, so the author stays
     a member when `Actor` gains a model case (§11a).
   - `AddedLater` is decided when the note is posted (the session was not the
     current one then) and never recomputed, because "current" moves.
   - Hidden state is three flat fields rather than a nested object so the
     visibility filter below stays a simple Marten LINQ expression.
   - Deletion uses the `ShouldDelete(SessionNoteDeleted)` convention on the
     self-aggregating `SessionNote`. If `Snapshot<T>` ignores it on Marten 7.31,
     use a `SingleStreamProjection<SessionNote>` with `DeleteEvent<SessionNoteDeleted>()`.
     The stream keeps every event either way.
2. **Registration and indexes** (`Bootstrap.AddMartenDB`):
   - `opts.Projections.Snapshot<Session>(SnapshotLifecycle.Inline)` and the same
     for `SessionNote`.
   - `Session`: a unique computed index on `(CampaignId, Number)`. It is the
     backstop when two members start the next session at once.
   - `SessionNote`: an index on `(CampaignId, PostedAt)` and one on `SessionId`.
3. **`CampaignAccess.RequireMember(session, campaignId, userId)`** returns
   `(Campaign, Member)` or throws 404 / 403, replacing the load-then-check block
   13b repeats in every endpoint. The 13b endpoints may adopt it; nothing else in
   them changes.
4. **The visibility rule lives in one place** (invariant 5):
   `SessionNoteVisibility.VisibleTo(Member viewer)` returns an
   `Expression<Func<SessionNote, bool>>` that every note query uses, plus an
   in-memory `CanSee(note, viewer)` for single loads and the hub. A viewer sees a
   note when:

   | Note | Author | DM | Other player |
   |---|---|---|---|
   | `Everyone` | yes | yes | yes |
   | `Everyone`, hidden | yes, marked hidden | yes, marked hidden | **no** |
   | `DM` | yes | yes | **no** |
   | `Me` | yes | **no** | **no** |

   A note the caller cannot see is a **404**, never a 403, so its existence does
   not leak ("hidden things are absent").
5. **Current session and Session 1.** The current session is the campaign's
   `Session` with the highest `Number`. `PostCreateCampaign` starts the Campaign
   stream and the Session 1 stream in the same `SaveChangesAsync`, so they share
   a transaction and a correlation id. `SessionStarted` for Session 1 carries the
   owner as its `Actor`.
6. **Gap prompt rule** (`SessionGap`, one constant of 3 days, reused by the
   Discord import in step 24). `SuggestNextSession` is true when the current
   session has at least one note the caller can see and the newest such note in
   the campaign is more than 3 days old. An empty current session never
   suggests a new one: it is waiting to be used. Only notes the caller can see
   count, so the prompt leaks nothing.
7. **Endpoints.** All take `{campaignId}` in the route and resolve the caller's
   member first. Operation ids are the class names (13c).

   | Endpoint | Who | Does |
   |---|---|---|
   | `GET /api/campaigns/{campaignId}/sessions` | members | `{ sessions[] (newest first), currentSessionId, suggestNextSession }` |
   | `POST /api/campaigns/{campaignId}/sessions` | members | body `{ number }`, the number the caller expects to start. `current + 1` appends `SessionStarted`. `current` returns the existing session (someone else just started it). Anything else is 409 |
   | `PUT /api/campaigns/{campaignId}/sessions/{sessionId}/title` | DMs | appends `SessionTitleChanged`. Unchanged title appends nothing |
   | `GET /api/campaigns/{campaignId}/stream?filter=&before=&take=` | members | a `SessionStream` page (below) |
   | `POST /api/campaigns/{campaignId}/notes` | members | body `{ sessionId?, text, visibility, isRecap }`. No `sessionId` means the current session. Appends `SessionNotePosted` with `AddedLater = sessionId is not current` |
   | `GET /api/campaigns/{campaignId}/notes/{noteId}` | who can see it | one note plus its session number (deep links) |
   | `PUT /api/campaigns/{campaignId}/notes/{noteId}` | author | body `{ text, isRecap }`. Appends `SessionNoteEdited`. Unchanged appends nothing |
   | `PUT /api/campaigns/{campaignId}/notes/{noteId}/visibility` | author | appends `SessionNoteVisibilityChanged` |
   | `PUT /api/campaigns/{campaignId}/notes/{noteId}/hidden` | DMs | body `{ hidden }`. Appends `SessionNoteHidden` / `SessionNoteUnhidden`; a no-op appends nothing |
   | `DELETE /api/campaigns/{campaignId}/notes/{noteId}` | author | appends `SessionNoteDeleted` |
   | `GET /api/campaigns/{campaignId}/notes/{noteId}/history` | who can see it | `{ versions[] { text, isRecap, at } }`, oldest first, read from the stream (`SessionNotePosted` and each `SessionNoteEdited`) |

   Responses:

   ```
   SessionResponse     { id, number, title?, startedAt, startedByMemberId, isCurrent }
   SessionNoteResponse { id, sessionId, authorMemberId, text, visibility, isRecap,
                         postedAt, addedLater, editedAt?, isHidden, hiddenByMemberId? }
   SessionStreamResponse
     { sessions[] { session: SessionResponse, notes[]: SessionNoteResponse },  // oldest first
       currentSessionId, suggestNextSession, hasOlder }
   ```

   - **Stream paging** is by session: up to `take` (default 3, max 10) sessions
     with `Number < before` (default: everything, so the first page ends at the
     current session). Each session carries all of its notes the caller can see
     that match the filter. Notes are ordered by `PostedAt`, so a back-posted note
     lands at the end of its session. Recaps are ordered the same way; the web
     lifts them under the divider.
   - **`filter`** is applied on the server so paging stays correct: `All`;
     `Text` (notes with no images, which in 14 is every note); `Images` (none
     until step 16); `Recaps` (`IsRecap`); `Combats` (none until step 18);
     `Mine` (`AuthorMemberId` is the caller). Sessions are returned even when no
     note matches, and the web decides whether to draw their dividers.
   - **Responses carry no per-viewer fields.** The web compares `authorMemberId`
     with the campaign's `currentMemberId`. That lets 14b push one payload to
     every allowed group.
   - **Validation.** Text is trimmed, required, and at most 10,000 characters.
     A title is at most 100 characters. `visibility` must be in the enum. A
     `sessionId` from another campaign is a 404.
   - **Edits are last-write-wins.** Only the author can edit, so a conflict means
     the same person on two devices. No expected version.
   - The text is stored verbatim. Nothing parses it in 14 (see Notes: the `@`
     seam).
8. **Tests.** Use `Users.DM` (owner), `Users.Player`, and `Users.Outsider` joined by
   code as a second player, all on a campaign created inside the test.
   - `SessionTests`: a new campaign has Session 1 as its current session, and its
     `SessionStarted` shares the `CampaignCreated` correlation id; any member
     starts the next session; starting `current` again returns it without
     appending; starting `current + 2` is 409; only a DM changes the title; an
     outsider gets 403 everywhere; the gap prompt is off for an empty current
     session and for a note from today, and on for a note older than 3 days
     (`SessionGap` reads the clock from an injected `TimeProvider`, and the test
     host swaps in a `FakeTimeProvider` advanced 4 days); every event carries an `Actor` and a correlation id.
   - `SessionNoteTests`: posting without a session goes to the current one;
     posting to an older session sets `addedLater`; only the author edits,
     changes visibility and deletes (others get 403, or 404 when they cannot see
     it); an edit sets `editedAt` and the history lists both versions; a delete
     removes the note from the stream and history returns 404; only a DM hides
     and unhides; hiding twice appends one event; text validation.
   - `SessionNoteVisibilityTests`: one test per cell of the table in step 4, on
     `GET stream`, `GET notes/{id}` and `GET notes/{id}/history`; the `Mine` and
     `Recaps` filters; the gap prompt ignores a note the caller cannot see.
9. **Web.** Regenerate `schema.d.ts` (`pnpm gen:api`) and commit it. No UI change.

### 14b. Visibility-aware push

1. **Messages** (added to `CampaignHubMessages`; camelCase on the wire):

   | Message | Payload | Sent to |
   |---|---|---|
   | `sessionStarted` | `SessionResponse` | `campaign:{id}` |
   | `sessionTitleChanged` | `SessionResponse` | `campaign:{id}` |
   | `sessionNoteUpserted` | `SessionNoteResponse` | the note's audience after the change |
   | `sessionNoteRemoved` | `{ noteId, sessionId }` | the note's audience before the change |
   | `sessionNoteHidden` | `{ noteId, sessionId, byMemberId }` | `member:{authorId}` only, so the author is told |

2. **Audience** (`SessionNoteAudience.Groups(note, campaignId)`), the push-side twin
   of the read rule in 14a:

   | Note | Groups |
   |---|---|
   | `Everyone`, not hidden | `campaign:{id}` |
   | `Everyone`, hidden | `campaign:{id}:dm`, `member:{authorId}` |
   | `DM` | `campaign:{id}:dm`, `member:{authorId}` |
   | `Me` | `member:{authorId}` |

   Send with `Clients.Groups([...])`. The default lifetime manager sends once per
   connection even when a connection is in several of the groups (a DM author).
   The web handlers are idempotent upserts anyway.
3. **Routing per write.** Each endpoint notifies after `SaveChangesAsync`, like
   13b's `NotifyMemberRoleChanged`:
   - post, edit, unhide: `sessionNoteUpserted` to the audience after.
   - visibility change and hide: `sessionNoteRemoved` to the audience **before**,
     then `sessionNoteUpserted` to the audience **after**. Messages on one
     connection arrive in order, so a member who can still see the note ends with
     it, and one who lost access ends without it. The removal goes only to groups
     that could already see the note, so its id leaks nothing new. Hide also sends
     `sessionNoteHidden` to the author.
   - delete: `sessionNoteRemoved` to the audience before.
   - `sessionStarted` and `sessionTitleChanged` to the campaign group.
4. **Role changes** need no new server work: 13b already moves the member's
   connections in or out of `campaign:{id}:dm`. The web refetches the stream when
   `memberRoleChanged` names the caller (14c), which picks up or drops DM notes.
5. **Tests.**
   - `SessionNoteAudienceTests` (unit): every row of the audience table, and a
     property check that for every visibility × hidden × role × is-author case,
     `CanSee` (14a) is true exactly when the viewer is in one of `Groups`. This is
     the test that keeps the read and push rules from drifting apart.
   - `SessionHubTests` (integration): with a recording `IHubContext<CampaignHub>`
     swapped into the host (`ConfigureTestServices`), assert the message names,
     groups and order for post, edit, visibility change, hide, unhide and delete,
     and that a `DM` note is never sent to `campaign:{id}`. If a real SignalR
     client over the Alba `TestServer` proves easy, add one end-to-end case: a
     Player connection does not receive a DM's `DM` note.
6. No web change. The API is ahead of the UI until 14c.

### 14c. Session stream (read and live)

1. **Markdown.** Add `markdown-it`. `utils/markdown.ts` configures it with
   `html: false` (raw HTML is escaped, which is the XSS guard), `linkify: true`,
   `breaks: true`, and links opened with `target="_blank" rel="noopener noreferrer nofollow"`.
   Headings render as bold paragraphs: a note is a chat line, not a document.
   `NoteMarkdown.vue` is the only place that uses `v-html`, and only with this
   renderer's output.
2. **Requests and queries.** `utils/api/session/*` and `utils/api/sessionNote/*` on
   the generated types, grouped as `useApi().session` and `useApi().note`. Aliases in
   `utils/api/types.ts`: `Visibility`, `Session`, `SessionNote`, `SessionStream`.
   `utils/queries/sessions.ts` has `getSessionStreamQuery(campaignId, filter)` as a
   `useInfiniteQuery` whose next page is `before = oldest loaded session number`.
3. **Cache updates in one place.** `utils/sessionStreamCache.ts` holds pure functions
   over the infinite query's pages: `upsertSession`, `upsertNote` (placed by
   `sessionId` and `postedAt`, a no-op when the note's session is not loaded yet),
   `removeNote`. Hub handlers (here) and mutations (14d) both use them. Under a
   filter other than `All`, an upserted note that no longer matches is removed.
4. **Stream UI.** Chronological, newest at the bottom, like Discord. It opens
   scrolled to the bottom of the current session and loads older sessions when
   scrolled to the top, keeping the scroll position.
   - `SessionDivider`: "Session 12 · Sat 20 Sep · The Triboar Trail". The date is
     `startedAt` in the viewer's time zone. DMs get an inline title edit.
   - Recaps sit directly under their divider, marked 📜 RECAP.
   - `SessionNoteCard`: author, time, the visibility badge for 🔒 DM and 🔒 Me,
     "(edited)", "added N days later" (whole days from the session's `startedAt`,
     "added later" under a day), and "Hidden by a DM" for a hidden note.
   - New notes from others while scrolled up show a "New notes ↓" pill instead of
     jumping.
5. **Members panel.** The 13d members list and join code move into
   `MembersPanel.vue`, opened from a members button in the Campaign tab (a sheet
   on a phone). The page is now the stream.
6. **Live.** `useCampaignHub` gains handlers for the five 14b messages that call
   the cache functions (returning nothing, per the 13d gotcha). `sessionNoteHidden`
   shows a toast to the author: "A DM hid your note. You can still see it." On
   `memberRoleChanged` for the caller, and on reconnect, the stream query is
   invalidated.

### 14d. Composer and note actions

1. **Composer** (one component, sticky at the bottom of the stream):
   - "Posting to: Session 13 ▾" (`SessionPicker`, every session, newest first; an
     older one shows "added later" in the picker) and "Visible to: Everyone ▾"
     (`VisibilityPicker`: Everyone, 🔒 DM, 🔒 Me).
   - A plain `<textarea>` that grows with its content. Enter posts on desktop and
     Shift+Enter is a new line; on a phone Enter is a new line and ➤ posts.
   - `ComposerToolbar` below the text box on desktop: bold, italic, list and a
     📜 Recap toggle, then ➤. It takes a list of items so steps 15 and 16 add `@`,
     📷 and 🖼 without reshaping it.
   - Posting is optimistic (`upsertNote` with a temporary id, replaced by the
     response). The picker and visibility reset to the current session and
     `Everyone` after each post; the recap toggle resets too.
   - A draft per campaign survives a reload (`localStorage`, wrapped in try/catch).
2. **Gap prompt.** When `suggestNextSession` is true, `GapPrompt` sits above the
   text box: "Last note was 5 days ago. Start Session 14?" One tap calls
   `POST sessions { number: current + 1 }` and targets the new session. Ignoring
   it posts to the current session. The prompt re-reads after every post and on
   `sessionStarted`.
3. **Starting a session** without the prompt: "Start Session N" at the end of the
   session picker, for any member.
4. **Note actions** (a menu on each note on desktop): Edit and Delete (author),
   Change visibility (author), Hide / Unhide (DM), Edit history (when edited),
   Copy link. Edit opens `NoteEditor` in place with the recap toggle. Delete asks
   for confirmation. Copy link copies `/app/campaigns/{id}?note={noteId}`; opening
   it loads pages until that note's session is loaded, then scrolls to and
   highlights it.
5. **Edit history** (`NoteHistoryDialog`): each version with its time, oldest first.

### 14e. Mobile composer, commands and filters

1. **Keyboard inset.** 13d set `interactive-widget=overlays-content`, so the
   on-screen keyboard covers the page instead of resizing it. `useKeyboardInset`
   listens to `visualViewport` `resize` and `scroll` and exposes
   `inset = innerHeight - (visualViewport.height + visualViewport.offsetTop)`.
   The composer is `position: fixed` with `bottom: inset` while it has focus on
   a phone, and the tab bar hides so nothing sits between the composer and the
   keyboard (invariant 11). Where `visualViewport` is missing, the inset is 0.
2. **Toolbar above the keyboard** on a phone, as §3a draws it: the session and
   visibility row on top, the text box, then the toolbar row. 44px targets.
3. **Commands, each with a touch control** (`useComposerCommands`). A command is
   recognised only at the start of the text, and is consumed into the composer's
   state as soon as it is followed by a space, so it never reaches the note's
   text:

   | Command | Touch control | Effect |
   |---|---|---|
   | `/recap` | 📜 Recap toggle | `isRecap = true` |
   | `/dm` | visibility picker → 🔒 DM | `visibility = DM` |
   | `/me` | visibility picker → 🔒 Me | `visibility = Me` |
   | `/session N` | session picker | targets Session N; an unknown N shows an inline error and leaves the text alone |

   Typing `/` at the start shows the four commands as a strip above the keyboard
   (a popover at the caret on desktop), the same slot the `@` strip uses in
   step 15.
4. **Filters.** `StreamFilters` is a sticky chip row under the header:
   `All · Text · Images · Recaps · Combats · Mine`. The filter is in the URL
   (`?filter=recaps`) and is part of the query key. Under a filter, sessions with
   no matching note have no divider, except the current one. Images and Combats
   show an empty state naming steps 16 and 18.
5. **Long-press** (`useLongPress`, 500ms, cancelled by movement) opens
   `NoteActionSheet` with the same actions as the desktop menu. Promote to wiki is
   step 15.

## Verify

1. `dotnet test` and `pnpm build` pass, `schema.d.ts` is fresh, and CI is green on
   every PR in the stack.
2. `pnpm dev`, then in three browser profiles at a phone size (390 × 844): A (the
   owner, DM) creates a campaign, and B and C join it as Players.
   1. The Campaign tab shows **Session 1** as the current session with an empty
      stream and a composer.
   2. A posts `**10gp** each` to Everyone. B sees it bold, without a reload.
   3. B posts with `/dm`. A sees it with 🔒 DM. C does not see it in the
      stream, by its link, or live.
   4. A posts with `/me`. Only A sees it.
   5. B edits their note. Everyone who can see it sees "(edited)", and the history
      shows both versions.
   6. A hides B's Everyone note. C loses it live, B keeps it marked hidden and is
      told, A sees it marked hidden. A unhides it and C gets it back.
   7. B posts `/recap We left Neverwinter…`. It appears under the Session 1 divider.
   8. Any member starts Session 2. A posts to Session 1 from the session picker;
      the note shows "added later" at the end of Session 1.
   9. Filters: Recaps shows the recap only; Mine shows only the caller's notes;
      Images and Combats show their empty states.
   10. With the keyboard open on a real phone (or Chrome's device mode with a
       virtual keyboard), the toolbar sits directly above the keyboard and every
       command has a button.
3. Gap prompt: back-date the newest note's `PostedAt` by 4 days in its
   `mt_doc_sessionnote` row. The composer offers "Start Session 3?", and one tap starts it.
4. In Postgres, every `mt_events` row of the new streams has a `correlation_id`
   and an `actor`, and the note's `mt_doc_sessionnote` row is gone after a delete
   while its events remain.

## Notes / gotchas

- **Commit scopes:** only `api`, `web`, `identity`, `dice`, `root`, `ci` and `docs`
  pass the husky hook. Every PR that changes an API contract regenerates and
  commits `schema.d.ts` (13c's CI check fails otherwise).
- **Reset the dev database after 14a.** Campaigns created before it have no
  Session 1, and nothing backfills it (there are no v2 users, §12):
  `docker compose -p takeinitiative -f compose.dev.yml down -v`.
- **Why sessions are not on the Campaign stream.** §9 makes Session and
  SessionNote their own aggregates to avoid contention on one busy stream.
  The price is that "next session number" is not guarded by one stream's
  version; the unique `(CampaignId, Number)` index plus the expected-number
  request body guard it instead, and turn a double tap into the same session.
- **`SessionStream` is a query, not a stored document.** §9 lists it as a read
  model. Visibility differs per viewer, so a stored per-session document would
  need a copy per audience. It is assembled per request from the `Session` and
  `SessionNote` projections with the visibility filter in the SQL. Combat cards
  join it in step 18 as `combats[]` on each session.
- **One visibility rule, two forms.** The LINQ expression (reads) and the audience
  table (push) are tested against each other in 14b. Change them together.
- **The `@` seam (step 15).** The text is stored verbatim, so a mention written
  as `@[text](entry:<id>)` round-trips today. `utils/markdown.ts` has one link rule
  where an `entry:` href renders as plain text; step 15 replaces that rule with a
  mention chip, and adds a `MentionIndex` projection fed by `SessionNotePosted`
  and `SessionNoteEdited`. The composer's strip slot and toolbar item list are
  where the `@` autocomplete and button go. Nothing in 14 parses `@`.
- **The images seam (step 16).** `Text` filter = "no images", `Images` filter and
  a note without text are all defined against an image list that 16 adds to
  `SessionNotePosted`. In 14 text is required.
- **Hidden notes and DMs.** DMs see hidden notes (marked) so they can unhide them.
  A `Me` note is invisible to DMs, so a DM can never hide one. Hide survives an
  edit and a visibility change.
- **Not in 14:** editing a session's date (the divider uses `startedAt`), moving a
  note to another session, deleting a session, notifications beyond the hide toast
  (§12), and optimistic concurrency on note edits.
- **iOS and `visualViewport`.** iOS Safari always overlays the keyboard and fires
  `visualViewport` `scroll` as well as `resize` while it animates; listen to both
  and avoid CSS transitions on `bottom`. Test on a real device before calling 14e
  done; Chrome's device mode does not show a keyboard.

### Deviations and decisions in 14a

- **The gap prompt's clock is a keyed `TimeProvider`** (`SessionGap.ClockKey`, registered
  in `Program.cs`), not the unkeyed one. Cookie authentication reads the unkeyed
  `TimeProvider` from DI, so moving that one 4 days expired the test users' 24-hour
  cookies and every call returned 401. `GetSessions` and `GetSessionStream` take it with
  `[FromKeyedServices]`, which FastEndpoints 5.22 honours.
- **`ShiftableTimeProvider`, not `FakeTimeProvider`.** The test clock is the real clock
  plus an offset, and `Clock.Advance(4 days)` returns a scope that moves it back. The
  fixture is shared by every test in a class; `FakeTimeProvider` is frozen and cannot go
  backwards, so one gap test would leak its time into the others. No new package.
- **`RequireMember` is an endpoint extension**, `this.RequireMember(session, campaignId,
  userId, ct)`, because `ThrowError` lives on the endpoint. The 13b endpoints are unchanged.
- **Extra files** beyond Files touched: `Sessions/SessionAccess.cs` (`RequireCurrentSession`,
  `RequireSession`, `RequireVisibleNote`, `RequireAuthor`, `RequireDm`), the response DTOs
  in `Api/GetSessions/SessionResponse.cs` and `Api/GetSessionNote/SessionNoteResponse.cs`,
  `Program.cs` and `GlobalUsings.cs` (the keyed clock, the `Sessions` namespace), and the
  test helpers `Features/Sessions/TestCampaign.cs` and `ShiftableTimeProvider.cs`.
- **`ShouldDelete(SessionNoteDeleted)` works** on the self-aggregating `SessionNote` under
  `Snapshot<T>` in Marten 7.31, so no `SingleStreamProjection` was needed. The document
  goes and the stream keeps both events (checked in tests and in Postgres).
- **Starting a session.** `number` must be the current number (returned as-is, nothing
  appended) or current + 1; anything else, older numbers included, is a 409, and a
  number below 1 is a 400. When two requests race past the read, the unique index
  rejects the loser's `SaveChangesAsync` (`23505`); the endpoint catches that and returns
  the winner, so both callers get the same session. A test fires six at once.
- **A campaign without sessions** (created before 14a) gets a 404 "This campaign has no
  sessions" from the reads and from posting a note. `POST sessions { number: 1 }` starts
  its Session 1. v2 has no such campaigns once the dev database is reset.
- **Shapes.** `GET notes/{id}` returns `{ note, sessionNumber }`. `DELETE notes/{id}` is a
  204 with no body, declared in the OpenAPI document. A title is trimmed and blank clears
  it. `filter` takes the enum names `All | Text | Images | Recaps | Combats | Mine`; the
  web maps its lower-case URL value (14e) to them. `take` outside 1–10 is a 400. `Images`
  and `Combats` skip the note query. Every write returns the note or session as it is
  after the change, or unchanged when nothing was appended.
- **Hide and permissions.** Every note check loads the note, then applies the read rule
  (404 when the caller cannot see it), then the role or author check (403). So a DM
  hiding a `Me` note gets a 404, and a Player hiding a note they can see gets a 403. A
  DM can hide a `DM` note too; it changes nothing for players, who never see it.
- **Dev database name.** `takedb` is the compose container's name. The database inside it
  is `postgres` (`docker exec takedb psql -U postgres -d postgres`).
- **Verify, as run in 14a** (no browser, no UI change): `dotnet test` 52/52 (20 from step 13,
  32 new: `SessionTests` 10, `SessionNoteTests` 7, `SessionNoteVisibilityTests` 15, of which
  12 are the visibility table cells), `nuxi typecheck` clean with the regenerated
  `schema.d.ts`. Against the API on 5010, `smoke14a.mjs` (three users, every new endpoint,
  every visibility case, hide/unhide, back-posting, paging, filters, delete, validation)
  passed 70/70 checks. Verify 3: back-dating a note's `PostedAt` by 4 days in
  `mt_doc_sessionnote` turned `suggestNextSession` on for `GET sessions` and `GET stream`,
  and starting the next session turned it off. Verify 4: all 13 session and note events
  that run wrote have a `correlation_id` and an `Actor`, Session 1 shares its
  `CampaignCreated` correlation id, and the deleted note's row is gone while its
  `session_note_posted` and `session_note_deleted` events remain.

### Deviations and decisions in 14b

- **The removal goes to the groups that lose the note, not to the whole "before" audience.**
  `NotifySessionNoteMoved(before, after)` sends `sessionNoteRemoved` to the "before" groups
  that are not in the "after" groups, and to none when the note is now `Everyone` and not
  hidden (every connection is in `campaign:{id}`, so nobody loses it). Then it sends
  `sessionNoteUpserted` to the "after" audience. So a hidden `DM` note being hidden, or
  `Me` becoming `DM`, is a plain upsert, with no remove-then-add flicker. The removal still
  reaches only groups that could see the note. Hide uses the same call.
- **`sessionNoteHidden` is not sent when a DM hides their own note.** Nobody needs to be
  told about something they just did.
- **Nothing is pushed when nothing was appended.** That covers an unchanged edit, the same
  visibility, hiding a hidden note, the same title, asking to start the current session
  number, and losing the start race. The request that appended is the one that pushes.
  A rejected write (403 or 404) pushes nothing.
- **`CampaignGroups.Of(campaignId, member)`** is new in `CampaignHub.cs`. It returns the
  groups `Join` puts a connection in, and `Join` now uses it. The unit test checks
  `SessionNoteAudience` against it, so the hub's group membership is part of the checked rule.
- **Extra files beyond Files touched:** the payload records `SessionNoteRemovedMessage` and
  `SessionNoteHiddenMessage` live in `SessionHub.cs`. The tests add
  `Scopes/Integration/RecordingHubContext.cs`, which holds the recorder and a
  `RecordingHubFixture`, and a `ConfigureTestServices` hook on
  `AuthenticatedWebAppWithDatabaseFixture`. The recorder is registered as a closed
  `IHubContext<CampaignHub>`, which wins over SignalR's open-generic registration.
- **No real SignalR client in `dotnet test`.** That would need the
  `Microsoft.AspNetCore.SignalR.Client` package, so the end-to-end case is the runtime
  script below instead.
- **Verify, as run in 14b:** `dotnet test` passed 74/74. That is 52 from 14a plus 22 new:
  - `SessionNoteAudienceTests` (9): the 6 table rows; the every-case check that the push
    rule, `CanSee` and the compiled `VisibleTo` agree; a replay of all 36 before/after
    moves for every viewer; and "never `campaign:{id}` unless `Everyone` and not hidden".
    Changing the rule so hidden `Everyone` notes go to `campaign:{id}` failed 4 of the 9.
  - `SessionHubTests` (13).

  Against the API on 5010, `hub14b.mjs` connected a DM, the author (a player) and a second
  player, built each one's note cache only from pushes, and passed 28/28 checks:
  - Each visibility reached only its audience.
  - Hide removed the note from the other player, kept it (marked) for the DM and the author,
    and sent `sessionNoteHidden` to the author only.
  - Unhide restored it for everyone.
  - Visibility changes DM → Everyone → Me → DM moved the note correctly each time.
  - An edit or delete of a DM note sent nothing to the other player.
  - `sessionStarted` and `sessionTitleChanged` reached all three.
  - Each viewer's pushed cache matched a fresh `GET stream`.

  `schema.d.ts` did not change, because no HTTP contract changed.

### Deviations and decisions in 14c

- **Extra files beyond Files touched:** `utils/sessionDates.ts` (the divider date, note
  times and the "added N days later" label), `vitest.config.ts`, `tests/unit/{markdown,
  sessionStreamCache,sessionDates}.test.ts`, `vitest` as a dev dependency with a `test`
  script, and a "Unit tests" step in `.github/workflows/testWeb.yml`. The tests import
  what they use, because Nuxt's auto-imports do not exist under vitest.
- **All eleven session and note endpoints are wired now** as `useApi().session`
  (`list`, `start`, `putTitle`, `getStream`) and `useApi().note` (`post`, `get`, `put`,
  `putVisibility`, `putHidden`, `delete`, `history`). 14d only adds queries and mutations.
- **Query keys.** The stream is `["sessionStream", campaignId, filter]`, apart from
  `["campaign", id]`, so a member joining does not refetch it. `updateSessionStreams`
  (in `utils/queries/sessions.ts`) applies one cache function to every loaded filter of a
  campaign, passing that filter and the caller's `currentMemberId`; the hub and 14d's
  mutations both use it. `staleTime` is `Infinity`: pushes keep it fresh.
- **The stream is refetched after every hub join**, not only after a reconnect. A stream
  fetched before `Join` finished could miss a note pushed in between. It costs one extra
  request when a campaign opens.
- **`upsertSession` also turns the gap prompt off** on the newest page when a new
  session becomes current, since an empty current session never suggests another (14a).
- **Markdown.** Images are off (`![x](url)` renders as text): a note is text in 14, and
  images in step 16 are uploads, not remote URLs that could track readers. The `@` seam
  is a core rule, `entry_mentions`, that turns an `entry:` link into
  `entry_mention_open` / `entry_mention_close` tokens and drops the `@` before it. Their
  renderer rules output nothing, so the mention reads as its text. Step 15 replaces those
  two renderer rules with the chip.
- **Divider date.** The parts are ordered weekday, day, month in every locale
  ("Sat 20 Sep"; en-GB's ICU data says "Sept"), with the year added when it is not this
  year.
- **Layout.** The members button sits in a slim row above the stream, where 14e's filter
  chips go. `MembersPanel` is a bottom sheet on a phone and a side sheet from 768px, with
  its own 44px close button instead of the shadcn one. The stream scrolls inside its own
  container, so 14d's composer mounts below `<SessionStream>` in
  `pages/app/campaigns/[campaignId]/index.vue`, outside the scroller.
- **Hooks for 14d.** `SessionNoteCard` has an `actions` slot in its header and an
  `id="note-{id}"` anchor for copy link. `SessionStream` exposes `scrollToBottom()`. Its own
  new note scrolls into view; a note from someone else while scrolled up shows the "New
  notes" pill. That covers any note new to the cache that did not come with an older
  page, so an unhide or a visibility change the reader gains counts too.
- **Filters in the stream.** `SessionStream` takes a `filter` prop (default `All`) and
  already hides the divider of a session with no matching note, except the current one.
  14e only passes the filter.
- **Hide toast.** The app's `Toaster` defaults to 1 second, so the hide toast asks for 6.
- **Verify, as run in 14c** (no browser; the UI was not looked at):
  - `nuxi typecheck` is clean and `nuxt build` succeeds.
  - `vitest` passes 33/33: the cache functions (18), markdown sanitising and the mention
    seam (10), and the dates (5). Turning `html: true` on and removing the mention rule
    failed 3 of them.
  - Against the API on 5010 and `nuxt dev` on 3100, `stream14c.mjs` passed 111/111 checks.
    It fetched the campaign routes and had Vite compile every new module. Then three
    viewers (a DM and two players) loaded the stream page by page, as the infinite query
    does, under `All`, `Recaps` and `Mine`, and applied the real hub pushes through
    `utils/sessionStreamCache.ts`. After each step, every viewer's cache equalled a
    fresh `GET stream` for every filter. The steps were: posts in every visibility, a
    recap, edits (one un-recapped), hide (the toast went to the author only), unhide,
    DM → Everyone → Me, a title change, Session 6 (only it current, gap prompt off), a
    back-posted note (last in its session, added later), a note for an unloaded
    session (ignored), deletes, and promoting a player (only they refetched, and they
    then held the DM note).
  - `smoke14a.mjs` passed 70/70, `hub14b.mjs` 28/28 and `pages13d.mjs` 19/19.


### Deviations and decisions in 14d

- **Extra files beyond Files touched:**
  - `utils/composer.ts` holds the composer's pure rules: its state and reset, the POST body, the optimistic note, what Enter does, the picker rows, the gap prompt, the draft, and bold/italic/list formatting. It also holds `ComposerToolbarItem` and `VISIBILITY_OPTIONS`.
  - `utils/noteActions.ts` holds `noteActionsFor`, the labels, `noteLink` and `noteLinkProgress`.
  - Tests: `tests/unit/{composer,noteActions}.test.ts`.
  - Modified: `components/Session/SessionStream.vue` (the note props, and following a note link), `composables/useCampaignHub.ts` (the sessions list and the optimistic copy), `utils/sessionStreamCache.ts` (`upsertSessionInList`, `dropPendingCopy` and the pending-id helpers), `utils/api/types.ts` (`SessionList`, `SessionNoteVersion`) and `utils/apiErrorParser.ts` (`apiErrorMessage`, `apiErrorStatus`).
  - No API change, so `schema.d.ts` is unchanged.
- **The session picker reads `GET sessions`** (`["sessions", campaignId]`, `staleTime: Infinity`), because the stream only has the pages loaded so far.
  - `applySession` upserts one session into both that list and every loaded stream. The hub's `sessionStarted` and `sessionTitleChanged` and the start and title mutations all use it.
  - The list is invalidated wherever the stream is: after joining the hub, on reconnect, and when the caller's role changes.
- **The gap prompt re-reads `GET sessions` after every post**, from the post mutation's `onSettled`.
  - On `sessionStarted` it does not refetch. `upsertSessionInList` turns `suggestNextSession` off, since an empty current session never suggests one (14a).
  - Between re-reads, `showGapPrompt` also hides the prompt when any loaded note is newer than 3 days. So a note someone else just posted hides it straight away. This does not duplicate the rule: it only turns off what the server turned on.
  - "Last note was N days ago" counts days from the newest loaded note.
- **Accepting the prompt, or "Start Session N"**, posts `number: current + 1`. It targets the new session by setting the pick back to "current" (`sessionId = null`).
  - A 409 means the list is stale. It shows "Someone else started a session. Try again." and re-reads the list.
  - A second tap on the same number gets back the same session (14a).
- **Posting sends `sessionId` only for an older session.** A post with nothing picked follows the current session, even if someone started a new one while the note was being typed. Picking the current session in the picker is stored as "nothing picked".
- **Optimistic posting.** The note appears at once with a `pending-…` id, marked "Sending…" and with no actions. The response replaces it in the same cache update (`removeNote` then `upsertNote`).
  - The push can arrive before the response. In that case the hub's `dropPendingCopy` drops the caller's pending note with the same session and text, so the note never shows twice.
  - If the post fails, the text, session, visibility and recap come back, unless something new has been typed since. A toast shows the API's error.
- **Enter** posts on desktop. On a touch screen (`(pointer: coarse)`) Enter adds a new line. While an IME is composing, Enter is left to the IME. The note editor behaves the same way, and Esc cancels an edit.
  - Ctrl/⌘+B and Ctrl/⌘+I toggle bold and italic.
  - The draft is saved to `localStorage` on every change, under the key `ti:composerDraft:{campaignId}`. Every read and write is wrapped in try/catch, and blank text removes the key.
- **The text box** grows up to 40dvh, then scrolls. A character count appears from 9,000 characters, and the limit is 10,000 after trimming, as in 14a.
- **Note actions.**
  - `SessionNoteCard` takes `campaignId`, `currentMemberId`, `isDm` and `highlighted`, and carries out every action in `run(action, visibility?)`.
  - The `actions` slot now defaults to `SessionNoteActions`, and exposes `{ actions, run }` for 14e's sheet.
  - With a mouse (`pointer: fine`) the ⋯ button shows on hover or focus. On touch screens it is always visible, at 44px, until 14e adds long-press.
  - "Change visibility" is a submenu with the same three choices as the composer.
  - A DM is never offered Hide on a `Me` note. They would get a 404 anyway, since only the author can see it.
  - Delete asks for confirmation in a dialog. The edit history and delete dialogs are mounted the first time they are used, not once per note.
- **Following a note link** (`?note=`) happens in `SessionStream` (`focusNoteId` prop, `goToNote` exposed):
  - `GET notes/{id}` supplies the session number. The stream then loads older pages until that session is loaded, and does it with the same scroll-keeping `loadOlder`. That loop stops after 500 steps.
  - The stream then scrolls the note to the centre and highlights it for 2.5s.
  - A 404 shows "That note is not there, or you cannot see it." A note hidden by the current filter shows a toast of its own.
  - The page then removes `?note=` from the URL, so a reload opens at the bottom again.
- **Phone.** In 14d the composer sits below the stream, in the page's normal flow. Nothing pins it above the keyboard yet: `interactive-widget=overlays-content` lets the keyboard cover it until 14e's `useKeyboardInset`. The session and visibility buttons shorten to "S13 ▾" and "👁 Everyone ▾", and every target is 44px.
- **Seams for 14e.**
  - `Composer` exposes `{ focus, state }`, where `state` is the reactive `ComposerState`. It also has a `strip` slot, which receives `{ state }`, above the text box. `useComposerCommands` can set `state.isRecap`, `state.visibility` and `state.sessionId`, and rewrite `state.text`. `targetSession` and `sessionOptions` resolve `/session N`.
  - The toolbar is `ComposerToolbar` with `items: ComposerToolbarItem[]` and an `end` slot. 14e can move it into the phone layout without changing its API.
  - `NoteActionSheet` should draw `noteActionsFor(note, ctx)` with `NOTE_ACTION_LABELS`, and hand the choice to the card's `run` through the `actions` slot props.
  - `SessionStream` already takes `filter` and passes it to the cards. `Composer` takes `filter` as well, so it shares the stream's query.
- **Verify, as run in 14d** (no browser; the UI was not looked at):
  - `nuxi typecheck` is clean and `nuxt build` succeeds.
  - `vitest` passes 71/71: 33 from 14c and 38 new. Of the new ones, 21 are in `composer`, 10 in `noteActions`, and 7 are cache tests for optimistic notes and the sessions list.
  - `composer14d.mjs` passed 142/142 against the API on 5010 and `nuxt dev` on 3100:
    - Vite compiled every new or changed module, and `/app/campaigns/x?note=y` served.
    - A DM and two players each drove the composer's flows through the web's own `utils/composer.ts`, `utils/noteActions.ts` and `utils/sessionStreamCache.ts`, with real hub pushes. Each flow checked that every viewer's stream and session list equalled a fresh `GET`. The flows were:
      - posting a note, blank refused, optimistic, and the reset after posting;
      - `DM`, `Me` and recap posts, and the player's 404 on a DM note's link;
      - the push racing the response, which left one copy;
      - the action lists per viewer, and the API's 403s for a player;
      - an edit with its history, where an unchanged edit added no version;
      - a visibility change, and hide and unhide with the toast to the author only;
      - a delete, after which the history returned 404;
      - the gap prompt, after back-dating `PostedAt` by 4 days: it read "Last note was 4 days ago. Start Session 2?", one tap started Session 2, the second player's picker updated live, a repeated tap returned the same session, and skipping a number was a 409;
      - a back-post from the picker, which was added later and last in Session 1;
      - a post with no pick, which followed a newly started session;
      - a note link three sessions back, found by paging.
  - The earlier runtime scripts still pass: `smoke14a` 70/70, `hub14b` 28/28, `stream14c` 111/111 and `pages13d` 19/19.
  - Back-dating needs `to_jsonb(timestamptz)`, not `::text`. Postgres's text form (`… +00`) does not deserialize, so that campaign's reads return 400.

### Deviations and decisions in 14e

- **Extra files beyond Files touched:**
  - Pure rules, unit tested: `utils/composerCommands.ts` (parsing, the strip's suggestions, picking one), `utils/keyboardInset.ts` (the inset and when to pin), `utils/longPress.ts` and `utils/streamFilters.ts` (URL value ↔ enum, empty states, and `visibleStreamSessions`, the divider rule moved out of `SessionStream`).
  - `components/Composer/CommandStrip.vue`: the `/` strip.
  - Tests: `tests/unit/{composerCommands,mobileComposer}.test.ts`.
  - Also modified: `components/Session/{SessionNoteCard,NoteActions}.vue` and `pages/app/campaigns/[campaignId]/index.vue`.
  - `useComposerPinned()` (in `useKeyboardInset.ts`) is a `useState` flag that the layout reads to hide the tab bar.
  - No API change, so `schema.d.ts` is unchanged.
- **Pinning.** The composer is pinned while its text box has focus and either the screen is a phone (below `md`) or a keyboard is covering the page (a tablet).
  - `inset` is 0 without `visualViewport`, and also while pinch-zoomed (`scale ≠ 1`), when the visual viewport shrinks for another reason.
  - While pinned, the composer's wrapper keeps its height plus `inset`, so the stream ends where the composer starts. The stream also watches its own size, so a reader at the bottom stays at the bottom when it shrinks.
  - There is no transition on `bottom`. With no keyboard up, the pinned composer keeps clear of the home-indicator safe area.
  - Unpinning waits 120ms after a blur, so focus passing through a button does not flicker. Every toolbar button, the ➤ button and the strip's chips use `mousedown.prevent`, so tapping them keeps the text box focused and the keyboard up.
  - Opening a picker moves focus into its menu, so the keyboard closes and the composer unpins. The pickers open upwards (`side="top"`), as in 14d.
- **The strip sits below the text box**, between it and the toolbar, as §3a draws it. 14d had put the `strip` slot above the text box. Step 15's `@` slot moved with it.
  - From `md` the strip is a popover just above the text box's left edge, not at the caret itself. A command only counts at the start of the text, so the caret is on that first line.
- **Commands.**
  - Pasted runs apply in order (`/dm /recap text`). A bad `/session` stops the run and leaves its text alone. `/session x` explains that it needs a number.
  - Unknown commands (`/roll`) stay as text. Commands are case-insensitive.
  - The strip lists the commands that match what has been typed. After `/session ` it lists the matching sessions, newest first, up to 8. So `/session N` can be done entirely by touch (tap `/session N`, then tap a session), as well as through the picker.
  - On desktop, ↑/↓ move through the strip, Tab or Enter picks, and Esc dismisses it until the text changes. Enter also picks on a phone while the strip is open.
- **Long-press** works with touch and pen only; a mouse has the ⋯ menu.
  - Moving more than 10px cancels it, and so does `pointercancel` (the stream starting to scroll).
  - It never starts on a link, a button or the editor.
  - It suppresses the system's context menu and the click that follows, and buzzes for 10ms where `navigator.vibrate` exists.
  - On touch screens the card has `user-select: none` and no callout, except while editing: iOS will not type into a field inside `user-select: none`.
- **⋯ on touch screens** is now screen-reader-only (`sr-only` under `pointer: coarse`), so VoiceOver and TalkBack still reach the menu. 14d had shown it at 44px until long-press existed.
- **`NoteActionSheet`.** There is one per stream, not one per card. The card emits `openActions` with its `{ actions, run }`.
  - Edit, Edit history and Delete run after the sheet has closed and released focus, so the editor or dialog keeps focus.
  - Copy link, Hide, Unhide and visibility changes run inside the tap. iOS only allows clipboard writes during a user gesture.
  - The sheet closes if its note leaves the stream (deleted, hidden, or filtered out).
- **Filters.**
  - `?filter=` holds the lower-case name. `All` is written as no parameter, and an unknown value reads as `All`. Changing the filter uses `router.replace`, so it adds no history entry.
  - The chip row is the slim row above the stream's scroller, outside it. It stays put without `position: sticky`.
  - Each filter has its own empty text. Images and Combats name steps 16 and 18.
- **Verify, as run in 14e** (no browser; the UI was not looked at on a device):
  - `nuxi typecheck` is clean and `nuxt build` succeeds.
  - `vitest` passes 94/94: 71 from 14d and 23 new (12 for commands, 11 for the inset, pinning, long-press and filters).
  - `dotnet test` passes 74/74 (the API is unchanged).
  - `filters14e.mjs` passed 233/233 against the API on 5010 and `nuxt dev` on 3100:
    - Vite compiled every new or changed module, and `/app/campaigns/x?filter=…` served for every filter.
    - A DM and two players posted by typing text through `applyCommands` into the composer state and `buildPostBody`. This covered `/dm`, `/me`, `/recap`, chained commands, a command that is not at the start, `/session 1` (added later) and `/session 3`. `/session 9` was refused inline with its text kept.
    - For all six filters, for each viewer, three things were checked after posting, after a hide, and after an un-recap, a visibility change and a delete:
      - the notes shown matched a model of who sees what;
      - the dividers matched the "only sessions with a match, plus the current one" rule;
      - the per-filter live cache, fed by hub pushes, equalled a fresh `GET`.
  - The earlier scripts still pass: `smoke14a` 70/70, `hub14b` 28/28, `stream14c` 111/111, `composer14d` 142/142 and `pages13d` 19/19.
  - Verify 4, re-checked: all 332 session and note events in the dev database have a `correlation_id` and an `Actor`. `smoke14a`'s deleted note has no `mt_doc_sessionnote` row, and its `session_note_posted` and `session_note_deleted` events remain.
- **Left to check by hand** (Verify 2 needs browsers, and 2.10 needs a real phone):
  - **Phone** (iOS Safari and Android Chrome, installed as a PWA and in the browser):
    - Focus the composer. The tab bar hides, and the composer (pickers, text box, toolbar) sits directly on the keyboard, with no gap and no jitter while the keyboard animates.
    - Blur it. The composer and tab bar return.
    - Tapping B, I, •, 📜 Recap, ➤ and a strip chip keeps the keyboard up.
    - Type `/`. The chips show above the keyboard. Tap `/session N`, then a session.
    - `/dm ` and `/recap ` change the pickers and toggle, and vanish from the text.
    - `/session 9 ` shows the inline error.
    - Long-press a note. The sheet opens after about half a second, and scrolling the stream does not open it. Each action works, including Delete's dialog, Edit (the keyboard must not cover the editor) and Copy link.
    - The filter chips scroll sideways.
    - Rotating the phone and pinch-zooming do not strand the composer.
  - **Desktop:**
    - Verify 2.1–2.9 in three browser profiles.
    - Type `/`. The popover appears above the text box, ↑/↓/Tab/Enter/Esc work, and Enter posts once it is closed.
    - Filters change the URL, survive a reload, and show the Images and Combats empty states.
    - The ⋯ menu shows on hover.
    - The composer never pins at desktop widths, even when the window is short.

