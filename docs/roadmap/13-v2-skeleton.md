# 13 — v2 skeleton

## Goal

Replace v1 in place with the smallest v2 that runs end to end: a Campaign with
members and roles, the existing auth, web types generated from the API's OpenAPI
document, and an installable PWA shell with **Campaign**, **Wiki** and **Combat**
tabs plus a search button. The Wiki and Combat tabs are empty.

The step ships as four PRs stacked with `gh stack` on top of this file's docs
PR, one per sub-step. Each PR leaves `dev` runnable as **v2 so far**:

| PR | Sub-step | Runnable state after merge |
|---|---|---|
| 13a | Delete v1 combat | Sign up, create a campaign, join it. There is no combat |
| 13b | Campaign model | The same flows, on the v2 Campaign stream with members and roles |
| 13c | Generated API types | The same flows, with `models.ts` gone |
| 13d | PWA shell | The step's Verify passes |

v1 combat comes back as Combat v2 in step 18. The app has no combat until then,
which is fine because v2 starts with no data and has no users to protect.

## Depends on

**12**. The glossary (§1) and invariants (§10) in
[12-v2-design-session.md](12-v2-design-session.md) are binding.

## Files touched

Paths are relative to `apps/TakeInitiative.Api` (API), `apps/TakeInitiative.Api.Tests`
(Tests) and `apps/TakeInitiative.Web` (Web).

**13a: delete**
- API `src/Features/Combats/**` (155 files, including `CombatHub`)
- API `src/Features/Campaigns/Api/CampaignMember/**`: the player character and resource endpoints
- API `src/Features/Campaigns/Api/GetCampaign/CombatHistoryDto.cs`, `CurrentCombatDto.cs`
- API `src/Features/Campaigns/Models/CampaignSettings.cs`: the combat display settings, replaced by per-combatant `PlayersSee` in step 18
- API `src/Features/Admin/Api/PutReprojectCombats/**`, `PutReadAndSaveCampaigns/**`
- API `src/Utilities/InitiativeComparer.cs` and the `InitiativeRoller` / `HealthRoller` adapters. `packages/TakeInitiative.Dice` stays
- Tests `Scopes/Integration/Features/Combat/**`, `Scopes/Unit/InitiativeOrderingTests.cs`, `InitiativeRollerTests.cs`
- Web `pages/app/campaigns/[campaignId]/combats/**`, `components/Campaign/Combat/**`, `components/Campaign/Character/**`
- Web `components/Campaign/{Create,Edit}PlayerCharacterForm.vue`, `components/Campaign/Player/**`
- Web `utils/api/combat/**`, `utils/api/plannedCombat/**`, the player character and resource requests in `utils/api/campaign/`
- Web `utils/queries/combats.ts`, `composables/{combatControls,useCombatStore,useDraftCombatHelper}.ts`
- Web `layouts/campaignCombats.vue`, `middleware/join_leave_combat_navigation.global.ts`, `utils/forms/{armorClass,health}FormValidator.ts`, `utils/Conditions.ts`

**13a: modify**
- API `Program.cs`: unmap `/combatHub`
- API `src/boostrap/Bootstrap.cs`: drop the `PlannedCombat` schema, `CombatProjection` and the roller registrations
- API `src/Features/Campaigns/Models/Campaign.cs`: drop `PlannedCombatIds`, `ActiveCombatId`, `CampaignSettings`
- API `src/Features/Campaigns/Models/CampaignMember.cs`: drop `Characters`
- API `src/Features/Campaigns/Api/GetCampaign/*`: drop the combat fields
- Web `pages/app/campaigns/[campaignId]/index.vue`, `settings.vue`: drop combat history, the join banner, player characters and display settings
- Web `utils/types/models.ts`: drop the combat and character schemas
- Web sidebar and navigation: drop the combat links

**13b: delete**
- API `src/Features/Campaigns/Models/CampaignMember.cs`, `CampaignMemberInfo.cs`, `CampaignMemberResource.cs`
- API `src/Features/Campaigns/Api/{PutCampaignDetails,DeleteCampaign}/**` (see Notes)
- API `src/Features/User/Api/GetUser/GetUserCampaignDto.cs`
- API `src/Utilities/CampaignIdShortener.cs`, replaced by a stored join code

**13b: add**
- API `src/Features/Campaigns/Models/Actor.cs`, `Role.cs`, `Member.cs`
- API `src/Features/Campaigns/Models/Events/{CampaignCreated,MemberJoined,MemberRoleChanged}.cs`
- API `src/Features/Campaigns/Models/Campaign.cs`: rewritten as the inline projection of the Campaign stream
- API `src/Features/Campaigns/Api/{PostCreateCampaign,PostJoinCampaign,GetCampaign,GetCampaigns,PutMemberRole}/**`
- API `src/boostrap/CorrelationMiddleware.cs`
- Tests `Scopes/Integration/Features/Campaign/CampaignTests.cs`

**13b: modify**
- API `src/boostrap/Bootstrap.cs`: register the Campaign projection and turn on event metadata
- API `src/Features/Campaigns/CampaignHub.cs`: v2 groups
- API `src/Features/User/Models/ApplicationUser.cs`: drop `Campaigns`
- API `src/Features/User/Api/GetUser/*`, `PostSignUp/*`: stop reading and writing `ApplicationUser.Campaigns`
- API `src/Utilities/Extensions/IDocumentSessionExtensions.cs`: membership checks read the Campaign projection
- Tests `Scopes/Integration/Features/Campaign/HubMembershipTests.cs`
- Web `components/Campaign/{CreateForm,JoinForm,Share,CampaignCard}.vue`, `utils/api/campaign/*`, `utils/queries/campaign.ts`, `utils/types/models.ts`, the campaign pages

**13c**
- API `TakeInitiative.Api.csproj`: add `FastEndpoints.Swagger`, remove `Swashbuckle.AspNetCore` and `Microsoft.AspNetCore.OpenApi`
- API `Program.cs`: `SwaggerDocument()` and an `--export-openapi` mode that writes the document and exits
- API `package.json`: `gen:openapi` script
- Web `package.json`: add `openapi-typescript`, and a `gen:api` script
- Web `utils/api/schema.d.ts`: generated and committed
- Web delete `utils/types/models.ts`. Every `utils/api/**` request uses generated types, and forms keep their own local zod schemas for validation only
- `turbo.json`: a `gen:api` task that `@ti/web#build` depends on
- `.github/workflows/testWeb.yml`: fail when the committed `schema.d.ts` is stale

**13d**
- Web `package.json`: add `@vite-pwa/nuxt`
- Web `nuxt.config.ts`: the `pwa` block and viewport meta from Ripple (`apps/pwa-nuxt/nuxt.config.ts`)
- Web `public/sw.js`: a minimal service worker, with no precaching
- Web `public/icons/*`: manifest icons (192, 512, maskable)
- Web `layouts/campaign.vue`: rewritten as the tab shell (header with search button, bottom tabs)
- Web `pages/app/campaigns/[campaignId]/{index,wiki,combat}.vue`: the three tabs
- Web delete the v1 layouts, components and pages the shell replaces (`AppSidebar`, `AppNavigationBar`, `layouts/mainApp.vue` and others found while building)

## Steps

### 0. Start the stack

```sh
git switch dev && git pull
gh stack init --base dev v2/13-step-file
```

This file and its README row are the bottom PR of the stack. Add each sub-step
on top with `gh stack add v2/13a-delete-v1-combat`, `v2/13b-campaign-model` and
so on.

### 13a. Delete v1 combat

1. **Record what the combat tests prove before deleting them.** Summarise
   `FullCombatTest`, `ComplexInitiativeTest`, `CharactersAddedAfterCombatStartedTest`
   and `EmptyCombatTest` as a behaviour list in the Notes of this file. Step 18
   rewrites them for the 7-event model from that list.
2. Delete the API combat feature, `CombatHub`, the player character and resource
   endpoints, campaign display settings and the two combat admin
   endpoints (Files touched). Remove what now fails to compile from `Campaign`,
   `CampaignMember`, `GetCampaign` and `Bootstrap`.
3. Delete the web combat pages, components, requests, queries, composables,
   layout and middleware. Strip combat, player characters and display settings
   from the campaign overview and settings pages.
4. `dotnet build`, `dotnet test` and `pnpm --filter @ti/web build` pass.
5. `grep -rniE "combat|plannedcombat|initiative" apps/` finds only the kept dice
   usage and nothing dangling.

### 13b. Campaign model

1. **Glossary check.** `Campaign`, `Member`, `Role` (`DM`, `Player`) and **owner**
   are in §1. `Actor` and **join code** are not, so add them to §1 first:
   - **Actor**: who caused an event. Today always a member. Not called "user".
   - **Join code**: the short code a user enters to join a campaign as a Player.
2. **Events and projection.** One Marten stream per campaign:

   ```
   Actor             { MemberId }                       // later: | Model { Name, Version, Confidence }
   CampaignCreated   { Actor, Name, OwnerMemberId, OwnerUserId, JoinCode }
   MemberJoined      { Actor, MemberId, UserId }        // joins as Player
   MemberRoleChanged { Actor, MemberId, Role }

   Campaign (inline projection)
     { Id, Name, JoinCode, OwnerMemberId, CreatedAt,
       Members[] { MemberId, UserId, Role, JoinedAt } }
   ```

   Every event implements `IActorEvent { Actor Actor }`. `CampaignCreated` makes
   the owner the first member, with role `DM`. The owner can never be demoted.
   Membership lives only in `Campaign.Members`; nothing else stores it.
3. **Event metadata.** In `AddMartenDB`, turn on
   `opts.Events.MetadataConfig.CorrelationIdEnabled`, `CausationIdEnabled` and
   `HeadersEnabled`. `CorrelationMiddleware` sets the session's correlation id
   from the request's trace id, so every event appended in one request shares it.
4. **Indexes.** A unique index on `Campaign.JoinCode`, and a GIN index so
   `Members.Any(m => m.UserId == id)` is cheap.
5. **Endpoints.**

   | Endpoint | Who | Does |
   |---|---|---|
   | `POST /api/campaigns` | any user | appends `CampaignCreated` |
   | `POST /api/campaigns/join` | any user, by join code | appends `MemberJoined`. Joining twice is a no-op |
   | `GET /api/campaigns` | any user | the caller's campaigns (replaces `ApplicationUser.Campaigns`) |
   | `GET /api/campaigns/{id}` | members | the campaign with its members, their usernames and roles |
   | `PUT /api/campaigns/{id}/members/{memberId}/role` | owner | appends `MemberRoleChanged` |

   Every write resolves the caller's `MemberId` first and puts it on the event's
   `Actor`. A non-member gets 403 before anything is appended.
6. **Auth.** Keep the Marten Identity setup (`src/Identity/**`, cookie and JWT
   config, `TakePolicies`). Remove `ApplicationUser.Campaigns` and every read
   and write of it.
7. **CampaignHub.** Groups `campaign:{id}`, `campaign:{id}:dm` and `member:{id}`
   (§9). `Join` checks membership against the projection and adds the connection
   to `campaign:{id}`, `member:{memberId}`, and `campaign:{id}:dm` for DMs.
   `MemberJoined` and `MemberRoleChanged` notify `campaign:{id}`. A role change
   also moves that member's connections in or out of the DM group, or tells them
   to reconnect.
8. **Tests.** `CampaignTests` covers: create makes the owner a DM; join by code
   adds a Player; joining twice is a no-op; only the owner changes roles; the
   owner cannot be demoted; every event carries an `Actor` and a correlation id.
   Update `HubMembershipTests` for the new groups.
9. **Web.** Point the existing create, join and campaign pages at the new
   endpoints with the fewest changes. They are replaced in 13d.

### 13c. Generated API types

1. Swap Swashbuckle for `FastEndpoints.Swagger`, which understands FastEndpoints'
   request and response DTOs. Remove `Microsoft.AspNetCore.OpenApi` if nothing
   else needs it.
2. Add an export mode: `dotnet run -- --export-openapi <path>` writes the document
   and exits without touching the database. `pnpm --filter @ti/api gen:openapi`
   writes it to `apps/TakeInitiative.Web/openapi.json`, which is gitignored.
3. `pnpm --filter @ti/web gen:api` runs `openapi-typescript openapi.json -o
   utils/api/schema.d.ts`. Commit `schema.d.ts` so the web builds without .NET.
4. Move each request in `utils/api/**` to the generated types
   (`components["schemas"]["…"]`). Keep axios.
5. Delete `utils/types/models.ts`. Forms that validated with its schemas get a
   small local zod schema instead.
6. CI: `testWeb.yml` regenerates and fails on a diff in `schema.d.ts`.

### 13d. PWA shell

1. Add `@vite-pwa/nuxt` with Ripple's config: `strategies: 'injectManifest'`,
   `srcDir: '../public'`, `filename: 'sw.js'`, `registerType: 'autoUpdate'`,
   `injectManifest.injectionPoint: undefined` (no precaching), `devOptions.enabled`.
   Manifest: name "Take Initiative", `display: standalone`, `orientation: portrait`,
   `start_url: /app`, the theme colour from `assets/index.css`, and the icons.
2. `public/sw.js`: install and activate handlers only, no fetch caching (offline
   is out of scope per §12).
3. Viewport meta from Ripple (`interactive-widget=overlays-content`), plus
   safe-area insets on the header and tab bar.
4. **Shell.** One responsive layout: a header with the campaign name and a 🔍
   button, the tab content, and bottom tabs **Campaign · Wiki · Combat** with
   touch targets of at least 44px. On desktop the tabs can move to the side, and
   the layout stays the same.
5. **Campaign tab.** Members with their roles, the join code with a share or copy
   button, and, for the owner, a control to change a member's role. It stays
   live over `CampaignHub`.
6. **Wiki and Combat tabs.** Empty states that name the step that fills them.
7. **Search button.** Opens an empty full-screen sheet. ⌘K opens the same sheet on
   desktop. Search itself is step 17.
8. Delete the v1 shell pieces the new layout replaces.

## Verify

From a clean clone, with no GitHub PAT:

1. `pnpm install && pnpm dev` starts the API, the web app and Postgres.
2. In a browser at a phone size (390 × 844):
   1. Sign up as user A and create a campaign. The Campaign tab shows A as **DM**.
   2. In a second browser profile, sign up as user B and join with the code. B is
      a **Player**, and A's Campaign tab shows B without a reload.
   3. A promotes B to DM. B's role updates live.
   4. The Wiki and Combat tabs show their empty states. 🔍 opens the search sheet.
3. Chrome DevTools > Application > Manifest shows no errors, and "Install app"
   installs it. It opens standalone at `/app`.
4. In Postgres, `mt_events` rows for the campaign have `correlation_id` set and
   every event body has an `actor`.
5. `dotnet test` and `pnpm build` pass. CI is green on every PR in the stack.

## Notes / gotchas

- **Commit scopes:** only `api`, `web`, `identity`, `dice`, `root`, `ci` and
  `docs` pass the husky hook.
- **Every PR in the stack must run on its own.** If one grows past a comfortable
  review, split it inside the stack (13b-1, 13b-2) rather than letting it grow.
- **Dropped without replacement in 13b:** editing campaign details and deleting a
  campaign. Neither is in the three 13b events. They come back as
  `CampaignRenamed` / `CampaignDeleted` when a step needs them, with the glossary
  checked first.
- **Player characters and resources** are deleted in 13a. Player characters come
  back as claimed Character entries (§1) in step 15. Resources have no v2
  equivalent.
- **Existing v1 data** in a developer's local Postgres is incompatible after 13a.
  Drop the dev database (`docker compose -p takeinitiative -f compose.dev.yml
  down -v`). There is no migration, per §12.
- **Marten is on 7.31.** Upgrading to 8 is out of scope for this step.
- **Hidden combatants and Paused are not ported.** v1 leaks hidden NPCs because it
  filters them in the browser, and it checks for a Paused state in about 7
  commands that can never be reached. Both leave with the rest of v1 combat in
  13a, and step 18 must not bring them back (design §8).

### Deviations and decisions in 13b

- **Membership index.** Marten 7 turns `Members.Any(m => m.UserId == id)` into
  `data -> 'Members' @> '[{"UserId": …}]'`. A whole-document `GinIndexJsonData()`
  cannot serve that expression, so 13b adds a GIN index on `(data -> 'Members')`
  instead (`mt_doc_campaign_idx_members`). `EXPLAIN` shows a bitmap index scan on it.
- **Join codes** are 8 random characters from an alphabet without `0/O/1/I/L`,
  stored on `CampaignCreated` and in `Campaign.JoinCode` (unique index). Join
  trims and upper-cases the code. An unknown code is a 400 on `joinCode`.
- **Responses.** Create, join, get and the role change all return the same
  `CampaignResponse` { id, name, joinCode, ownerMemberId, createdAt,
  currentMemberId, members[] { memberId, userId, username, role, joinedAt,
  isOwner } }. `GET /api/campaigns` returns `{ campaigns[] { id, name, role,
  isOwner, memberCount } }`. `GET /api/user` no longer lists campaigns.
- **Role changes.** Setting the role a member already has appends nothing. The
  owner cannot be set to `Player` (400). A non-owner DM gets 403.
- **Metadata.** The correlation id is the request's W3C trace id
  (`Activity.Current`), falling back to `HttpContext.TraceIdentifier`, and is
  echoed in `X-Correlation-Id`. Each event also gets a `request` header
  (`METHOD /path`). Causation is enabled but unset until events cause events.
- **Enums** are stored as strings (`EnumStorage.AsString`), and `Role` has a
  `JsonStringEnumConverter`, so the API sends `"DM"` / `"Player"`.
- **Hub DM group.** A role change moves the member's live connections in or out
  of `campaign:{id}:dm` through an in-memory `CampaignConnections` registry. That
  assumes one API instance; scaling out needs a backplane or a reconnect message.
- **Dropped:** the "you already own a campaign with that name" check (the
  projection stores the owner's member id, not their user id), the web settings
  page (rename, delete) and the campaign introduction, which has no v2 event.
  The owner's role control sits on the campaign overview until 13d.
- **Local dev Postgres** is `takedb` on port 7401 (`compose.dev.yml`). Port 5432
  on this machine can belong to another project.

### Behaviour kept from the v1 combat tests (filled in during 13a)

The v1 tests were snapshot tests (`CombatVerifier`) with a faked dice roller and
initiative roller. Step 18 rewrites these behaviours against the 7-event model.
Words in brackets are the v1 names.

**Lifecycle** (`FullCombatTest`, `EmptyCombatTest`)
- A DM creates a Draft combat [planned combat], adds combatants, then starts it
  [open]. A player then adds their own combatant [staged character].
- Starting a combat with no combatants succeeds.
- The DM finishes a combat. Finishing an empty combat succeeds.
- The player whose combatant has the turn ends it. The turn then moves to the
  next combatant in initiative order.

**Combatants** (`FullCombatTest`)
- Each combatant has a name, AC (optional), HP (none, or current and max), an
  initiative expression, and a hidden flag.
- HP given as a roll (`20d20 + 10`) is rolled once, when the combatant is added.
- The DM edits a combatant: un-hides it, changes current HP, and adds and removes
  a condition (a name plus a note).
- A combatant can be removed after the combat has started.
- v1 `Quantity = 10` split one planned NPC into 10 copies. v2 uses `@Goblin ×4`.

**Late joiners** (`CharactersAddedAfterCombatStartedTest`)
- A combatant added after the combat starts waits without an initiative, and the
  next roll slots it into the existing order without re-rolling anyone else.

**Ties** (`ComplexInitiativeTest`, `InitiativeRollerTests`, `InitiativeOrderingTests`)
- Equal initiatives never produce an ambiguous order. v1 re-rolled a d20 per tie,
  kept every roll as an array, and sorted the arrays lexicographically (for
  example `[24, 4, 16]` before `[24, 4, 11]` before `[22]`). A late joiner that
  tied an existing combatant rolled again against it only.
- v2 replaces all of this with one int plus a hidden random `Tiebreak` (design §8).
  The rule to keep: the sort is total and stable, and adding a combatant never
  reorders the ones already placed.
