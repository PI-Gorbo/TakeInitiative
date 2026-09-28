# 18 — Combat v2

## Goal

**Combat** comes back, simplified as design §8 describes. A DM creates a `Draft`
combat and fills it with **combatants**. `@Goblin ×4` adds Goblin 1–4, with their HP
rolled and their AC and initiative roll taken from the entry's **Stats**, and a player
adds their own **player character**. The first **roll** starts the combat: every
waiting combatant rolls, the **initiative order** is sorted by one int plus a hidden
random `Tiebreak`, and the turn goes to the top. Whoever owns the turn, or a DM, ends
it, and the rounds count up until a DM finishes the combat. Each combatant has its own
**PlayersSee** (`Exact`, `Band` or `Nothing`). The server redacts every read and every
push for players (invariant 8), so a hidden goblin, a monster's HP and a Draft never
reach a player's browser. Each combat is a **combat card** in its session in the
session stream, with a "Join combat" banner and a pulsing Combat tab while one is live.
⌘K gets a COMBATS section and "⚔ Start combat".

"Running" at the end: three browsers (a DM and two players) run a whole fight from
the Combat tab, on a phone and on a desktop, with live updates and no reload. The
Campaign tab shows the fight as a card in its session.

The step ships as six PRs, stacked with `gh stack` on top of this file's docs PR, which
sits on 17c (#229). Each PR leaves the app runnable:

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 18a | `v2/18a-combat-api` | Combat model, redaction and combatants (API) | 17's app unchanged in the browser. The API creates Draft combats, adds, edits and removes combatants, answers a redacted view per viewer and pushes it live. The leak tests pass | [x] |
| 18b | `v2/18b-combat-turns-api` | Initiative, turns, finish and history (API) | The same in the browser. Roll starts a combat and slots in late joiners, end turn advances turns and rounds, a DM reorders and finishes, and a DM reads the history | [[x] |
| 18c | `v2/18c-combat-tab` | The Combat tab and the combat page | The Combat tab lists combats. A DM creates one, adds combatants with `@Goblin ×4`, rolls and finishes, a player adds their character and ends their turn, all live | [x] |
| 18d | `v2/18d-combatant-sheet` | The combatant sheet | Tapping a combatant opens its sheet: damage and heal, conditions, PlayersSee, hidden, AC, initiative, remove, and drag to reorder. A DM opens the history | [x] |
| 18e | `v2/18e-combat-card` | Combat cards, the banner and entries' combats | The session stream shows combat cards and the Combats filter works. A live combat shows the Join combat banner and pulses the Combat tab. An entry page lists its combats. The combat page has the slim composer | [ ] |
| 18f | `v2/18f-combat-search` | Combats in ⌘K | ⌘K has a COMBATS section and "⚔ Start combat". The step's Verify passes | [ ] |

Connections' "Fought together" (§6) is step 19, which reads the combatant links this
step stores. Reference stat blocks feeding Stats are step 20. This step builds neither.

## Depends on

**15**: entries, the `@` picker (`EntryPicker`), claims and Stats, which supply a
combatant's defaults. **14** for sessions and the stream, and **17** for the ⌘K
providers and the action registry that 18f extends. These parts of
[12-v2-design-session.md](12-v2-design-session.md) are binding:

- the glossary (§1), including the nouns this PR adds;
- the combat screen's slim composer (§3);
- player characters and Stats (§4);
- combat v2 (§8), all of it;
- SignalR groups (§9);
- the invariants (§10, especially 3, 5, 8, 9 and 11).

The behaviour list kept from the v1 combat tests is in
[13-v2-skeleton.md, "Behaviour kept from the v1 combat tests"](13-v2-skeleton.md#behaviour-kept-from-the-v1-combat-tests-filled-in-during-13a).
18a and 18b rewrite each item as a test against the new model.

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web), except where they start at the repo root.
Nothing about combat exists today apart from the Combat tab's empty state
(`pages/app/campaigns/[campaignId]/combat.vue`), the `Combats` stream filter that
matches nothing, and comments naming step 18 (`Stats.cs`, `PutEntryStats.cs`,
`ISearchProvider.cs`, `Bootstrap.cs`, `GetSessionStream.cs`, `sessionStreamCache.ts`,
`streamFilters.ts`, `searchActions.ts`). v1 combat was deleted in 13a (#195).

**This PR (docs)**
- `docs/roadmap/18-combat.md`: this file
- `docs/roadmap/12-v2-design-session.md` §1: the new nouns (step 0 below)
- `docs/roadmap/README.md`: link step 18, `in progress`
- `docs/roadmap/HANDOVER.md`: 17 closed, 18 next

**18a**
- API add `src/Features/Combats/Models/{Combat,Combatant,Condition,PlayersSee,HpBand,CombatStatus,CombatOrder}.cs`
- API add `src/Features/Combats/Models/Events/{CombatCreated,CombatantsAdded,CombatantEdited,CombatantRemoved,InitiativeRolled,TurnEnded,CombatFinished}.cs` (all seven now, so the projection is whole; 18b appends the last three)
- API add `src/Features/Combats/{CombatAccess,CombatView,CombatHub,CombatantDefaults,CombatWrite}.cs`
- API modify `src/Features/Entries/Api/PutEntryStats/PutEntryStats.cs` (a `DiceExpression` overload for child rules)
- API add `src/Features/Combats/Api/{PostCombat,GetCombats,GetCombat,PostCombatants,PutCombatant,DeleteCombatant}/*.cs`, `Api/CombatResponse.cs`
- API modify `src/boostrap/Bootstrap.cs` (the `Combat` snapshot projection and its indexes), `src/Features/Campaigns/CampaignHub.cs` (`CampaignHubMessages.CombatChanged`), `GlobalUsings.cs`
- Tests add `Scopes/Unit/{CombatOrderTests,CombatViewTests,CombatantDefaultsTests}.cs`
- Tests add `Scopes/Integration/Features/Combats/{CombatTests,CombatantTests,CombatLeakTests,CombatHubTests,CombatTestKit}.cs`
- Web `utils/api/schema.d.ts`: regenerated

**18b**
- API add `src/Features/Combats/Api/{PostCombatRoll,PostCombatEndTurn,PostCombatFinish,PutCombatantPosition,GetCombatHistory}/*.cs`
- API modify `src/Features/Combats/Models/Combat.cs` (the three `Apply`s), `CombatOrder.cs` (placing and respacing)
- API modify `src/Features/Combats/CombatWrite.cs` (the retrying `Write`), `Models/Events/CombatantEdited.cs` (`Tiebreak`), `Api/{PostCombatants,PutCombatant,DeleteCombatant}` (onto `Write`)
- Tests add `Scopes/Integration/Features/Combats/{InitiativeTests,TurnTests,CombatHistoryTests}.cs`, `Scopes/Unit/CombatTurnTests.cs`
- Tests modify `Scopes/Integration/Features/Combats/{CombatTests,CombatLeakTests,CombatTestKit}.cs`
- Web `utils/api/schema.d.ts`: regenerated

**18c**
- Web delete `pages/app/campaigns/[campaignId]/combat.vue`
- Web add `pages/app/campaigns/[campaignId]/combat/index.vue`, `combat/[combatId].vue`
- Web add `components/Combat/{CombatList,CombatListItem,NewCombatDialog,InitiativeList,CombatantRow,CombatantHp,WaitingList,CombatBar,AddCombatantsDialog,AddMyCharacter}.vue`
- Web add `utils/combat.ts` (order, turn, HP display, `parseAddCombatants`), `utils/api/combat/*.ts`, `utils/queries/combats.ts`
- Web modify `composables/useCampaignHub.ts` (`combatChanged`), `utils/api/types.ts`
- Web add `tests/unit/combat.test.ts`

**18d**
- Web add `components/Combat/{CombatantSheet,HpAdjust,ConditionEditor,PlayersSeePicker,CombatHistorySheet}.vue`, `composables/useReorderDrag.ts`
- Web modify `components/Combat/{InitiativeList,CombatantRow}.vue`, `utils/combat.ts` (`applyHpDelta`, conditions, `dropPosition`)
- Web add `tests/unit/combatantSheet.test.ts`

**18e**
- API add `src/Features/Combats/Api/GetEntryCombats/*.cs`, `src/Features/Combats/CombatCard.cs`
- API modify `src/Features/Sessions/Api/GetSessionStream/GetSessionStream.cs` (`combats[]` per session, the `Combats` filter)
- Tests add `Scopes/Integration/Features/Combats/{CombatStreamTests,EntryCombatsTests}.cs`
- Web add `components/Combat/{CombatCard,JoinCombatBanner,EntryCombats}.vue`
- Web modify `components/Session/SessionStream.vue`, `utils/sessionStreamCache.ts`, `utils/streamFilters.ts`, `layouts/campaign.vue` (the pulse), `pages/app/campaigns/[campaignId]/index.vue` (the banner), `pages/app/campaigns/[campaignId]/wiki/[entryId].vue`, `components/Composer/Composer.vue` (`slim`), `combat/[combatId].vue`
- Web modify `tests/unit/sessionStreamCache.test.ts`; add `tests/unit/combatCard.test.ts`
- Web `utils/api/schema.d.ts`: regenerated

**18f**
- API add `src/Features/Search/Providers/CombatSearchProvider.cs`
- API modify `src/Features/Search/Api/GetSearch/{GetSearch,SearchResponse}.cs` (`Combats`, `kind: "Combat"`), `src/Features/Search/Sql/SearchVisibilitySql.cs` (the combat fragment), `src/boostrap/Bootstrap.cs` (the registration)
- Tests modify `Scopes/Integration/Features/Search/{SearchLeakTests,SearchTests,SearchVisibilityParityTests}.cs`
- Web modify `utils/search.ts` (the COMBATS rows and target), `components/Search/SearchHitRow.vue`, `utils/searchActions.ts` ("⚔ Start combat"), `combat/index.vue` (`?new=`)
- Web modify `tests/unit/{search,searchActions}.test.ts`
- Web `utils/api/schema.d.ts`: regenerated

## Steps

### 0. Start the stack (this PR)

```sh
git switch v2/17c-search-actions
gh stack add v2/18-combat-plan
```

Add each sub-step on top with `gh stack add v2/18a-combat-api` and so on.

**Glossary check.** Combat, Combatant and Round / Turn / Condition are in §1 already.
This PR adds the nouns the step puts into code and UI:

- **Initiative order**: a combat's rolled combatants, highest initiative first, ties
  broken by the hidden `Tiebreak`. Not "turn order list" or "tracker".
- **Waiting**: a combatant with no initiative yet. The next roll places it.
- **PlayersSee**: per combatant, what players see of its HP: `Exact`, `Band` or
  `Nothing`. AC shows only with `Exact`.
- **HP band**: `Healthy`, `Bloodied` (half or less) or `Down` (0 or less), shown for
  `Band`.
- **Combat card**: a combat drawn in its session in the session stream.

### 18a. Combat model, redaction and combatants (API)

1. **The aggregate** (§8, §9). One Marten stream per combat, stream id = combat id,
   with an inline snapshot projection like `Session` and `Entry`:

   ```csharp
   record Combat {
       Guid Id; Guid CampaignId; Guid SessionId; string Name;
       CombatStatus Status;              // Draft | Active | Finished
       int Round;                        // 0 while Draft
       Guid? TurnCombatantId;            // see Notes, "TurnCombatantId, not TurnIndex"
       IReadOnlyList<Combatant> Combatants;
       Guid[] EntryIds;                  // denormalised for 18e's entry combats, like ArticleMentionIds
       Guid CreatedByMemberId; DateTimeOffset CreatedAt; DateTimeOffset? StartedAt; DateTimeOffset? FinishedAt;
   }
   record Combatant {
       Guid Id; string Name; Guid? EntryId; Guid? OwnerMemberId;
       string InitiativeRoll;            // a dice expression; see Notes
       int? Initiative; int Tiebreak;    // null Initiative = waiting
       int? Hp; int? MaxHp; int? Ac;
       bool Hidden; PlayersSee PlayersSee; IReadOnlyList<Condition> Conditions;
   }
   record Condition(string Label, string? Note);
   ```

   - Schema: indexes on `CampaignId`, `(CampaignId, Status)` and `SessionId`, and a GIN
     index on `EntryIds`, the way `Entry.ArticleMentionIds` is indexed.
   - Every event carries `Actor` (invariant 9). `CombatCreated(Actor, CampaignId,
     SessionId, Name)`, `CombatantsAdded(Actor, Combatant[])`, `CombatantEdited(Actor,
     CombatantId, CombatantState)` (the combatant's whole editable state after the
     edit, so replay needs no merge rules), `CombatantRemoved(Actor, CombatantId)`.
     18b fills in `InitiativeRolled`, `TurnEnded` and `CombatFinished`.
   - Limits: name 1–100 characters, at most 50 combatants, conditions at most 20 of
     1–40 characters with a note of at most 200, `Hp` −999…9,999, `MaxHp` 1…9,999, `Ac`
     0…99 (`Stats.AcMax`), initiative −99…99.
2. **The order** (`CombatOrder`, pure). `Ordered(combat)` is the rolled combatants by
   `Initiative` descending, then `Tiebreak` descending, then `Id`, so the sort is total
   and stable (13a's rule). `Waiting(combat)` is the rest, in the order they were added.
   `Tiebreak` is `Random.Shared.Next(0, 1 << 30)` when a combatant is added, so two
   equal initiatives are never ambiguous and adding a combatant never reorders the ones
   already placed.
3. **Defaults from an entry** (`CombatantDefaults`, pure over `IDiceRoller`). For each
   entry picked, with a count of 1–20:
   - Names are the entry's name, or `Goblin 1`…`Goblin 4` for a count above 1. The
     numbering continues past combatants already in the combat with that entry
     (`Goblin 5` next), and a count of 1 whose name is already taken becomes
     `Goblin 2`. The name is a copy: renaming the entry does not rename it, and it can
     be edited.
   - Stats come from `EntryStats.For(entry, caller)`, the rule 15g built, so a player
     never reads an NPC's stats. `MaxHp` is **rolled once per combatant** when it is
     added (§8, 13a's "HP given as a roll"). `Hp` starts at `MaxHp`. `Ac` is copied.
     `InitiativeRoll` is `Stats.InitiativeRoll`, or `1d20` when there is none.
   - `OwnerMemberId` is the entry's claimer.
   - `PlayersSee` is `Exact` for a combatant with an owner and `Band` otherwise (§8).
   - `Hidden` is **true when the entry is not `Everyone`**, so adding a DM entry to a
     fight never reveals its name to players (invariant 5, "nothing is revealed
     automatically"). Otherwise false, unless the DM asks for hidden.
   - A combatant with no entry (a plain name) takes the request's values, with
     `InitiativeRoll` `1d20` by default.
   - Expressions are checked with `IDiceRoller.Check` in the validator, and a roll that
     fails at evaluation (it cannot, after the check) is a 400, not a 500.
4. **Who can do what** (`CombatAccess`, §8). Every route resolves the member with
   `RequireMember` first. A player asking for a Draft, or another campaign's combat,
   gets 404, never 403, so a Draft's existence does not leak.

   | Action | DM | Player |
   |---|---|---|
   | Create, finish, roll everyone, reorder, history | yes | no (403) |
   | See a combat | any | only once it has started (`StartedAt` set) |
   | Add combatants | any, any status but `Finished` | one combatant per call, from a Character entry they have claimed, `Active` only, not one already in the combat |
   | Edit a combatant | every field | their own: `Hp`, `MaxHp`, `Conditions`, and `Initiative` while it is waiting |
   | Remove a combatant | any | their own |
   | End the turn | any turn | the turn of a combatant they own |

   A `Finished` combat is read-only: every write is a 409 "This combat has finished.".
5. **Redaction** (`CombatView.For(combat, viewer, visibleEntryIds)`, pure). This is the
   only way a combat leaves the server, for reads and pushes alike (invariant 8).

   ```
   CombatResponse   { id, campaignId, sessionId, name, status, round, turnCombatantId?,
                      startedAt?, finishedAt?, combatants: CombatantResponse[] }
   CombatantResponse { id, name, entryId?, ownerMemberId?, initiative?, waiting: bool,
                      hp?, maxHp?, band?: "Healthy" | "Bloodied" | "Down", ac?,
                      hidden: bool, playersSee, conditions: Condition[], initiativeRoll? }
   ```

   - **DMs** get every field except `Tiebreak`, which no response carries (the server
     does every sort and placement).
   - **Players** get, in `CombatOrder` order:
     - no `Hidden` combatant at all: not its row, not its name, not a count;
     - `turnCombatantId` null when the turn is on a hidden combatant;
     - for `Exact`: `hp`, `maxHp` and `ac`;
     - for `Band`: `band` only, from `Hp` and `MaxHp`, or nothing when `MaxHp` is null;
     - for `Nothing`: no HP, no band and no AC;
     - **their own combatant exactly**, whatever its PlayersSee, since it is their
       character;
     - `entryId` only when the viewer can see that entry (`visibleEntryIds`, loaded once
       per request with `EntryVisibility.VisibleTo`), and resolved through
       `MergedIntoId` (15g). Otherwise null, and the name stays as text;
     - `initiativeRoll` on their own combatant only;
     - `hidden` and `playersSee` only on their own combatant (always `false` and its
       value). Other rows carry `playersSee` as the level they were redacted to, which
       the web needs to draw the row, and nothing more.
   - Conditions are visible on every combatant a player can see. They are what the
     table sees happen, not a stat.
6. **Endpoints.** Under `/api/campaigns/{campaignId}`:

   | Endpoint | Who | Does |
   |---|---|---|
   | `POST combats { name }` | DMs | Starts a `Draft` combat in the current session. With no session yet, 409 "Start Session 1 first." |
   | `GET combats?status=` | members | Summaries (`id, name, status, round, sessionId, sessionNumber, combatantCount, startedAt?, finishedAt?`) the caller can see, newest first. `status` is a comma list of `Draft,Active,Finished`. The count excludes hidden combatants for players |
   | `GET combats/{combatId}` | members | `CombatResponse` for the caller |
   | `POST combats/{combatId}/combatants { combatants: [{ entryId?, name?, count?, initiativeRoll?, maxHp? (expression), ac?, hidden?, playersSee? }] }` | 4 | Adds them as waiting combatants with one `CombatantsAdded`. With `entryId`, the defaults (3) fill what is omitted |
   | `PUT combats/{combatId}/combatants/{combatantId} { name, initiative?, hp?, maxHp?, ac?, hidden, playersSee, conditions }` | 4 | Replaces the editable state. A player sending a changed field they may not edit gets 403 |
   | `DELETE combats/{combatId}/combatants/{combatantId}` | 4 | Removes it. When it had the turn, the turn passes on (18b) |

   Every write returns the caller's `CombatResponse`. The whole `PUT` shape keeps the
   endpoint simple, and edits are last-write-wins per combatant (Notes).
7. **Pushes** (`CombatHub.NotifyCombatChanged`, after `SaveChangesAsync`). One message,
   `combatChanged`, carrying the receiver's own `CombatResponse` plus the summary:
   - the full view to `campaign:{id}:dm`;
   - once the combat has started, each **Player** member's own view to their
     `member:{id}` group. The campaign has at most a few players, so this is a handful
     of small payloads, and each is redacted for exactly its receiver;
   - a Draft goes to the DM group only.

   §9 says "per-combat DM and player groups". This uses the groups that exist instead
   (Notes, "Why no per-combat groups"). A role change needs nothing new: 13b's
   `NotifyMemberRoleChanged` already moves the connection in or out of the DM group,
   and the web refetches combats on `memberRoleChanged`.
8. **Tests.**
   - `CombatViewTests` (unit): for each PlayersSee × owner or not × hidden or not ×
     DM or player, exactly the fields above. `Tiebreak` is never serialised (checked on
     the JSON).
   - `CombatOrderTests` (unit): ties by `Tiebreak`, then `Id`; waiting combatants are
     not in the order; adding a combatant never moves the placed ones.
   - `CombatantDefaultsTests` (unit): numbering (`Goblin 1–4`, continuing to 5, a single
     taken name), HP rolled per copy with a seeded roller, the PlayersSee and Hidden
     defaults, and no stats for a player.
   - `CombatTests`: create in the current session, 409 with no session, list and get,
     Draft 404 for a player, `Finished` read-only (after 18b's finish, so it is added
     there).
   - `CombatantTests`: the table in 4, row by row, including a player adding someone
     else's character (403) and a DM adding a plain-name combatant.
   - `CombatLeakTests` (the core of 18a, one test each). Each plants a unique name
     (`zanthor`) and fetches `GET combats`, `GET combats/{id}` and the push as a player:
     1. a Draft combat's name;
     2. a hidden combatant's name, HP, AC, initiative and count;
     3. the turn on a hidden combatant (no id);
     4. a `Band` monster: no `hp`, `maxHp` or `ac` anywhere in the JSON;
     5. a `Nothing` monster: no band either;
     6. a combatant from a `DM` entry: hidden by default, and after the DM unhides it,
        `entryId` is still null for the player;
     7. another campaign's member: 404 on the combat, nothing in the list;
     8. the raw JSON of every player response contains no `tiebreak`.
   - `CombatHubTests`: a DM connection gets the full view and a player connection its
     own; a Draft reaches no player; a demoted DM stops getting the full view.

### 18b. Initiative, turns, finish and history (API)

1. **Roll** (`POST combats/{combatId}/roll`). One `InitiativeRolled(Actor, SessionId?,
   Rolls: [{ combatantId, total, roll, evaluation }])`.
   - **A DM** rolls every waiting combatant (§8). A **player** rolls only their own
     waiting combatants, and only in an `Active` combat. A roll with nothing waiting is
     a 200 that appends nothing.
   - **The first roll starts the combat**: `Draft` → `Active`, `Round` 1, `StartedAt`,
     and the turn on the top of the order. It also moves the combat to the **current
     session** (the event's `SessionId`), so a Draft prepared in Session 12 and run in
     Session 13 is a card in 13 (Notes). Starting with no combatants succeeds (13a).
   - **Later rolls slot the late joiners in** by sort, without re-rolling anyone, and
     the turn stays on the combatant who had it (13a's late joiners).
2. **End turn** (`POST combats/{combatId}/end-turn { combatantId, round }`). The body
   says whose turn the caller is ending. A stale body (someone else already ended it) is
   a 409 "The turn has already moved on.", which the web treats as done, so a double
   tap or two DMs at once end one turn. `TurnEnded(Actor, FromCombatantId,
   ToCombatantId, Round)`: the turn moves to the next combatant in the order, and past
   the last one it wraps to the top and `Round` goes up. Hidden combatants take turns
   like any other.
3. **Removing the turn's combatant** (18a's `DELETE`) moves the turn to the one after
   it in the same `Apply`, wrapping and counting the round as end turn does. Removing
   the last combatant leaves no turn.
4. **Reorder** (`PUT combats/{combatId}/combatants/{combatantId}/position { afterId? }`,
   DMs). The DM drags a combatant to just after `afterId`, or to the top when null
   (§8, "the DM can drag to reorder"). The server gives it the initiative of the
   neighbour it lands next to and a `Tiebreak` between its neighbours'. When there is no
   integer left between them, it respaces that initiative's ties evenly across the range
   in the same append. All of it is `CombatantEdited`, one per combatant that changed.
   Waiting combatants cannot be placed this way: roll them or type an initiative.
5. **Finish** (`POST combats/{combatId}/finish`, DMs). `CombatFinished(Actor)`: status
   `Finished`, `FinishedAt`, no turn. Allowed from `Active`, and from `Draft` as the way
   to discard one: a combat that never started stays DM-only (18a.4), so a discarded
   Draft shows only in a DM's list and never in the stream. Finishing an empty combat
   succeeds (13a).
6. **History** (`GET combats/{combatId}/history`, DMs). It reads the stream directly
   (§8, `FetchStreamAsync`), with no History list stored. Each row is `{ version,
   timestamp, actorMemberId, kind, text }`, where `text` is a sentence built on the
   server ("Sam rolled initiative: Goblin 2 rolled 17."). Players get 403: redacting past
   events per viewer is not in 18 (Notes).
7. **Concurrency.** Writes load the stream with `FetchForWriting<Combat>` and append at
   the version they read, so two writes at once fail one with a concurrency error. End
   turn and roll turn that into their 409 or no-op; edits retry once.
8. **Tests.**
   - `InitiativeTests`: the first roll starts the combat and moves its session; a player
     rolls only their own; late joiners slot in without re-rolling; ties never produce
     an ambiguous order (13a); a typed initiative is kept by the next roll.
   - `TurnTests`: the v1 lifecycle (13a's `FullCombatTest`, rewritten): Draft, add,
     roll, a player adds their character and it waits, the turn owner ends it, the
     next combatant has it, the wrap counts the round; the stale-turn 409; the DM ends
     anyone's turn; a player cannot end another's; removing the turn's combatant; the
     empty combat.
   - `CombatTurnTests` (unit): next-turn and wrap with hidden and waiting combatants,
     and reorder respacing.
   - `CombatHistoryTests`: one row per event, the actor on each, 403 for players.
   - `CombatLeakTests` gains: the roll's push to a player carries no hidden combatant's
     roll, and the turn moving onto a hidden combatant shows as no turn.

### 18c. The Combat tab and the combat page

1. **Routes.** `pages/app/campaigns/[campaignId]/combat.vue` becomes `combat/index.vue`
   (the list) and `combat/[combatId].vue` (a combat). A `combat.vue` beside a `combat/`
   folder would be a parent route that needs `<NuxtPage>`, so the file moves. The index
   keeps the route name `app-campaigns-campaignId-combat`, and `layouts/campaign.vue`
   already treats `…-combat-combatId` as the Combat tab (15's prefix rule). Both pages
   keep a single element root.
2. **Queries** (`utils/queries/combats.ts`). `getCombatsQuery` (the list) and
   `getCombatQuery` (one combat), plus a mutation per endpoint that writes the returned
   `CombatResponse` into the cache. `useCampaignHub` applies `combatChanged` to both
   caches, and refetches both on `memberRoleChanged` and on reconnect.
3. **The list** (`CombatList`). Sections **Live**, **Drafts** (DMs only) and
   **Finished** (the last 20, then "Show more"). Each row: name, "Round 3 · 6
   combatants", the session ("S13"). A DM has **＋ New combat** (`NewCombatDialog`: a
   name, prefilled "Combat", and Create, which opens the new combat). An empty list
   says "No combats yet." and, for a DM, offers New combat. For a player: "When your DM
   starts a combat, it shows here."
4. **The combat page** (§8, mobile first, invariant 11).
   - **Header**: name, `Draft` / `Round 3` / `Finished`, and the DM's actions: **Add**,
     **Roll** (the label says what it will do: "Start combat" on a Draft, "Roll 2
     waiting" later) and, in an overflow menu, **Finish** with a confirm.
   - **Initiative order** (`InitiativeList`, `CombatantRow`). One row per combatant, at
     least 44 px: the initiative, the name (linking to its entry when `entryId` is
     there), HP (`CombatantHp`: `31 / 45` with a bar for exact, a coloured
     Healthy / Bloodied / Down chip for a band, nothing for `Nothing`), AC in a shield
     when present, and conditions as chips. The turn's row is highlighted and scrolled
     into view when the turn moves. A DM's hidden rows are dimmed with an eye-off icon,
     and show their PlayersSee as a small label.
   - **Waiting** (`WaitingList`): the waiting combatants under the order, each with its
     roll expression, and the Roll button again.
   - **The bar** (`CombatBar`, sticky at the bottom, above the safe area): **End turn**,
     enabled for the turn's owner and for DMs, and labelled with the combatant ("End
     Goblin 2's turn" for a DM). A player whose turn it is sees "Your turn" on it.
   - **Players** see their own character first in the Add menu: **Add my character**
     (`AddMyCharacter`) lists their claimed Character entries that are not in the combat
     yet. It shows only on an `Active` combat. Then **Roll my initiative** while it
     waits.
5. **Adding combatants** (`AddCombatantsDialog`, DMs).
   - One text box with 15d's `@` picker behaviour through `EntryPicker` (`noCreate`,
     since creating entries from a fight would need a visibility decision the dialog
     does not ask for). Typing `goblin ×4`, `goblin x4` or `@Goblin ×4` picks Goblin with
     a count of 4 (`parseAddCombatants`, pure). A name that matches no entry offers
     **Add "Bandit" without an entry**.
   - Picks collect in a staged list: name, ×count (a stepper), and the defaults the
     server will use (HP expression, AC, initiative roll) shown as editable fields. A
     monster with no Stats says "No stats: HP and AC can be set later."
   - **Add n combatants** sends one request. The dialog stays open for more, with the
     staged list cleared, so a whole encounter goes in one sitting.
   - On a phone the dialog is a full-height sheet, and the picker's suggestions use the
     mention strip docked above the keyboard (§3a).
6. **Live.** Every write and push lands in the cache, so the page never refetches on
   its own writes. A combat that becomes invisible to the viewer (a push the viewer
   no longer gets after a role change) is dropped on the refetch. A 404 on the page
   says "This combat isn't available." with a link to the list.
7. **Tests** (`tests/unit/combat.test.ts`): `parseAddCombatants` (`×4`, `x4`, `@`, spaces,
   counts outside 1–20 clamped), the order and waiting split, the turn's row, the HP
   display for each redaction, and which actions each role sees in each status.

### 18d. The combatant sheet

1. **Opening.** Tapping a row opens `CombatantSheet` (Reka dialog, a bottom sheet on a
   phone) for a DM, or for a player on their own combatant. Anyone else's tap does
   nothing.
2. **HP** (`HpAdjust`). One number field with **Damage** and **Heal** buttons
   (`applyHpDelta`, pure: heal caps at `MaxHp` when there is one, damage goes as low as
   −999). Below it, Max HP and the current HP as plain fields. The field has
   `inputmode="numeric"` and `enterkeyhint="done"`, and Enter applies damage.
3. **Conditions** (`ConditionEditor`). Chips with a remove ×. **＋ Condition** offers the
   5e list (Blinded, Charmed, Deafened, Exhaustion, Frightened, Grappled, Incapacitated,
   Invisible, Paralyzed, Petrified, Poisoned, Prone, Restrained, Stunned, Unconscious,
   plus Concentrating) or any text, and an optional note (§1: a text label with an
   optional note).
4. **DM only.** Name, AC, initiative (a number, or clear it to make the combatant wait
   again), **PlayersSee** (`PlayersSeePicker`: `Exact` "HP and AC", `Band` "Healthy /
   Bloodied / Down", `Nothing` "No HP"), **Hidden** (a switch: "Hidden from players"),
   **Move up / Move down**, and **Remove** with a confirm. A player's own sheet shows
   HP, conditions and, while waiting, their initiative.
5. **Saving.** Each control saves as it changes (one `PUT` with the whole state), with
   the row updating from the response. There is no Save button, because at the table a
   DM applies damage and moves on.
6. **Drag to reorder** (`useReorderDrag`, DMs, placed combatants only). A handle on each
   row drags with pointer events (mouse and touch, with a 250 ms long-press on touch so
   scrolling still works). Dropping sends `position { afterId }` (`dropPosition`, pure).
   No new dependency: Reka has no sortable list, and this is one list of at most 50
   rows. Move up and Move down in the sheet are the accessible path.
7. **History** (`CombatHistorySheet`, DMs). From the header's overflow menu: 18b's
   rows, newest first, with the actor's name and a time.
8. **Tests** (`tests/unit/combatantSheet.test.ts`): `applyHpDelta` at the caps, condition
   add and remove, `dropPosition` at the top, middle and end, and which fields each role
   sees.

### 18e. Combat cards, the banner and entries' combats

1. **Cards in the stream** (API). `SessionStreamSession` gains `combats: CombatCard[]`
   (14's Notes reserved it), the combats in that session the caller can see.

   ```
   CombatCard { id, name, status, round, startedAt?, finishedAt?, createdAt,
                combatants: { name, entryId?, count }[] }
   ```

   `CombatCard.From(CombatResponse)` groups the caller's view of the combatants by
   entry (`4× @Goblin`) and lists plain names as they are, so a card holds nothing the
   combat page would not show the same viewer. DMs see their Drafts' cards. The
   `Combats` filter returns cards and no notes. `All` returns both. `Text`, `Images`,
   `Recaps` and `Mine` return notes only.
2. **Cards in the stream** (web). `SessionStream` merges each session's notes and
   combat cards by time (`startedAt ?? createdAt`). `CombatCard` draws §3's line: "⚔
   Combat · Goblin Ambush · 3 rounds · @Klarg, 4× @Goblin", with mention chips for
   entries the viewer can see, a Live badge while `Active`, and **Open**. The
   `combatChanged` push updates the card in place (`sessionStreamCache.ts`,
   `upsertCombatCard`), and a first roll that moves a combat's session moves its card.
   The Combats filter's empty state loses its "step 18" text: "No combats in these
   sessions."
3. **Join combat banner and the pulse.** While a combat the viewer can see is `Active`,
   the Campaign tab shows `JoinCombatBanner` above the stream ("⚔ Goblin Ambush is live
   · Round 2 · **Join combat**"; with two live combats it names the newest and says "+1
   more"), and the Combat tab's icon pulses (`animate-pulse`, off under
   `prefers-reduced-motion`). Both read `getCombatsQuery({ status: Active })`, which
   pushes keep current.
4. **Entries' combats** (§4's "COMBATS Goblin Ambush (S12)"). `GET
   entries/{entryId}/combats` returns the cards of combats in which the entry, or an
   entry merged into it, is a combatant the caller can see (`EntryIds` with the GIN
   `?|`). A player never gets a combat through a hidden combatant. The entry page shows
   them under COMBATS, below the gallery, each linking to its combat.
5. **The slim composer** (§3). The combat page has a ✎ button in its bar that opens
   the composer in `slim` mode: the text, the visibility picker and send, posting to
   the current session. It is not tied to the combat. `Composer` gets a `slim` prop that
   hides the session picker, the gap prompt and the image buttons.
6. **Tests.** `CombatStreamTests`: cards per session and viewer, the filters, a Draft
   card for DMs only, the card of a combat run in a later session. `EntryCombatsTests`:
   through a visible combatant, not through a hidden one, and through a merged entry.
   `combatCard.test.ts`: grouping, and the stream merge by time.

### 18f. Combats in ⌘K

1. **Provider** (`CombatSearchProvider`, a registration after the session provider, as
   17a.10 planned). It fills a `Combats` section, matching the query against the combat
   name and the names of the combatants the viewer can see, down 17a's ladder (exact,
   prefix, word prefix, substring, fuzzy).
2. **Visibility inside the SQL** (17a.6). A new `SearchVisibilitySql` fragment: DMs see
   every combat in the campaign, and players see a combat whose `StartedAt` is set.
   Combatant names are matched from `jsonb_array_elements(data -> 'Combatants')` with
   `NOT (c ->> 'Hidden')::boolean` for a player, so a hidden combatant's name matches
   nothing. `SearchVisibilityParityTests` checks the fragment against `CombatAccess`, and
   the provider re-checks each returned combat and throws `SearchDrift` as the others do.
3. **Hits.** `kind: "Combat"`, `combat: CombatCard`, ranked by the ladder, then live
   before finished, then newest. The row: ⚔, the name, "Live · Round 3" or "S12 ·
   Finished", and the matched combatant name when it was not the combat's. It opens
   `/combat/{id}`.
4. **"⚔ Start combat"** (§7) joins the registry: DMs only (`available` reads the role
   17c's context already carries), and only once a session exists. It goes to
   `combat?new=` with the query text, and the list page opens `NewCombatDialog` with
   that name, consuming the parameter once, as `?compose=` does.
5. **Tests.** `SearchLeakTests` gains a Draft combat's name and a hidden combatant's
   name, each absent for a player, and a started combat found by a visible combatant's
   name. `SearchTests` gains the section and its ranking. `searchActions.test.ts` gains
   Start combat's availability (DM only, needs a session) and target.
6. **Close the step:** tick this file's PR table and README's status, and describe
   anything built differently in the Notes.

## Verify

1. `dotnet test` and `pnpm build` pass, `vitest` and `npx nuxi typecheck` pass,
   `schema.d.ts` is fresh, and CI is green on every PR in the stack.
2. `pnpm dev` on an existing dev database starts cleanly and creates `mt_doc_combat`
   and its indexes. A second start changes nothing.
3. Three browser profiles, A (the owner, DM) at 1280 × 800, and B and C (Players) at
   390 × 844. B has claimed "Brynn" with Stats (HP `24`, AC 15, initiative `1d20+3`). The
   wiki has Goblin (Everyone, Stats `2d6`, AC 15, `1d20+2`) and Klarg (a `DM` entry).
   1. A opens Combat, **＋ New combat**, "Goblin Ambush". B and C see nothing in their
      Combat tab, and ⌘K `ambush` finds nothing for them.
   2. A adds `goblin ×4` and `@Klarg`. Goblin 1–4 have rolled HP. Klarg shows as
      hidden.
   3. A presses **Start combat**. Everyone's Combat tab pulses, and the Campaign tab
      shows "⚔ Goblin Ambush is live" and a card in the current session. B and C see
      Goblin 1–4 with Healthy chips and no AC, and no Klarg, not even in the order's
      gaps. In the dev tools, B's `GET combats/{id}` has no `klarg`, `hp` or `ac` for the
      goblins, and no `tiebreak`.
   4. B taps **Add my character**, then **Roll my initiative**. Brynn slots into the
      order without anyone else moving, showing `24 / 24` and AC 15 to everyone.
   5. A ends turns until Brynn's. B's bar says "Your turn". B ends it. C cannot end it.
      After the last combatant the round goes to 2.
   6. A applies 5 damage to Goblin 1: B sees it go to Bloodied or stay Healthy as the
      maths says. A sets Goblin 2 to `Nothing`: its chip disappears for B. A adds
      Poisoned to Goblin 3: B sees the chip.
   7. A unhides Klarg. B sees "Klarg" as plain text, not a link. A drags Klarg to the
      top; the order changes for everyone.
   8. A finishes the combat. The banner and the pulse stop. The card says "Goblin
      Ambush · n rounds · Brynn, 4× Goblin, Klarg", and the Combats filter shows only it.
      Goblin's entry page lists the combat under COMBATS. A opens the history.
   9. On B's phone profile, every control is reachable by touch, no sheet is under the
      keyboard, and the End turn bar sits above the home indicator.
   10. ⌘K `>start combat` as A opens New combat. As B, the action is not there.

## Notes / gotchas

- **Commit scopes:** `api`, `web` and `docs`. Every PR that changes an API contract
  regenerates `schema.d.ts` (`pnpm gen:api`) and commits it.
- **Decisions this file made where the design was open.** Each is a sensible default;
  change it here before the sub-step if you disagree.
  - **`TurnCombatantId`, not `TurnIndex`.** §8's model has `TurnIndex`. An index into
    the order moves whenever a late joiner is slotted in above the turn, a combatant is
    removed, or the DM reorders, so every one of those would have to fix it up. The id
    of the combatant whose turn it is stays right by itself.
  - **A combatant keeps an `InitiativeRoll` expression.** §8's model has only
    `Initiative`, but "the next roll slots them in" needs something to roll, and Stats
    hold an expression. Default `1d20`.
  - **The first roll is the start.** §8's seven events have no `CombatStarted`, and its
    endpoints no start, so `InitiativeRolled` on a Draft starts it. That keeps §8's
    event and endpoint lists as they are, apart from reorder and history (below).
  - **A Draft moves to the current session when it starts.** Invariant 3 is about
    exactly one session at a time. A Draft is prep (§12), and its card belongs where
    the fight happened.
  - **Players see a combat once it has started.** "Drafts are DM-only" (§8), and a
    Draft finished without starting (a discard) stays DM-only.
  - **Combatants from non-`Everyone` entries start hidden.** Nothing is revealed
    automatically (invariant 5).
  - **The owner sees their own combatant exactly**, whatever its PlayersSee.
  - **Conditions are visible to players** on every combatant they can see.
  - **Players edit their own HP and conditions**, and remove their own combatant. §8
    lists only adding and ending the turn; at the table, players track their own HP.
  - **Two endpoints beyond §8's eight:** reorder (`position`) and the DM's history.
    Reorder is not a field of the combatant `PUT` because the server, not the client,
    picks the `Tiebreak`.
  - **History is DM-only.** Redacting past events per viewer (a hidden goblin's rolls
    in an old `InitiativeRolled`) is its own piece of work, and the players have the
    card and the page.
  - **No delete.** A combat is finished, not deleted. There is no event for it (§8).
  - **Combatant names are copies.** Renaming Goblin does not rename Goblin 3.
    Mentions stay by id (invariant 6), but a combatant is a row with its own name.
- **Why no per-combat groups.** §9 says combat uses per-combat DM and player groups.
  Both jobs are done by groups that exist: `campaign:{id}:dm` gets the full view, and
  each player's `member:{id}` gets their own. Per-combat groups would need a join
  call from the combat page, tracking which connection watches which combat so a role
  change can move it, and would still not reach the banner, the card or the pulsing
  tab, which need the change on every tab. And a single "players" payload cannot
  carry "your own combatant exactly". At a handful of players, a payload each is cheap.
- **Edits are last-write-wins per combatant.** Two DMs editing one goblin at once is
  rare, and the loser sees the winner's value arrive by push. End turn and roll, where
  a race matters, are guarded (18b.2, 18b.7).
- **Band thresholds.** `Down` at `Hp ≤ 0`, `Bloodied` at `Hp ≤ MaxHp / 2` (integer
  division, so 7 of 15 is Bloodied), `Healthy` above. No band without `MaxHp`.
- **Stats rules come from 15g.** `EntryStats.For` is why a player cannot seed a goblin
  from its Stats, and why a DM's stats show only for Character entries. Entries of
  other kinds can still be combatants (a trap as an `Item`), with no defaults.
- **Merged entries.** A combatant keeps the loser's id. Reads resolve it through
  `MergedIntoId`, and entry combats query the winner's `MergedFromIds` too, the way
  mentions do (15g).
- **Seams for later steps.**
  - **Connections (19):** "Fought together" is two entries that are both visible
    combatants in one started combat, from `Combat.EntryIds` and the same redaction.
  - **Reference (20, 21):** "+ wiki" fills Stats, so `@Goblin ×4` works straight
    from an SRD monster.
  - **D&D Beyond (22):** refreshed max HP, AC and initiative bonus land in Stats, and
    the next combatant added from that character takes them.
- **As built, 18a.** Where it differs from 18a above:
  - `Combat.Apply(InitiativeRolled)` is in 18a, not 18b, so a started combat can be read
    and redacted in 18a's tests. They append the event directly (`CombatTestKit.Start`).
    18b adds the roll endpoint and the `TurnEnded` and `CombatFinished` applies.
  - Removing the turn's combatant already passes the turn on (`CombatOrder.After`, wrapping
    and counting the round), and so does clearing its initiative.
  - Redacted fields are left out of the JSON rather than sent as `null`, so a player's
    payload has no `hp`, `maxHp`, `ac` or `band` key at all. `CombatResponse` and the
    summary also carry `createdAt`.
  - A player's own combatant shows to them even when hidden, with its real `hidden`, so
    their `PUT` can echo it back.
  - A player's `POST combatants` may carry only `entryId`. Any other field is a 403.
  - A rolled max HP is clamped to 1…9,999.
  - Writes use `FetchForWriting`. A concurrent write is a 409 "The combat changed while you
    were saving." for now, with no retry; 18b.7 adds the retries.
  - The push to the DM group is one payload, redacted for a DM who created none of the
    entries. A combatant from a DM's own `Me` entry is plain text in the push and a link
    in that DM's reads.
  - `GET combats` filters `status` in memory, after the `CampaignId` query.
- **As built, 18b.** Where it differs from 18b above:
  - Every write goes through `CombatWrite.Write`, which loads with `FetchForWriting`, asks
    the endpoint for its events, and on a concurrency error decides again on the fresh
    state, up to 3 attempts (not "edits retry once"). So a stale end turn becomes its 409,
    a roll with nothing left waiting appends nothing, and edits, adds and removes land over
    the other write. Marten raises `EventStreamUnexpectedMaxEventIdException` here, which
    18a's `catch (ConcurrencyException)` did not catch. After 3 attempts it is still 409
    "The combat changed while you were saving."
  - Reorder's tiebreak needs an event field: `CombatantEdited` gained an optional
    `Tiebreak` (null keeps the combatant's own), since `CombatantState` is the `PUT`'s
    shape and carries none. A combatant placed after `afterId` takes that combatant's
    initiative; one dropped on top takes the initiative of the one below. Dropping a
    combatant where it already is, or after itself, appends nothing. A waiting combatant,
    moved or as `afterId`, is a 400.
  - End turn: a player sending the id of a combatant they cannot see gets the stale 409,
    not a 403, so a hidden combatant's turn does not leak. A Draft is a 409 "This combat
    hasn't started yet.". A lone combatant's end turn gives it the next round's turn.
  - A roll clamps each total to −99…99. Its `SessionId` is set only on the first roll.
  - History is `GET combats/{id}/history` → `{ items: [{ version, timestamp,
    actorMemberId, kind, text }] }`, oldest first, where `kind` is `Created`,
    `CombatantsAdded`, `CombatantEdited`, `CombatantRemoved`, `InitiativeRolled`,
    `TurnEnded` or `Finished`. The text replays the stream, so names are the ones in use
    at the time. A member who has left is "A former member".
  - Finish checks Finished (409) before the DM rule, so a player finishing a finished
    combat gets 409 rather than 403.
- **As built, 18c.** Where it differs from 18c above:
  - Every endpoint has a request in `utils/api/combat/` and a mutation in
    `utils/queries/combats.ts`, so 18d only draws the sheet. `getCombatsQuery` takes an
    optional `status` list (18e's banner), kept in the key as `"Active"` or `"all"`; a push
    updates every loaded list and drops a summary a filtered list does not want.
  - A write's response has no session number, so the lists take a summary built from the
    one they hold (`summaryFromCombat`), and are read again for a new combat or a first
    roll that moved it to another session. The push's own summary lands too.
  - End turn sends `{ combatantId: turnCombatantId, round }`; its 409 is treated as done
    and the combat is read again.
  - The add dialog matches with 15d's `matchEntries` over the whole entry directory rather
    than through `EntryPicker`, which lists only entries the viewer can edit and draws its
    own Create row. The suggestions sit under the box, not in the mention strip: on a
    phone the sheet ends at the keyboard (`useKeyboardInset`), so the box, the
    suggestions and the Add button all stay above it. An entry that is not `Everyone` says
    "starts hidden". Staged fields are blank-means-default; each entry row reads the
    entry (`getEntryQuery`) to show its Stats.
  - Players get no Add dialog. "Add my character" is one button with one claimed
    character and a menu with several.
  - A DM's Finish on a Draft reads "Discard draft". The DM's overflow menu has only
    Finish until 18d adds History.
  - The End turn bar says "Waiting for the turn" to a player while a hidden combatant
    has it.
- **As built, 18d.** Where it differs from 18d above:
  - The sheet also opens from the **Waiting** list, so a player can type their own
    initiative there. Nobody opens a sheet on a `Finished` combat (it is read-only).
  - A player's sheet has no Remove, as 18d.4 says, though the API lets them remove their
    own combatant.
  - `components/Combat/NumberField.vue` is added: the HP, Max HP, AC and initiative
    fields save on blur or Enter and keep the old value on a bad one.
  - Each save lands in the cache first (`withCombatantEdit`), the PUTs go one at a time,
    and only the last response is written back, so fast taps never undo each other on
    screen. A failed save toasts and reads the combat again. A reorder does the same
    (`withMovedCombatant`).
  - Setting Max HP on a combatant with no HP fills HP to it (`withMaxHp`). Damage with
    no HP starts from Max HP, or 0. Heal leaves HP already above Max HP where it is.
  - The drag handle also moves a row with the arrow keys, announced in a live region.
    A drag scrolls the page near its edges, and Escape cancels it.
  - A DM's overflow menu now always shows (History), with Finish below it while the
    combat is open. The history is read again each time it opens.
  - On a phone the sheet ends at the keyboard (`useKeyboardInset`), and nothing takes
    focus when it opens; with a mouse, the damage box does. Remove confirms inline in
    the sheet rather than in a second dialog.
- **Not in 18:** temporary HP, death saves, concentration checks, legendary actions,
  lair turns, ready or delay, combat-scoped notes, a combat log for players, deleting
  combats, and v1's Paused, stages, Quantity and CopyNumber (§8: gone).
