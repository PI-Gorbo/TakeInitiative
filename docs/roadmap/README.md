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
| 13 | [v2 skeleton](13-v2-skeleton.md) | `built`; Verify pending | 12 | New branch. Campaign, members and roles, auth, OpenAPI type generation, PWA shell with three tabs |
| 14 | [Sessions + session notes](14-sessions-and-notes.md) | `built`; Verify pending | 13 | Composer, markdown, visibility, filters, back-posting, gap prompt, live over SignalR |
| 15 | [Wiki + mentions](15-wiki-and-mentions.md) | `done` | 14 | `@` composer, entries, articles, timeline, promote, secret blocks, aliases, merge, edit access |
| 16 | [Images](16-images.md) | `done` | 14, 15 | S3 blob store, image notes, captions, galleries, share target |
| 17 | [⌘K search](17-search.md) | `done` | 15 | FTS plus trigram, visibility-aware, actions |
| 18 | [Combat v2](18-combat.md) | `done` | 15 | Simplified model, combatants from entries, per-combatant PlayersSee, combat card |
| 19 | [Connections + loose ends](19-connections.md) | `done` | 15, 18 | Evidence panel, graph page, loose ends in the wiki and on sessions |
| 20 | [SRD reference](20-srd-reference.md) | `done` | 17 | Bundled SRD 5.2 provider, stat-block card, + wiki with Stats |
| 21 | [5eTools index](21-5etools-index.md) | `superseded` by 26 | 20 | Preprocessing script, search-only provider, deep links. 21a–21c shipped; **21d (deleting the four Bestiary branches) is still the user's to run**, and step 26 replaces the in-memory index with a database-backed one |
| 23 | [In-browser suggestions](23-suggestions.md) | `done` (23a–23f) | 15, 17 | Zero-shot extraction (GLiNER small v2.5, picked by 23a's spike) in the browser; suggestions, never facts. 23f: "✨ Find suggestions" in a note's own menu, with a deeper pass on each "Look again" |
| 25 | [Wiki redesign](25-wiki-redesign.md) | `done` | 15, 19, 20 | Mobile-first entry page (Summary \| Notes tabs, collapsed sections), a desktop side panel, "Played by" for claims, no "Add a note" on the wiki |
| 26 | [Knowledge base](26-knowledge-base.md) | `done` | 20, 21 | A .NET ingest CLI, the slim 5eTools index in Postgres with a `tsvector`, and a **browsable Knowledge base surface** so reference rows have somewhere to live |
| 27 | [Links](27-links.md) | `done` | 26 | The `EntryLink` seam becomes real: `knowledgebase` and `external` links on an entry, with events, history and visibility |
| 28 | [Knowledge-base match suggestions](28-kb-suggestions.md) | `done` | 26, 27, 23 | On an entry that looks like a knowledge-base item, offer the link and let the user confirm it |
| 29 | [Deploy](29-deploy.md) | `todo` | 13–28 | Production images built in GitHub Actions, pushed to GHCR, pulled by Coolify. The app is reachable by other people |

### Bugs found while planning 26–29 (2026-10-01)

Neither is new work; both are defects in already-`done` steps, found by building the
thing that would have exposed them. Recorded here because an architecture review
should see them as "found and fixed", not discovered again.

| Bug | Found by | Status |
|---|---|---|
| **The Marten schema was never applied outside Development.** `ApplyAllDatabaseChangesOnStartup()` sat behind `if (IsDevelopment)` and the API container sets no `ASPNETCORE_ENVIRONMENT`. Document tables would still appear lazily, but `pg_trgm` and `unaccent` are `ExtendedSchemaObjects` that hang off no document type, so ⌘K's `word_similarity()` would have failed on the server while working on every laptop. A step 17 bug | planning 29, fixed in 26c | `fixed` |
| **Every server-rendered route 500s in a production build**, the landing page included. The SSR bundle emits `import require$$0 … from 'vue'`, a dead Rollup CJS-interop artifact, and Vue's ESM build has no default export. `/app/**` survives only because it is `ssr: false`. `pnpm dev` is unaffected, which is why it went unseen | building the web image in 29d | `in progress` |
| **Two flaky tests.** `SessionNoteTests.AnEdit_SetsEditedAt_…` compared Postgres microseconds with .NET ticks (fixed in #205, and it is what kept step 14 open). `EntryHistoryTests.AnNpcsStats_…` asserted a raw body lacked `"5d8"`, which a hex member id hits about one run in 70 | #205; the schema measurement | `fixed` |

**MVP line.** Everything below is post-MVP (design §11 and §11a).

| # | Step | Status | Depends on | Goal |
|---|---|---|---|---|
| 22 | [D&D Beyond link](22-ddb-link.md) | `deferred`; link half folded into 27 | 27 | The sheet URL is just an `external` link, so 27 delivers it. What stays here is the **unofficial fetch** behind its off-by-default flag, which needs nothing for MVP |
| 24 | Discord import | `todo` | 16, 23 | Import a Discord export into sessions, with suggested mentions to review. **Out of MVP (the user's call, 2026-09-30): "a discord integration will be overkill for MVP"** |

Step 12 is the design every later step must respect, and its
[invariants](12-v2-design-session.md#10-invariants) are binding. Step files 13+
are written **one at a time**, just before each one starts, so they reflect what
the previous step actually taught us.

### Where the MVP line moved, and why (2026-09-30)

It used to sit after 19, which made steps 20–25 post-MVP work that had already
been built. The user's MVP is "something I can show my mates", and that needs
three things the old line excluded:

1. **A knowledge base that is a place, not just search results.** Steps 20 and 21
   put reference rows in ⌘K and nowhere else, so a link on an entry pointed into
   something the app never showed you. ⌘K is for quick access; it is not the
   gateway to a body of content. Step 26 gives the knowledge base a surface, and
   moving the index into Postgres follows from that.
2. **Links that a user can make by hand.** `Entry.Links` has been a declared seam
   since step 15 and has never been written. Step 27 makes it real, for
   knowledge-base items and arbitrary external URLs alike.
3. **A deployment.** Nothing in steps 01–25 gets the app onto a machine anybody
   else can reach. Step 29 does.

Steps 22 and 24 went the other way: neither is needed to demonstrate the app.

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
