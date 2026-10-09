# 10 — Swap the API onto the dice library

## Goal

Point `IDiceRoller` at `TakeInitiative.Dice`. At the end the app runs entirely on
the new roller. Python is still installed and bootstrapped, but nothing calls it.

## Depends on

**09**.

## Files touched

- `apps/TakeInitiative.Api/TakeInitiative.Api.csproj` — `ProjectReference` to `packages/TakeInitiative.Dice`
- `apps/TakeInitiative.Api/src/Utilities/DiceRoller/` — `DiceRoll.cs` deleted (the library owns it); `DiceRoller.cs` reimplemented; `IDiceRoller.cs` gains `Check`
- The validators that inject `IDiceRoller` — switch to `Check`
- `apps/TakeInitiative.Api/src/boostrap/Bootstrap.cs` — DI registration
- `apps/TakeInitiative.Api/dockerfile`, `compose.dev.yml` — build context becomes the repo root, so the API image can see `packages/`

## Steps

### 1. Interface

```csharp
public interface IDiceRoller
{
    Result<DiceRoll> EvaluateRoll(string roll);
    DiceRoll RollD20();
    UnitResult<string> Check(string roll);
}
```

`Check` returns the joined DM-facing messages on failure, which is what the
FluentValidation `AddFailure` call sites want.

### 2. Implementation

`DiceRoller(Random random)` calls `DiceLanguage.Roll` / `DiceLanguage.Check` and
maps `DiceError[]` to a message. Register it with `Random.Shared` — thread-safe,
unlike a shared `new Random()`.

`RollD20()` builds its result directly rather than `EvaluateRoll("1d20").Value`,
so a misconfigured limit can never throw from inside `InitiativeRoller`.

### 3. Validators

Every validator that calls `EvaluateRoll` purely to decide validity calls `Check`
instead. Afterwards, `grep EvaluateRoll` should only find places that genuinely
want a number (`HealthRoller`, `UnevaluatedCharacterInitiative.RollInitiative`).

### 4. DI

Register the new roller in `Bootstrap.AddDiceRollers`. **Leave the Python
initialisation block in place but unreferenced by the roller** — reverting is then
a one-line change. Step 11 removes it.

### 5. Docker

The API Dockerfile used `apps/TakeInitiative.Api` as its build context, which
cannot see `packages/`. Move the context to the repo root (as the web image
already does) and copy both project files before restore.

## Verify

```bash
dotnet build --configuration Release -p:TreatWarningsAsErrors=True
dotnet test
docker compose -p takeinitiative -f compose.dev.yml build api
```

Manually, `pnpm dev`:

1. Add a character with initiative `1d20 + 2` — saves
2. Add one with `adv(1d20)` — saves
3. Add one with `2d20kh1` — **rejected**: "write this as kh1(2d20)"
4. Add one with `adv(2d6)` — **rejected** with a readable message
5. Start a combat, roll initiative, confirm ordering and tie-break rerolls
6. Add a character with rolled health — exercises `HealthRoller`

## Notes / gotchas

> **CHECKPOINT — stop and report here.** This is the last point at which falling
> back to Python costs one line.

- **`InitiativeRoller` is the subtle consumer.** It groups characters with equal
  roll prefixes and calls `RollD20()` until ordering is unique. Its tests stub
  `IDiceRoller`, so they pass regardless — test manually.
- `Result<DiceRoll>` is `CSharpFunctionalExtensions`; the codebase is
  railway-oriented throughout.
- Stored expressions in legacy syntax will now fail validation. That is accepted:
  there is no backwards-compatibility obligation and the schema is being redone.
