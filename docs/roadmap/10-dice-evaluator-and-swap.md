# 10 — Dice evaluator and swap

## Goal

Evaluate the typed AST, render the `Evaluation` string, and switch `IDiceRoller`
over to the C# implementation. At the end the app runs entirely on the new roller.
Python is still installed, but nothing calls it.

## Depends on

**09**.

## Files touched

New:
- `apps/TakeInitiative.Api/src/Utilities/DiceRoller/Language/DiceEvaluator.cs`
- `apps/TakeInitiative.Api/src/Utilities/DiceRoller/Language/EvaluationFormatter.cs`

Modified:
- `apps/TakeInitiative.Api/src/Utilities/DiceRoller/DiceRoller.cs` — reimplemented
- `apps/TakeInitiative.Api/src/Utilities/DiceRoller/IDiceRoller.cs` — add `Check`
- `apps/TakeInitiative.Api/src/boostrap/Bootstrap.cs` — DI registration

## Steps

### 1. Evaluator

Walk the `TypedAst`, rolling pools and folding arithmetic.

**The RNG is injected.** `DiceEvaluator(Random random)` — a hard requirement from
spec §8, because deterministic tests are otherwise impossible and every assertion
becomes a range check.

Register `Random.Shared` in production. `Random.Shared` is thread-safe; a plain
`new Random()` shared across requests is not.

For each pool, keep the individual die results and which were dropped — the
formatter needs both.

### 2. Formatter

Implement spec §6 exactly:

| Expression | `Evaluation` |
|---|---|
| `1d20 + 3` | `1d20 [14] + 3 = 17` |
| `2d6 + 1d4 + 2` | `2d6 [3, 5] + 1d4 [2] + 2 = 12` |
| `adv(1d20) + 5` | `adv(1d20) [18, ~7] + 5 = 23` |
| `kh2(4d6)` | `kh2(4d6) [6, 5, ~3, ~1] = 11` |
| `10 - 1d4` | `10 - 1d4 [3] = 7` |

Kept dice first, then dropped (prefixed `~`), each group descending.

Render from the **AST**, not the raw input string, so `1D20+3` normalises to
`1d20 [14] + 3 = 17`.

### 3. Extend `IDiceRoller`

```csharp
public interface IDiceRoller
{
    Result<DiceRoll> EvaluateRoll(string roll);
    DiceRoll RollD20();
    Result<TypedAst, DiceError[]> Check(string roll);   // new
}
```

`Check` is what the validators want — step 11 points them at it. Adding it here
keeps step 11 to deletions.

### 4. Reimplement `DiceRoller`

Replace the `Py.GIL()` / `Py.Import("d20")` body with parse → check → evaluate.
`RollD20()` stays `EvaluateRoll("1d20").Value`.

Keep returning `Result<DiceRoll>` with the same failure semantics, but build the
message from the structured `DiceError`s rather than an exception string.

### 5. DI

In `Bootstrap.AddDiceRollers`, register the new `DiceRoller` **without** the
`PythonEngine` initialisation block.

**Leave the Python bootstrap code in place but unreferenced** — do not delete it
here. If something is wrong with the new roller, reverting is then a one-line DI
change rather than a git archaeology exercise. Step 11 removes it for real.

## Verify

```bash
dotnet build --configuration Release -p:TreatWarningsAsErrors=True
dotnet test
```

Then, with a seeded `Random`, assert exact totals and exact `Evaluation` strings
for the spec §6 table. Deterministic tests are the whole reason the RNG is
injected — use them.

Manually, the paths that actually matter:

1. `pnpm dev`, create a campaign
2. Add a character with initiative `1d20 + 2` — saves
3. Add one with `adv(1d20)` — saves
4. Add one with `2d20kh1` (legacy form) — saves
5. Add one with `adv(2d6)` — **rejected**, with a readable message
6. Start a combat, roll initiative, confirm ordering and the tie-break rerolls in
   `InitiativeRoller` still behave
7. Add a character with a rolled health value — exercises `HealthRoller`

Confirm Python is genuinely unused:
```bash
# with the venv temporarily renamed, the app must still work
mv .venv .venv.off && pnpm dev    # roll dice; then: mv .venv.off .venv
```

That is the real proof, and worth doing before step 11 deletes anything.

## Notes / gotchas

> **CHECKPOINT — stop and report here.** This is the last point at which falling
> back to Python costs one line. Do not start step 11 until this has been seen
> working.

- **`InitiativeRoller` is the subtle consumer.** ~160 lines that group characters
  with equal roll prefixes and reroll d20s until the ordering is unique. It calls
  `RollD20()` repeatedly. Its existing tests (`InitiativeRollerTests`,
  `InitiativeOrderingTests`) stub `IDiceRoller` via NSubstitute, so they will
  pass regardless — they do **not** prove the new roller works. Test manually.
- Existing stored expressions are in legacy syntax. Step 09 supports it, but this
  is the step where that gets exercised against real data. If there is a
  production database, spot-check a few stored `PlayerCharacter` initiative
  values parse.
- `Result<DiceRoll>` is `CSharpFunctionalExtensions`. Keep using it; the codebase
  is railway-oriented throughout.
- Watch for `RollD20()` calling `EvaluateRoll("1d20").Value` — `.Value` throws on
  failure. It cannot fail for a literal, but if the limit checks are ever
  misconfigured it would throw at an awkward moment. Consider constructing the
  d20 roll directly.
