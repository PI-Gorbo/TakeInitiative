# TakeInitiative

A webapp designed to help dungeon masters to create combats and track initiative
for those combats.

## Technologies

-   C# ASP.NET WebAPI for the backend
-   Nuxt Frontend (BFF pattern)
-   pnpm workspace driven by Turborepo

## Requirements for local development

1. `Docker` (for Postgres, and for the full-stack compose setup)
2. `Node` 24 — via [Volta](https://volta.sh), which reads the pinned version
   from `package.json`
3. `pnpm` 10 — `corepack enable` picks up the pinned version
4. The `.NET 10 SDK`

You do **not** need `make`, `bun`, or a manual Python install. `pnpm dev`
installs the Python interpreter the dice roller embeds.

## QuickStart

```bash
pnpm install
pnpm dev
```

`pnpm dev` will:

1. Install node dependencies.
2. Create `apps/TakeInitiative.Web/.env` from `TEMPLATE.env` if it is missing.
3. Install CPython 3.11 via `uv`, create `.venv`, install `d20`, and write the
   resolved interpreter paths into the gitignored
   `apps/TakeInitiative.Api/appsettings.Development.json`.
4. Start Postgres in Docker.
5. Run the API and the web app side by side in a Turborepo TUI.

| Service  | URL                     |
| -------- | ----------------------- |
| Web      | http://localhost:3000   |
| API      | http://localhost:5010   |
| Postgres | `localhost:7401`        |

## Working on individual projects

-   `pnpm api` — Postgres plus the API only
-   `pnpm web` — Postgres plus the web app only
-   `pnpm build` — build everything through turbo
-   `pnpm test` — run the API test suite (Alba + Testcontainers; needs Docker)

## Running everything in containers

```bash
docker compose -p takeinitiative -f compose.dev.yml up -d --build
```

This builds and runs Postgres, the API and the web app.

| Service  | URL                     |
| -------- | ----------------------- |
| Web      | http://localhost:7403   |
| API      | http://localhost:7402   |
| Postgres | `localhost:7401`        |

## Resetting the database

The API applies all Marten schema changes on startup in Development, so a stale
local database can block startup. To wipe it:

```bash
docker compose -p takeinitiative -f compose.dev.yml down -v
```
