# 07 — Known bug fixes

## Goal

Fix three defects found while surveying the codebase. Independent of each other
and of everything else; each is small. **The first is a security hole and should
be done first.**

## Depends on

**03** — needs a building project to verify against.

## Files touched

- `apps/TakeInitiative.Api/src/Features/Campaigns/CampaignHub.cs`
- `apps/TakeInitiative.Api/src/Features/Combats/Models/Character/CharacterOriginDetails.cs`
- `apps/TakeInitiative.Web/utils/types/models.ts` **or** the C# `CombatState` enum

## Steps

### 1. `CampaignHub.Join` authorises nothing — SECURITY

The membership check is a tautology:

```csharp
x.UserId == x.UserId     // always true
```

So **any authenticated user can join any campaign's SignalR group** and receive
its `campaignStateUpdated` and `campaignMemberStateUpdated` broadcasts, for a
campaign they are not a member of.

Fix: compare against the caller's id read from the authenticated connection.

**Correction (verified during execution): `CombatHub.JoinCombat` was NOT correct.**
Its signature was `JoinCombat(IDocumentStore Store, Guid UserId, Guid CombatId)` —
the user id came *from the client*, so any authenticated user could pass a known
member's id and join that combat's group. Same hole, different mechanism. Both
hubs are fixed; the fix changes the SignalR contract, so the Vue client calling
`joinCombat` / `leaveCombat` had to change in lockstep.

**Add a test.** This is exactly the class of bug that silently returns after a
refactor: a test asserting a non-member is rejected from `Join` is worth more than
the fix.

### 2. `CharacterOriginDetails` factories all return the same origin

All three factory methods set `CharacterOrigin = PlayerCharacter` — a copy-paste
slip. Each should set its own origin (player character / planned character /
ad-hoc staged character; confirm the exact enum members against
`CharacterOriginOptions`).

Check what actually consumes `CharacterOrigin` before fixing — if the frontend
branches on it, correcting this may visibly change behaviour in combat, which is
good but should be verified rather than discovered later.

### 3. `CombatState` has drifted between C# and TypeScript

| Value | C# (`CombatState`) | TS (`utils/types/models.ts`) |
|---|---|---|
| 0 | `Started` | `Open` |
| 1 | `InitiativeRolled` | `Started` |
| 2 | `Paused` | `Paused` |
| 3 | `Finished` | `Finished` |

Same wire values, different meanings for 0 and 1. Anywhere the frontend branches
on `Open` or `Started` it is reasoning about the wrong state.

Fix the **TypeScript** side to match C#, since C# is what the projection writes
and the database holds. Then check every frontend usage of `CombatState` — the
combat page, `useCombatStore`, and any `v-if` on combat state — because some may
have been written to compensate for the drift and will now be doubly wrong.

Note the frontend's Zod schemas (433 lines mirroring the C# models by hand) are
the root cause. Stage 3 replaces them with generated types; this is a point fix,
not the cure.

## Verify

```bash
dotnet build --configuration Release -p:TreatWarningsAsErrors=True
dotnet test
```

Manually, for each:

1. **CampaignHub** — with two accounts, confirm a non-member cannot receive
   broadcasts for a campaign they aren't in. The new test should cover this, but
   verify once by hand.
2. **CharacterOriginDetails** — stage characters from all three origins in a
   combat and confirm each is labelled correctly.
3. **CombatState** — walk a combat through every state (start → roll initiative →
   pause → resume → finish) and confirm the UI shows the right thing at each step.

## Notes / gotchas

- These are independent. If one turns out to be bigger than expected, do the
  others and split it out rather than blocking the step.
- The `CampaignHub` fix is the only one with a security implication. If time is
  short, do that one and defer the rest.
- More latent issues were noted during the survey but deliberately left out of
  scope here: several `CombatProjection.Apply` methods are `async` and take
  `IQuerySession` to load `ApplicationUser` for history entries — an inline
  projection doing database reads is a performance and correctness smell. Stage 3
  rewrites the projections, so leave it.
