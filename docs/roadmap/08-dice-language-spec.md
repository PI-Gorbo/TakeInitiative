# 08 — Dice language specification

## Goal

Agree the grammar, type rules and output format for the C# dice roller **before
any code is written**. This is a design document. No implementation happens in
this step.

## Depends on

Nothing. Read and argue with it; steps 09–11 implement whatever it says.

## Files touched

This document only.

---

## 1. Why replace d20's syntax

Today's roll expressions use the Python `d20` library's postfix syntax:
`2d20kh1` for advantage, `2d20kl1` for disadvantage. That is terse and requires
knowing the trick — `2d20kh1` does not read as "advantage" to anyone who hasn't
been told.

The replacement uses **function-call syntax**: `adv(1d20)`, `kh1(2d10)`.

- Readable and teachable at the table.
- Trivially extensible — `min`, `max`, `reroll`, `explode` all slot in later
  without inventing new syntax.
- Composable with no precedence rules for modifiers: `adv(1d20) + 3`,
  `kh1(2d10) + kl1(2d6)`.
- Errors can name the function and the argument that was wrong.

## 2. Backwards compatibility — support both

**The parser accepts the legacy postfix form as well.** `2d20kh1` and `kh1(2d20)`
both parse, and mean the same thing.

This is not optional politeness. Existing databases hold d20-syntax strings in
`PlayerCharacter` initiative and health fields, and in `UnevaluatedCharacterHealth`
/ `UnevaluatedCharacterInitiative` on stored combats. Supporting both makes the
migration a non-event, and it is a small addition to the grammar — a postfix
suffix that desugars to the same AST node.

Function form is **canonical**: it is what documentation, placeholder text and
tooltips show, and what any future formatter would emit.

## 3. Grammar

```
expr     := term (('+' | '-') term)*
term     := number
          | pool
          | call
          | '(' expr ')'

pool     := [count] 'd' sides [ postfix ]        -- count defaults to 1
postfix  := ('kh' | 'kl') [number]               -- legacy form, number defaults to 1

call     := 'adv' '(' expr ')'
          | 'dis' '(' expr ')'
          | ('kh' | 'kl') [number] '(' expr ')'  -- number defaults to 1

count    := integer
sides    := integer
number   := integer
```

Whitespace is insignificant. Case-insensitive for `d`, `kh`, `kl`, `adv`, `dis`.

Note `kh1` lexes as the keyword `kh` followed by the integer `1` — not as a
single identifier. That is what makes the count optional (`kh(2d20)` ≡ `kh1(2d20)`).

### Examples

| Expression | Meaning |
|---|---|
| `1d20` | one twenty-sided die |
| `d20` | same — count defaults to 1 |
| `1d20 + 2d4 + 3` | sum of all |
| `adv(1d20)` | roll 2d20, keep the higher |
| `dis(1d20)` | roll 2d20, keep the lower |
| `kh1(2d10)` | roll 2d10, keep the higher |
| `kh2(4d6)` | roll 4d6, keep the highest 2 |
| `kl1(2d20)` | roll 2d20, keep the lower |
| `2d20kh1` | legacy form of `kh1(2d20)` |
| `adv(1d20) + 5` | advantage plus a modifier |
| `kh1(kh2(3d20))` | keep highest 2 of 3, then the highest of those |
| `10 - 1d4` | subtraction is allowed |

## 4. Type system

This is the part that earns its keep. `adv(2d6)` is **well-formed syntax** — it
is semantically wrong, so it must be caught by a type-checking pass rather than
by the parser.

### Types

```
Pool(count, sides)   -- a collection of dice that can still be selected from
Scalar               -- a single value: a constant, or a sum
```

### Typing rules

| Form | Operand requirement | Result type |
|---|---|---|
| `<n>` | — | `Scalar` |
| `NdS` | — | `Pool(N, S)` |
| `kh<n>(e)` | `e : Pool(c, s)` and `n <= c` and `n >= 1` | `Pool(n, s)` |
| `kl<n>(e)` | `e : Pool(c, s)` and `n <= c` and `n >= 1` | `Pool(n, s)` |
| `adv(e)` | `e : Pool(1, 20)` exactly | `Pool(1, 20)` |
| `dis(e)` | `e : Pool(1, 20)` exactly | `Pool(1, 20)` |
| `a + b`, `a - b` | any | `Scalar` |

A `Pool` collapses to the sum of its kept dice whenever it is used as a `Scalar`
— in arithmetic, or as the top-level result.

`kh<n>` returning `Pool(n, s)` rather than `Scalar` is what makes `kh1(kh2(3d20))`
meaningful. It is also the natural reading: keeping dice from a pool gives a
smaller pool.

`adv` desugars to `kh1(Pool(2, 20))` — it takes the *die* and doubles it.
That is why the operand must be `Pool(1, 20)` and not `Pool(2, 20)`.

### Rejection cases the checker must catch

| Expression | Error |
|---|---|
| `adv(2d6)` | `adv` requires a d20; got d6 |
| `adv(1d6)` | same |
| `adv(2d20)` | `adv` requires exactly one d20; use `kh1(2d20)` to keep 1 of 2 |
| `kh1(3 + 4)` | `kh` requires dice, not a fixed number |
| `kh3(2d10)` | cannot keep 3 highest of 2 dice |
| `kh0(2d10)` | must keep at least 1 die |

`kh3(2d10)` is the case that best justifies the whole stage: syntactically
perfect, arithmetically nonsense, statically catchable.

## 5. Limits — these are a security requirement

Roll expressions come from **user input** and are evaluated server-side, today by
roughly a dozen FluentValidation validators on request payloads. `99999d99999`
must not be evaluable.

Enforce during type-checking, before any dice are rolled:

| Limit | Suggested value |
|---|---|
| Max dice per pool (`count`) | 1000 |
| Max sides per die | 1000 |
| Max total dice in one expression | 1000 |
| Max expression length | 200 characters |
| Max nesting depth | 32 |

Values are a starting point — argue with them. They only need to be comfortably
above anything a real table would type and comfortably below anything that costs
real CPU or memory.

## 6. Output — the `Evaluation` string

`DiceRoll` is `record DiceRoll(int Total, string Roll, string Evaluation)`.

`Evaluation` is currently captured, stored, included in the frontend Zod
schema — **and rendered nowhere**. So its format is entirely ours to choose, and
surfacing it is a easy win: showing the DM what was actually rolled is genuinely
useful at the table.

Format: each pool renders as its source text followed by the individual dice in
brackets, dropped dice prefixed with `~`. The whole expression ends with `= total`.

| Expression | Example `Evaluation` |
|---|---|
| `1d20 + 3` | `1d20 [14] + 3 = 17` |
| `2d6 + 1d4 + 2` | `2d6 [3, 5] + 1d4 [2] + 2 = 12` |
| `adv(1d20) + 5` | `adv(1d20) [18, ~7] + 5 = 23` |
| `kh2(4d6)` | `kh2(4d6) [6, 5, ~3, ~1] = 11` |
| `10 - 1d4` | `10 - 1d4 [3] = 7` |

Kept dice are listed before dropped dice within a pool, each group in descending
order. `~` for dropped matches d20's convention, so it stays familiar.

## 7. Errors

The type checker returns **structured** errors, not strings, so the frontend can
underline the offending span:

```csharp
record DiceError(string Code, string Message, int Position, int Length);
```

Codes are stable identifiers (`ADV_REQUIRES_D20`, `KEEP_EXCEEDS_POOL`,
`KEEP_REQUIRES_POOL`, `POOL_TOO_LARGE`, `PARSE_ERROR`, ...). Messages are written
for a DM mid-combat, not for a compiler author:

> `adv() only works on d20s — try adv(1d20)`
> `can't keep the highest 3 of only 2 dice`

Compare today's behaviour: `Result.Try` catches whatever CPython threw and
surfaces `"Failed to evaluate dice roll, please check your syntax. {ex.Message}"`,
i.e. a raw Python exception shown to a user.

## 8. Pipeline

```
source ──parse──▶ Ast ──typecheck──▶ TypedAst ──evaluate──▶ DiceRoll
        Pidgin           custom                  injected RNG
```

Three separable pieces, each independently testable. **The evaluator takes an
injected `Random`** so tests are deterministic — this is a hard requirement, not
a nicety.

Public surface:

```csharp
Result<TypedAst, DiceError[]> Check(string expression);   // validation, no rolling
Result<DiceRoll>              Roll(string expression);    // check + evaluate
```

`Check` is the valuable half. The ~12 validators that inject `IDiceRoller` today
only want to know whether an expression is valid — they currently evaluate a
throwaway roll to find out. They should call `Check`.

## 9. Open questions

1. **Are the limits in §5 right?** Especially max sides — is `1d1000` a real
   thing anyone rolls?
2. **Should `adv(1d20)` accept a bare `adv(d20)`?** The grammar allows it since
   count defaults to 1. Probably yes, no reason to forbid it.
3. **Is `kh1(kh2(3d20))` worth supporting**, or should nesting be rejected as
   confusing? It falls out of the type system for free, but nobody will type it.
4. **Should the legacy postfix form be deprecated visibly** — a warning in the UI
   nudging toward function form — or silently supported forever?
5. **Any other functions worth including now?** `min`/`max`, exploding dice,
   rerolls. Adding later is cheap; the point of asking now is whether any of them
   would change the type system.

## 10. Not in scope

- Roll history, statistics, or persistence of individual die results
- Critical-hit doubling, damage types, resistances
- Per-user custom macros
- Anything requiring the parser to know about D&D rules beyond dice
