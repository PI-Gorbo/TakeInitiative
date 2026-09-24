# 09 — Dice library: parser, type checker, evaluator

## Goal

Implement the whole language from the spec — parse, type-check, evaluate, format —
as a self-contained, unit-tested .NET project. **Wired to nothing**: `IDiceRoller`
still uses Python at the end of this step, and the app behaves exactly as before.

## Depends on

**08** (the agreed spec) and **03** (a building solution).

## Files touched

New:

```
packages/TakeInitiative.Dice/
├── TakeInitiative.Dice.csproj    -- net10.0 classlib; Parlot + CSharpFunctionalExtensions only
├── package.json                  -- @ti/dice, so turbo builds it
├── DiceLanguage.cs               -- public Check / Roll
├── DiceError.cs                  -- structured errors + codes
├── DiceRoll.cs                   -- record DiceRoll(int Total, string Roll, string Evaluation)
├── DiceLimits.cs                 -- spec §4
├── Syntax/
│   ├── Ast.cs                    -- untyped nodes, each with Position + Length
│   └── DiceParser.cs             -- Parlot grammar
├── Typing/
│   ├── DiceType.cs               -- Pool(count, sides) | Scalar
│   ├── TypedExpression.cs
│   └── DiceTypeChecker.cs
└── Evaluation/
    ├── DiceEvaluator.cs          -- injected Random
    └── EvaluationFormatter.cs

packages/TakeInitiative.Dice.Tests/
├── TakeInitiative.Dice.Tests.csproj
├── package.json                  -- @ti/dice-tests
├── ParserTests.cs
├── TypeCheckerTests.cs
├── LimitTests.cs
└── EvaluatorTests.cs
```

Modified: `TakeInitiative.sln` — add both projects.

## Steps

1. **Projects.** Classlib + xunit test project, matching the versions already used
   in `apps/TakeInitiative.Api.Tests`. The library must not reference the API or
   ASP.NET — that is what keeps a later wasm build possible.
2. **AST.** Every node carries `Position` and `Length`. Retrofitting spans later
   is miserable. In Parlot, capture them with `.Then((ctx, start, end, value) => …)`.
3. **Parser.** Spec §2. Keywords via `Terms.Text(..., caseInsensitive: true)`.
   Left-associative binary levels via `LeftAssociative`. Detect the legacy
   `NdSkhN` suffix and report `LEGACY_KEEP_SYNTAX` rather than a generic parse
   error. Catch `ParseException` and map it to `PARSE_ERROR` — no Parlot
   exception escapes. Length is checked before parsing.
4. **Type checker.** Spec §3 exactly, plus the limits from §4. Collect all
   errors. Output a `TypedExpression` carrying resolved types so the evaluator
   never re-derives them.
5. **Evaluator.** Walk the typed tree with the injected `Random`. Pools keep
   their individual results and which were dropped. Checked arithmetic, floor
   division, `DIVIDE_BY_ZERO` at runtime.
6. **Formatter.** Spec §5, from the AST, parenthesising by precedence.

## Tests

- Every row of the spec §2 examples table parses.
- Every row of the spec §3 rejection table, asserting the **code**, not the text.
- Each limit from §4, just under and just over.
- Whitespace, casing and default-count variants (`d20`, `D20`, ` 1 d 20 `, `KH(2d20)`).
- Precedence: `2 * 1d6 + 1` ≡ `(2 * 1d6) + 1`; `10 - 2 - 3` is left-associative.
- Spans: the error for `adv(2d6)` points at `2d6`.
- With a seeded/scripted `Random`: exact totals and exact `Evaluation` strings for
  every row of the spec §5 table.

## Verify

```bash
dotnet build --configuration Release -p:TreatWarningsAsErrors=True
dotnet test packages/TakeInitiative.Dice.Tests
dotnet test   # whole solution still green; nothing else changed
```

## Notes / gotchas

- **This step changes no app behaviour.** If the app behaves differently, something
  was wired up early. That's step 10.
- Parlot's error messages are not user-facing quality. Map them to `DiceError`
  rather than surfacing them raw — that mistake is exactly what the Python
  implementation makes.
- Don't optimise. Expressions are tens of characters long.
