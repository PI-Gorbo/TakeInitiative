# 05 — Fix the rotted dev infrastructure

## Goal

Make compose, ports, env vars and the README agree with each other and with
reality. At the end, `docker compose up -d --build` builds every service, and the
README's setup instructions actually work on a fresh machine.

## Depends on

**01** (compose is driven from the pnpm scripts). Best done after **04** so the
README can document the real, working flow.

## Files touched

- `compose.dev.yml`
- `apps/TakeInitiative.Api/appsettings.json`
- `apps/TakeInitiative.Api/Properties/launchSettings.json`
- `apps/TakeInitiative.Api/src/boostrap/Bootstrap.cs`
- `apps/TakeInitiative.Web/nuxt.config.ts`
- `scripts/setup-env.mjs` (create, or fold into `setup-python.mjs`)
- `README.md`
- `apps/TakeInitiative.Api/dockerignore` (delete — stray empty file beside the
  real `.dockerignore`)

## Steps

### 1. compose.dev.yml

Four problems:

- **`version: "3.9"`** is obsolete and warns on every invocation. Delete the key.
- **The `admin` service cannot build.** It points at
  `./apps/TakeInitiative.Web/dockerfile.Admin`, which was never committed. There
  is no admin frontend anywhere in the repo — only `/api/admin/*` endpoints and an
  `AdminAppCors` policy. **Delete the service.** (Keep the API-side admin
  endpoints; they're used by the maintenance-mode kill switch.)
- **The `web` service cannot build** either — it points at `dockerfile.Main`,
  which also doesn't exist. The real file is `apps/TakeInitiative.Web/Dockerfile`.
  Fix the path.
- **Env var names don't match what the app reads.** Compose sets
  `NUXT_PUBLIC_AXIOS_BASE_URL` and `NUXT_PUBLIC_WEB_URL`, but `nuxt.config.ts`
  reads `process.env.API_URL` and `process.env.WEB_URL` at build time. Pick one.
  Recommended: rename in compose to `API_URL` / `WEB_URL` to match `TEMPLATE.env`
  and the Nuxt config, since that's two places agreeing against one.

Also re-enable the commented-out API healthcheck, fixing its typo
(`/heathz` → `/healthz`).

### 2. Unify the ports

Four schemes currently disagree:

| Source | Ports |
|---|---|
| `launchSettings.json` | API 5010 / 7010 (https) |
| `appsettings.json` CORS | web 3000, admin 3001 |
| `compose.dev.yml` | db 7401, api 7402, web 7403, admin 7404 |
| old makefile isolated db | 5432 |

Pick one scheme and apply it everywhere. Suggested, keeping the familiar native
dev ports and leaving compose in its own range:

- **Native dev** (what `pnpm dev` uses): db `5432`, api `5010`, web `3000`
- **Compose** (full-stack): db `7401`, api `7402`, web `7403`

Then make `compose.dev.yml`'s postgres publish `5432:5432` for the `pnpm dev`
path, since that is the only compose service `setup_environment` starts and the
API's default connection string in `appsettings.json` already expects 5432.
Update CORS entries to match whichever web port is real.

### 3. Uncomment `ApplyAllDatabaseChangesOnStartup()`

In `Bootstrap.AddMartenDB`. Schema creation currently relies on Marten's implicit
auto-create-on-first-use, which is fragile and makes a fresh database's behaviour
depend on which endpoint happens to be hit first.

Note this makes startup fail loudly on a schema conflict rather than failing
subtly later. That is the desired behaviour, but it means a stale local database
may now block startup — document `docker compose down -v` as the reset.

### 4. Generate `.env`

`apps/TakeInitiative.Web/TEMPLATE.env` holds:
```
API_URL=http://localhost:5010
WEB_URL=https://localhost:3000
```

Copy it to `.env` during `setup_environment` if absent. Do not overwrite an
existing `.env`.

Ripple solves this with a homegrown `envman` CLI
(`/Users/sam/projects/Ripple/devTools/envMan`) that reads a declarative schema and
prompts for missing values. That is overkill for two variables — a few lines in
the setup script is enough. Revisit only if the variable count grows.

### 5. README

Rewrite `## Requirements for local development`, `## QuickStart` and
`## Working on individual projects`:

- Requirements become: Docker, Node (via Volta), pnpm, .NET 10 SDK. **No make, no
  bun, no manual Python step** — `pnpm dev` handles Python itself.
- QuickStart becomes `pnpm install && pnpm dev`.
- Delete the `## Nuget` section entirely (step 02 already made it a lie).
- Delete the manual `PythonDLL` instructions — step 04 automates it, and step 11
  deletes the concept.
- Fix the stray `s` on line 3.

Note the README currently tells people to run `make api` and
`make docker.dev.compose`, where `api` only exists in the deleted sub-makefile.

## Verify

```bash
docker compose -p takeinitiative -f compose.dev.yml config     # parses, no version warning
docker compose -p takeinitiative -f compose.dev.yml up -d --build
docker compose -p takeinitiative -f compose.dev.yml ps          # postgres, api, web all healthy
curl -f http://localhost:7402/healthz
```

Then the native path, from a clean clone, following only the README:
```bash
pnpm install && pnpm dev
```
Sign up, create a campaign, start a combat, roll initiative, end a turn. Confirm
the SignalR `combatUpdated` broadcast reaches the browser (devtools → WS frames).

## Notes / gotchas

- The API compose service was unbuildable before step 02 regardless (no
  `personal_github_token` build arg was passed), so `make docker.dev.compose` has
  been broken for a long time. Expect surprises on first successful build.
- `compose.dev.yml` bind-mounts `/dockerVolumes/take-initiative/postgresql`, an
  absolute host path that won't exist on a fresh machine. Consider switching to a
  named volume, which is portable and lets `down -v` reset cleanly.
- `nuxt.config.ts` only enables `typescript.strict` and `typeCheck` when
  `TAKE_INIT_ENVIRONMENT === 'DEVELOPMENT'`, but the web Dockerfile sets it to
  `'PRODUCTION'` — so production builds skip type checking. CI compensates with a
  separate `nuxi typecheck`. Worth noting; not worth fixing here.
- The web Dockerfile is Bun-based (`oven/bun:1`, `bun install`, `COPY bun.lockb`).
  Step 01 deletes the bun lockfiles, so **this Dockerfile must be migrated to
  pnpm** or the web image stops building. Ripple's four-stage pattern
  (`base` → `prepare` with `turbo prune` → `builder` → `runner`) is the reference,
  but a plain `corepack enable && pnpm install --frozen-lockfile` is sufficient
  here.
