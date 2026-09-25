# TakeInitiative Revival Roadmap

Open this file at the start of every session. It is the single source of truth for
what is done, what is next, and what blocks what.

## The rule

**Every step ends somewhere runnable.** No step leaves `dev` in a broken state.
If a step turns out to be bigger than one session, split it — don't leave it half-applied.

## Why this exists

The repo went dormant after `1.0.14` and is currently **unbuildable** in four
independent ways:

| Blocker | Detail |
|---|---|
| Private NuGet feed | `GP.MartenIdentity 1.1.1` lives only on `nuget.pkg.github.com/PI-Gorbo`. No `nuget.config` (gitignored), no local PAT. `obj/project.assets.json` records `"Unable to find package GP.MartenIdentity"`. |
| Wrong .NET runtime | Projects target `net8.0`; only SDK `10.0.400` / runtime `10.0.11` installed. No `global.json`. |
| Python missing | `pythonnet` embeds CPython in-process. `appsettings.json` points `PythonDLL` at `C:/Users/sam.gorbatov/...python312.dll` — a Windows path from another machine. |
| Bun missing | Root + web use Bun. `bun` is not on PATH; no `node_modules` anywhere. |

## Stages

| Stage | Goal | Branch |
|---|---|---|
| **1 — Get running** | `pnpm dev` works from a clean clone with no GitHub PAT | `dev` |
| **2 — Replace dice roller** | Python gone from the running app | `dev` |
| **3 — Full system** | v2, gated behind a joint design session | new branch |

Ordering is deliberate: the dice roller comes **before** the v2 rewrite so the
current app sheds Python, and v2 never inherits it. The Python-in-Docker problem
gets solved once instead of twice.

---

## Stage 1 — Get running

| # | Step | Status | Depends on | Goal |
|---|---|---|---|---|
| 01 | [pnpm + Turborepo](01-pnpm-turborepo.md) | `done` | — | Bun and the Makefile gone; pnpm workspace + turbo drive everything |
| 02 | [Drop GorboPackages](02-drop-gorbopackages.md) | `done` | — | `dotnet restore` succeeds with only nuget.org |
| 03 | [Retarget net10.0](03-retarget-net10.md) | `done` | 02 | `dotnet build` and `dotnet run` work on the installed runtime |
| 04 | [Install Python](04-install-python.md) | `done` | 01, 03 | The embedded interpreter resolves its stdlib and `d20` |
| 05 | [Fix dev infrastructure](05-fix-dev-infra.md) | `done` | 01 | compose, ports, env vars and README all agree |
| 06 | [release-please](06-release-please.md) | `done` | 01 | Releases automated; `Scripts/Release.ts` deleted |
| 07 | [Known bug fixes](07-known-bug-fixes.md) | `done` | 03 | Campaign membership actually verified; enum drift resolved |

**Stage 1 exit criteria** — from a clean clone, with no GitHub PAT:
`pnpm install && pnpm dev` → sign up → create a campaign → start a combat →
roll initiative → end a turn → SignalR update lands in the browser.

> **CHECKPOINT.** Stop here and report. This is the first moment the app can
> actually be seen and used.

---

## Stage 2 — Replace the dice roller

| # | Step | Status | Depends on | Goal |
|---|---|---|---|---|
| 08 | [Dice language spec](08-dice-language-spec.md) | `done` | — | Agreed grammar + type rules. **Design doc, no code.** |
| 09 | [Dice library](09-dice-parser-and-typechecker.md) | `done` | 08, 03 | `packages/TakeInitiative.Dice` + its own tests: parse, check, evaluate. Wired to nothing |
| 10 | [Swap](10-dice-evaluator-and-swap.md) | `done` | 09 | App and validators running on the new roller; Python installed but unused |
| 11 | [Remove Python](11-remove-python.md) | `done` | 10 | Zero Python in the repo |

> **CHECKPOINT.** Stop and report at the end of **10**, before 11 deletes Python.
> That is the last point where falling back is free.

**Stage 2 exit criteria** — no Python anywhere in the repo; combat and initiative
rolling behave as before; invalid expressions produce useful inline errors instead
of a raw CPython exception message.

There is **no backwards-compatibility obligation** for stored roll expressions —
the schema is being redone — so d20's postfix syntax (`2d20kh1`) is dropped.
Steps 08–11 ship as one stack of PRs (`gh stack`), one PR per step.

---

## Stage 3 — Full system (GATED)

| # | Step | Status | Depends on | Goal |
|---|---|---|---|---|
| 12 | [v2 design](12-v2-design-session.md) | `done` | Stage 2 | Glossary, UX, combat simplification, architecture and invariants agreed |
| 13 | [v2 skeleton](13-v2-skeleton.md) | `in progress` | 12 | New branch. Campaign, members and roles, auth, OpenAPI type generation, PWA shell with three tabs |
| 14 | [Sessions + session notes](14-sessions-and-notes.md) | `in progress` | 13 | Composer, markdown, visibility, filters, back-posting, gap prompt, live over SignalR |
| 15 | [Wiki + mentions](15-wiki-and-mentions.md) | `done` | 14 | `@` composer, entries, articles, timeline, promote, secret blocks, aliases, merge, edit access |
| 16 | [Images](16-images.md) | `in progress` | 14, 15 | S3 blob store, image notes, captions, galleries, share target |
| 17 | ⌘K search | `todo` | 15 | FTS plus trigram, visibility-aware, actions |
| 18 | Combat v2 | `todo` | 15 | Simplified model, combatants from entries, per-combatant PlayersSee, combat card |
| 19 | Connections + loose ends | `todo` | 15, 18 | Evidence panel, graph page, loose ends in the wiki and on sessions |

**MVP line.** Everything below is post-MVP (design §11 and §11a).

| # | Step | Status | Depends on | Goal |
|---|---|---|---|---|
| 20 | SRD reference | `todo` | 17 | Bundled SRD 5.2 provider, stat-block card, + wiki with Stats |
| 21 | 5eTools index | `todo` | 20 | Preprocessing script, search-only provider, deep links; delete the Bestiary branches |
| 22 | D&D Beyond link | `todo` | 15 | Sheet URL on player characters, manual refresh of core stats |
| 23 | In-browser suggestions | `todo` | 15, 17 | Zero-shot extraction (GLiNER vs Laya) in the browser; suggestions, never facts |
| 24 | Discord import | `todo` | 16, 23 | Import a Discord export into sessions, with suggested mentions to review |

Step 12 is the design every later step must respect, and its
[invariants](12-v2-design-session.md#10-invariants) are binding. Step files 13+
are written **one at a time**, just before each one starts, so they reflect what
the previous step actually taught us.

---

## Conventions

Every step file has six sections:

- **Goal** — one sentence, and what "running" looks like at the end
- **Depends on** — which steps must land first
- **Files touched** — concrete paths
- **Steps** — ordered and executable
- **Verify** — how to prove it worked, ending at a runnable app
- **Notes / gotchas** — findings already made, so nothing gets re-derived

## Reference environment

Verified on this machine at time of writing:

```
node    v24.15.0        dotnet SDK      10.0.400
pnpm    10.33.0         dotnet runtime  10.0.11  (no net8 runtime)
npm     11.12.1         python3         3.9.6    (Xcode CLT, no usable dylib)
uv      0.11.5          bun             NOT INSTALLED
docker  29.4.0          compose         v5.1.1
arch    arm64 (Apple Silicon)
```

Ripple (`/Users/sam/projects/Ripple`) is the convention reference for pnpm,
Turborepo, release-please, husky/commitlint and PWA config.
GorboPackages (`/Users/sam/projects/GorboPackages`) is the source for step 02.
