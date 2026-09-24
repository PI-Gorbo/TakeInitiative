# 08 — Dice language specification

## Goal

Agree the grammar, type rules and output format for the C# dice roller **before
any code is written**. This is a design document. No implementation happens in
this step.

## Depends on

Nothing. Steps 09–11 implement whatever it says.

## Files touched

This document only.

## Decisions already made

| Decision | Detail |
|---|---|
| **No backwards compatibility** | The schema is being redone. d20's postfix syntax (`2d20kh1`) is **not** supported — it gets a targeted error pointing at the function form. |
| **Own project** | The language lives in `packages/TakeInitiative.Dice` with its own test project, `packages/TakeInitiative.Dice.Tests`. It references nothing from the API and no ASP.NET, so it can be compiled to wasm later for live validation in the browser. |
| **Parlot** | Parser combinators. No maintained PEG grammar-file generator exists for .NET (Pegasus is dormant since 2023, IronMeta is dead, ANTLR isn't PEG and needs Java at build time). Parlot targets `net10.0`, is actively released, and has case-insensitive terms built in. |
| **Python goes** | Stage 2 ends with no Python in the repo. |

---

## 1. Why function-call syntax

d20's postfix `2d20kh1` is terse and requires knowing the trick. The replacement
uses **function-call syntax**: `adv(1d20)`, `kh1(2d10)`, `max(1d4, 2)`.

- Readable and teachable at the table.
- Extensible — new functions slot in without inventing new syntax.
- Composable: `adv(1d20) + 3`, `kh1(2d10) + kl1(2d6)`, `2 * (1d6 + 1)`.
- Errors can name the function and the argument that was wrong.

## 2. Grammar

```
expr     := product (('+' | '-') product)*
product  := atom (('*' | '/') atom)*
atom     := number
          | dice
          | call
          | '(' expr ')'

dice     := [integer] 'd' integer                  -- count defaults to 1

call     := ('adv' | 'dis') '(' expr ')'
          | ('kh' | 'kl') [integer] '(' expr ')'   -- keep count defaults to 1
          | ('min' | 'max') '(' expr (',' expr)+ ')'

number   := integer
```

- Whitespace is insignificant.
- Keywords (`d`, `kh`, `kl`, `adv`, `dis`, `min`, `max`) are case-insensitive.
- `*` and `/` bind tighter than `+` and `-`; all four are left-associative.
- `kh1` lexes as the keyword `kh` followed by the integer `1`, which is what makes
  the count optional (`kh(2d20)` ≡ `kh1(2d20)`).
- There is no unary minus. `10 - 1d4` is fine; `-1d4` is a parse error.

### Examples

| Expression | Meaning |
|---|---|
| `1d20` | one twenty-sided die |
| `d20` | same — count defaults to 1 |
| `1d20 + 2d4 + 3` | sum of all |
| `adv(1d20)` | roll 2d20, keep the higher |
| `adv(d20)` | same |
| `dis(1d20)` | roll 2d20, keep the lower |
| `kh1(2d10)` | roll 2d10, keep the higher |
| `kh(2d10)` | same — keep count defaults to 1 |
| `kh2(4d6)` | roll 4d6, keep the highest 2 |
| `kl1(2d20)` | roll 2d20, keep the lower |
| `kh1(kh2(3d20))` | keep highest 2 of 3, then the highest of those |
| `adv(1d20) + 5` | advantage plus a modifier |
| `10 - 1d4` | subtraction |
| `2 * 1d6 + 1` | multiplication binds tighter: `(2 * 1d6) + 1` |
| `2 * (1d6 + 1)` | parentheses group |
| `1d20 / 2` | floor division |
| `max(1d4, 2)` | at least 2 |
| `min(1d20 + 5, 20)` | capped at 20 |
| `max(1d6, 1d6, 1d6)` | min / max take two or more arguments |

## 3. Type system

`adv(2d6)` is **well-formed syntax** but semantically wrong, so it is caught by a
type-checking pass rather than by the parser.

### Types

```
Pool(count, sides)   -- a collection of dice that can still be selected from
Scalar               -- a single value: a constant, a sum, a product, a min/max
```

### Typing rules

| Form | Operand requirement | Result type |
|---|---|---|
| `<n>` | — | `Scalar` |
| `NdS` | — | `Pool(N, S)` |
| `kh<n>(e)`, `kl<n>(e)` | `e : Pool(c, s)`, `1 <= n <= c` | `Pool(n, s)` |
| `adv(e)`, `dis(e)` | `e` is the dice literal `1d20` / `d20` | `Pool(1, 20)` |
| `min(e, …)`, `max(e, …)` | two or more arguments, any type | `Scalar` |
| `a + b`, `a - b`, `a * b`, `a / b` | any | `Scalar` |
| `(e)` | — | type of `e` |

A `Pool` collapses to the sum of its kept dice whenever it is used as a `Scalar`
— in arithmetic, as a `min`/`max` argument, or as the top-level result.

`kh<n>` returning `Pool(n, s)` rather than `Scalar` is what makes `kh1(kh2(3d20))`
meaningful: keeping dice from a pool gives a smaller pool. The outer keep selects
from the inner keep's *kept* dice; anything the inner keep dropped stays dropped.

`adv` rolls the die twice and keeps the higher — it is `kh1` over two copies of
its die. Its operand must be the literal `1d20`, not an arbitrary `Pool(1, 20)`
such as `kh1(2d20)`, because "roll a keep-expression twice" has no clear meaning
at the table.

### Division

`/` is **floor** division — D&D rounds down. A literal zero divisor (`1d6 / 0`)
is a type error. A divisor that can only be known by rolling is not an error at
check time; if it rolls to zero, evaluation fails with `DIVIDE_BY_ZERO`.

Arithmetic is checked. Overflowing a 32-bit integer fails with
`RESULT_OUT_OF_RANGE` rather than wrapping.

### Rejection cases the checker must catch

| Expression | Code | Message |
|---|---|---|
| `adv(2d6)` | `ADV_REQUIRES_D20` | `adv() only works on a single d20 — try adv(1d20)` |
| `adv(1d6)` | `ADV_REQUIRES_D20` | same |
| `adv(2d20)` | `ADV_REQUIRES_D20` | same, plus: use `kh1(2d20)` to keep 1 of 2 |
| `adv(kh1(2d20))` | `ADV_REQUIRES_D20` | same |
| `kh1(3 + 4)` | `KEEP_REQUIRES_POOL` | `kh() needs dice to choose from, not a fixed number` |
| `kh1(1d6 + 1d6)` | `KEEP_REQUIRES_POOL` | same — a sum is a `Scalar` |
| `kh3(2d10)` | `KEEP_EXCEEDS_POOL` | `can't keep the highest 3 of only 2 dice` |
| `kh0(2d10)` | `KEEP_AT_LEAST_ONE` | `must keep at least 1 die` |
| `0d6` | `DICE_COUNT_ZERO` | `roll at least 1 die` |
| `1d0` | `DICE_SIDES_ZERO` | `a die needs at least 1 side` |
| `1d6 / 0` | `DIVIDE_BY_ZERO` | `can't divide by zero` |
| `2d20kh1` | `LEGACY_KEEP_SYNTAX` | `write this as kh1(2d20)` |
| `1d20 +` | `PARSE_ERROR` | `expected a number, dice or function after '+'` |

`kh3(2d10)` is the case that justifies the whole stage: syntactically perfect,
arithmetically nonsense, statically catchable.

The checker **collects all errors** rather than stopping at the first. A DM who
typed two things wrong sees both. (Parse errors are the exception: the parser
stops at the first, since everything after it is unreliable.)

## 4. Limits — these are a security requirement

Roll expressions come from **user input** and are evaluated server-side by
request validators. `99999d99999` must not be evaluable.

Enforced during type-checking, before any dice are rolled:

| Limit | Value | Code |
|---|---|---|
| Max dice in one pool | 1000 | `POOL_TOO_LARGE` |
| Max sides per die | 1000 | `TOO_MANY_SIDES` |
| Max total dice rolled by one expression | 1000 | `TOO_MANY_DICE` |
| Max numeric literal | 10000 | `NUMBER_TOO_LARGE` |
| Max expression length | 200 characters | `EXPRESSION_TOO_LONG` |
| Max nesting depth | 32 | `EXPRESSION_TOO_DEEP` |

"Total dice rolled" counts what is actually rolled: `adv(1d20)` rolls 2,
`kh1(kh2(3d20))` rolls 3. Length and depth are checked before parsing proper,
so a pathological input never reaches the combinators.

## 5. Output — the `Evaluation` string

`DiceRoll` is `record DiceRoll(int Total, string Roll, string Evaluation)`.

`Evaluation` is stored and in the frontend Zod schema but rendered nowhere, so
its format is ours to choose.

Format:
- Each **maximal pool subtree** — a `Pool`-typed expression whose parent is not
  itself a keep or `adv`/`dis` — renders as its normalised source followed by its
  dice in brackets. Kept dice come first, then dropped dice prefixed `~`, each
  group descending.
- Numbers render as themselves. Operators render with single spaces. Parentheses
  are emitted where precedence requires them, not where the user typed them.
- The whole expression ends with `= total`.

It renders from the **AST**, not the input string, so `1D20+3` normalises to
`1d20 [14] + 3 = 17`.

| Expression | Example `Evaluation` |
|---|---|
| `1d20 + 3` | `1d20 [14] + 3 = 17` |
| `2d6 + 1d4 + 2` | `2d6 [5, 3] + 1d4 [2] + 2 = 12` |
| `adv(1d20) + 5` | `adv(1d20) [18, ~7] + 5 = 23` |
| `kh2(4d6)` | `kh2(4d6) [6, 5, ~3, ~1] = 11` |
| `kh1(kh2(3d20))` | `kh1(kh2(3d20)) [18, ~12, ~3] = 18` |
| `10 - 1d4` | `10 - 1d4 [3] = 7` |
| `2 * (1d6 + 1)` | `2 * (1d6 [4] + 1) = 10` |
| `max(1d4, 2)` | `max(1d4 [1], 2) = 2` |

## 6. Errors

Errors are **structured**, not strings, so the frontend can underline the
offending span:

```csharp
record DiceError(string Code, string Message, int Position, int Length);
```

Codes are the contract (tests assert codes, never message text). Messages are
written for a DM mid-combat, not for a compiler author. Spans point at the
offending sub-expression — the error for `adv(2d6)` covers `2d6`, not the whole
input.

Compare today: `Result.Try` catches whatever CPython threw and surfaces
`"Failed to evaluate dice roll, please check your syntax. {ex.Message}"` — a raw
Python exception shown to a user.

## 7. Pipeline and public surface

```
source ──parse──▶ Ast ──typecheck──▶ TypedExpression ──evaluate──▶ DiceRoll
        Parlot           custom                        injected Random
```

Three separable pieces, each independently testable. **The evaluator takes an
injected `Random`** so tests are deterministic — a hard requirement.

```csharp
public static class DiceLanguage
{
    Result<TypedExpression, DiceError[]> Check(string expression);            // no rolling
    Result<DiceRoll, DiceError[]>        Roll(string expression, Random rng); // check + evaluate
}
```

`Check` is the valuable half: the validators that inject `IDiceRoller` today only
want to know whether an expression is valid, and currently roll a throwaway
result to find out.

The library depends on Parlot and `CSharpFunctionalExtensions` only — both pure
managed code, so the wasm option stays open.

## 8. Not in scope

- Exploding dice and rerolls — they make the number of dice rolled unbounded,
  which changes the limits model. Revisit in v2.
- Unary minus.
- Roll history, statistics, or persistence of individual die results.
- Critical-hit doubling, damage types, resistances.
- Per-user custom macros.
- Surfacing error spans in the frontend (the API returns them via the message
  only for now; underlining is a v2 frontend concern).
