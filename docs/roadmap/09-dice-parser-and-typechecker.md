# 09 — Dice parser and type checker

## Goal

Implement the parser and type-checking passes from the spec as a self-contained,
unit-tested library. **Wired to nothing** — `IDiceRoller` still uses Python at the
end of this step, and the app behaves exactly as before.

## Depends on

**08** (the agreed spec) and **03** (a building project).

## Files touched

New, under `apps/TakeInitiative.Api/src/Utilities/DiceRoller/Language/`:

```
Language/
├── Ast.cs              -- untyped AST nodes
├── DiceParser.cs       -- Pidgin grammar
├── TypedAst.cs         -- typed AST + DiceType
├── DiceTypeChecker.cs  -- the checking pass
└── DiceError.cs        -- structured errors
```

Plus tests under `apps/TakeInitiative.Api.Tests/Scopes/Unit/Dice/`.

Modified:
- `apps/TakeInitiative.Api/TakeInitiative.Api.csproj` — add `Pidgin`

## Steps

### 1. Add Pidgin

Parser **combinators**, not a PEG generator — which is why searching for a PEG
library came up empty. The grammar is small enough that combinators are the right
tool: no build step, no generated code, and the grammar reads as ordinary C#.

Add the `Pidgin` package reference. Check the current version at implementation
time rather than trusting a number written here.

### 2. AST

Model the untyped tree from the spec's grammar:

```csharp
abstract record DiceNode(int Position, int Length);

record NumberNode(int Value, ...)                             : DiceNode;
record PoolNode(int Count, int Sides, ...)                    : DiceNode;
record KeepNode(KeepKind Kind, int N, DiceNode Operand, ...)  : DiceNode;  // kh / kl
record AdvantageNode(bool IsAdvantage, DiceNode Operand, ...) : DiceNode;  // adv / dis
record BinaryNode(BinaryOp Op, DiceNode Left, DiceNode Right, ...) : DiceNode;
```

**Every node carries `Position` and `Length`.** The structured errors in the spec
need them to underline the offending span, and retrofitting source positions
through a parser afterwards is miserable. Pidgin exposes position via
`Parser.CurrentPos` / `MapWithInput`.

### 3. Parser

Implement the grammar from spec §3, including the legacy postfix form
(`2d20kh1`) desugaring to the same `KeepNode` the function form produces. Both
surface syntaxes must yield an identical AST — that is what makes supporting both
nearly free.

Watch for:
- `kh1` lexes as keyword `kh` + integer `1`, not one identifier, so the count can
  be optional (`kh(2d20)` ≡ `kh1(2d20)`).
- `d20` with no count must parse (count defaults to 1).
- Case-insensitivity for `d`, `kh`, `kl`, `adv`, `dis`.
- Whitespace insignificant everywhere.
- Left-associative `+`/`-`. Pidgin's `Chainl1` handles this.

Parse failures become a `PARSE_ERROR` `DiceError` with the position Pidgin
reports — do not let a Pidgin exception escape.

### 4. Type checker

Implement the rules from spec §4 exactly:

- `NdS` → `Pool(N, S)`
- `kh<n>` / `kl<n>` require `Pool(c, s)` with `1 <= n <= c`, produce `Pool(n, s)`
- `adv` / `dis` require `Pool(1, 20)` exactly, produce `Pool(1, 20)`
- arithmetic produces `Scalar`; pools collapse to their sum

Then enforce the limits from spec §5 **in this pass, before anything is rolled** —
they are a security requirement, not a nicety. User-supplied expressions reach
this code through request validators.

Collect **all** errors rather than stopping at the first. A DM who typed two
things wrong should see both.

Output a `TypedAst` carrying resolved types, so the evaluator in step 10 never
re-derives them.

### 5. Tests

The grammar is small and total, so aim for genuinely thorough coverage:

- Every row of the examples table in spec §3
- Every row of the rejection table in spec §4, asserting the **error code**, not
  the message text (messages will get reworded; codes are the contract)
- Legacy/function equivalence: `2d20kh1` and `kh1(2d20)` produce identical ASTs
- Each limit from §5, just under and just over the boundary
- Whitespace, casing, and default-count variants (`d20`, `D20`, ` 1 d 20 `)
- Positions: assert that the error span for `adv(2d6)` points at `2d6`, not the
  whole expression

## Verify

```bash
dotnet build --configuration Release -p:TreatWarningsAsErrors=True
dotnet test --filter FullyQualifiedName~Dice
```

The whole suite must still pass — nothing is wired up yet, so nothing else should
change. Confirm the app still runs on the Python roller exactly as before.

## Notes / gotchas

- **This step changes no behaviour.** If the app behaves differently at the end,
  something was wired up that shouldn't have been. That's step 10.
- Keep `Check` and `Roll` off `IDiceRoller` for now. Step 10 extends the
  interface; this step only builds the library behind it.
- Pidgin's error messages are decent but not user-facing quality. Map them to the
  spec's `DiceError` shape rather than surfacing them raw — that mistake is
  exactly what the current Python implementation makes.
- Don't optimise. These expressions are tens of characters long and evaluated a
  handful of times per request.
