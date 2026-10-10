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
| 29 | [Deploy](29-deploy.md) | `todo`; **29i built, armed** | 13–28, 30, the review | Production images built in GitHub Actions, pushed to GHCR, pulled by Coolify. The app is reachable by other people. **Runs last**; see “Order after 28”. The deploy half is now Ripple's pipeline, ported — see 29's 2026-10-07 amendment and [docs/deploy/pipeline.md](../deploy/pipeline.md) |
| 30 | [Marten 7 → 9](30-marten-9.md) | `todo` | 28 | The database settled before anybody's data is in it, on Marten 9's defaults. **Runs first** |
| 31 | Wolverine (CQRS) | `todo` | 30, the review | `[Aggregate]` handlers and a transactional outbox. Its position relative to 29 is the review's call. Step file unwritten |

### Order after 28 (2026-10-02)

The step numbers are identity, not sequence. After 28 the order is **30 → review → 29**.

1. **30 — Marten 7.31.1 → 9.x.** On FastEndpoints, with nothing else moving, green on both
   workflows before anything else starts. It goes first because **there is no production
   database yet**: Marten 9 migrates `mt_version` from `integer` to `bigint` and moves event
   sequences to `bigint`, which against empty tables is a schema definition and against real
   campaigns is a data migration. The same asymmetry covers anything the review wants to change
   about event or document shape. 29b — which makes `ApplyAllDatabaseChangesOnStartup` run
   outside Development — also gets written against Marten 9 once, instead of written twice.
2. **The review.** The full architectural and implementation pass, against Marten 9 code.
3. **29 — Deploy.** Deployment is only ever after a review. This is a standing rule.

### The deploy pipeline landed early, inert (2026-10-07)

29i — `.github/workflows/deploy.yml`, `scripts/ci/**`, `deploy-targets.json` and
`docs/deploy/pipeline.md` — is **built and committed ahead of its place in the queue**, at the
user's request, while the Ripple pipeline it ports was fresh. This does not move step 29 or weaken
"deployment is only ever after a review":

- ~~Nothing is deployed and nothing can be.~~ **Armed 2026-10-10.** It shipped with both apps
  `enabled: false` and no Coolify application to name, so a push to `main` planned, reported
  "skipped" and exited 0. The one-time setup in `pipeline.md` — the Tailscale tag, the federated
  OAuth client, the `prod` Environment — is now done and both apps are `enabled: true`, so `main`
  deploys. Step 29's remaining work is unaffected; this was always the last switch in it.
- What it buys now: the port was written against Ripple's working pipeline in one sitting rather
  than reconstructed from it months later, and the decisions live in unit-tested code
  (`pnpm deploy:test`) rather than in a plan nobody has executed.
- **29g still runs last**, after Marten 9 and the review, and it is what actually makes the app
  reachable.

**31 — Wolverine** is wanted: the `[Aggregate]` handler workflow suits an API whose 75 endpoints
already load an aggregate, append events and reload the inline projection by hand, and its
transactional outbox closes a real gap — every write today calls `SaveChangesAsync` and *then*
`hub.Notify…`, so a crash between the two commits the event and tells nobody. Whether it lands
before or after 29 is **deliberately left to the review**, which will scope it against whatever
else it finds.

### Bugs found while planning 26–29 (2026-10-01)

Neither is new work; both are defects in already-`done` steps, found by building the
thing that would have exposed them. Recorded here because an architecture review
should see them as "found and fixed", not discovered again.

| Bug | Found by | Status |
|---|---|---|
| **The Marten schema was never applied outside Development.** `ApplyAllDatabaseChangesOnStartup()` sat behind `if (IsDevelopment)` and the API container sets no `ASPNETCORE_ENVIRONMENT`. Document tables would still appear lazily, but `pg_trgm` and `unaccent` are `ExtendedSchemaObjects` that hang off no document type, so ⌘K's `word_similarity()` would have failed on the server while working on every laptop. A step 17 bug | planning 29, fixed in 26c | `fixed` |
| **Every server-rendered route 500s in a production build**, the landing page included. The SSR bundle emits `import require$$0 … from 'vue'`, a dead Rollup CJS-interop artifact, and Vue's ESM build has no default export. `/app/**` survives only because it is `ssr: false`. `pnpm dev` is unaffected, which is why it went unseen | building the web image in 29d | `in progress` |
| **Two flaky tests.** `SessionNoteTests.AnEdit_SetsEditedAt_…` compared Postgres microseconds with .NET ticks (fixed in #205, and it is what kept step 14 open). `EntryHistoryTests.AnNpcsStats_…` asserted a raw body lacked `"5d8"`, which a hex member id hits about one run in 70 | #205; the schema measurement | `fixed` |

### The admin feature is gone (2026-10-10)

The V1 admin surface — `Features/Admin`, maintenance mode and the `AdminApp` CORS keys — was
deleted. There has never been an admin frontend in this repo; step 05 noted that in passing and
kept the API half. V2 does not use any of it.

It also closed a hole that step 29 would have shipped. `Program.cs` gave every route under
`/api/admin` `AllowAnonymous(["GET", "POST", "PUT", "DELETE"])`, and `PUT /api/admin/maintenance`
wrote `MaintenanceConfig.InMaintenanceMode`, which the `NotInMaintenanceMode` policy enforced on
**every other authenticated endpoint**. Any unauthenticated caller could therefore lock the whole
app, and the only way back out was the same anonymous endpoint or the database. Nothing was
exposed, because nothing is deployed yet.

Removed with it, all verified unreferenced first:

| Thing | Why it went |
|---|---|
| `Features/Admin/**` | The endpoints, `MaintenanceConfig` and `IAdminConfig`, plus its Marten document hierarchy |
| `RequireNotInMaintenanceMode*`, `TakePolicies.NotInMaintenanceMode` | The policy had one source of truth and it is gone |
| `JWTOptions`, `JWTSigningKey`, `JWT_SIGNING_KEY` | Bound and never read since auth went cookie-based. `29-deploy.md` and `coolify.md` both said so in prose; now they do not have to |
| `CORS:AdminApp`, `MainAppAndAdminApp` | Origins for an app that does not exist |
| `ImmutableListExtensions`, `LoadAdminConfig` | No callers |
| `utils/styles.ts`, `utils/types/{FormInputBase,HelperTypes}.ts` | V1 web leftovers; no importers and nothing auto-imported them |
| `public/img/` | `redDice.png` unreferenced, `yellowDice.png` a byte-identical duplicate of `public/yellowDice.png` |

A maintenance mode is still a reasonable thing to want before 29. If it comes back it should be
an operator concern — a flag the deploy sets, or a Traefik middleware — not an anonymous write
endpoint in the application.

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
