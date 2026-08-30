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
| 01 | [pnpm + Turborepo](01-pnpm-turborepo.md) | `todo` | — | Bun and the Makefile gone; pnpm workspace + turbo drive everything |
| 02 | [Drop GorboPackages](02-drop-gorbopackages.md) | `done` | — | `dotnet restore` succeeds with only nuget.org |
| 03 | [Retarget net10.0](03-retarget-net10.md) | `todo` | 02 | `dotnet build` and `dotnet run` work on the installed runtime |
| 04 | [Install Python](04-install-python.md) | `todo` | 01, 03 | The embedded interpreter resolves its stdlib and `d20` |
| 05 | [Fix dev infrastructure](05-fix-dev-infra.md) | `todo` | 01 | compose, ports, env vars and README all agree |
| 06 | [release-please](06-release-please.md) | `todo` | 01 | Releases automated; `Scripts/Release.ts` deleted |
| 07 | [Known bug fixes](07-known-bug-fixes.md) | `todo` | 03 | Campaign membership actually verified; enum drift resolved |

**Stage 1 exit criteria** — from a clean clone, with no GitHub PAT:
`pnpm install && pnpm dev` → sign up → create a campaign → start a combat →
roll initiative → end a turn → SignalR update lands in the browser.

> **CHECKPOINT.** Stop here and report. This is the first moment the app can
> actually be seen and used.

---

## Stage 2 — Replace the dice roller

| # | Step | Status | Depends on | Goal |
|---|---|---|---|---|
| 08 | [Dice language spec](08-dice-language-spec.md) | `todo` | — | Agreed grammar + type rules. **Design doc, no code.** |
| 09 | [Parser + type checker](09-dice-parser-and-typechecker.md) | `todo` | 08, 03 | Pure library, unit tested, wired to nothing |
| 10 | [Evaluator + swap](10-dice-evaluator-and-swap.md) | `todo` | 09 | App running on the new roller; Python installed but unused |
| 11 | [Remove Python](11-remove-python.md) | `todo` | 10 | Zero Python in the repo |

> **CHECKPOINT.** Stop and report at the end of **10**, before 11 deletes Python.
> That is the last point where falling back is free.

**Stage 2 exit criteria** — no Python anywhere in the repo; combat and initiative
rolling behave as before; invalid expressions produce useful inline errors instead
of a raw CPython exception message.

---

## Stage 3 — Full system (GATED)

| # | Step | Status | Depends on | Goal |
|---|---|---|---|---|
| 12 | [v2 design session](12-v2-design-session.md) | `todo` | Stage 2 | Agree design invariants **together** before any step files are written |

**Stage 3 is deliberately not broken into steps.** Pre-writing it as executable
work is exactly how this scope-creeps into something nobody wanted. Step 12 is an
*agenda*, not a plan: it carries the design work already done, framed as proposals
to argue with, and ends with an open-questions list and an empty Invariants
section to fill in together.

Only once those invariants are agreed do steps 13+ get written.

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
