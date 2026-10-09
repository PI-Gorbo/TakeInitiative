# TakeInitiative

A webapp designed to help dungeon masters to create combats and track initiative
for those combats.

## Technologies

-   C# ASP.NET WebAPI for the backend
-   Nuxt Frontend (BFF pattern)
-   `packages/TakeInitiative.Dice` — the dice roll language (Parlot parser, type
    checker, evaluator); see `docs/roadmap/08-dice-language-spec.md`
-   pnpm workspace driven by Turborepo

## Requirements for local development

1. `Docker` (for Postgres, and for the full-stack compose setup)
2. `Node` 24 — via [Volta](https://volta.sh), which reads the pinned version
   from `package.json`
3. `pnpm` 10 — `corepack enable` picks up the pinned version
4. The `.NET 10 SDK`

You do **not** need `make`, `bun`, or Python.

## QuickStart

```bash
pnpm install
pnpm dev
```

`pnpm dev` will:

1. Install node dependencies.
2. Create `apps/TakeInitiative.Web/.env` from `TEMPLATE.env` if it is missing.
3. Start Postgres in Docker.
4. Run the API and the web app side by side in a Turborepo TUI.

| Service  | URL                     |
| -------- | ----------------------- |
| Web      | http://localhost:3000   |
| API      | http://localhost:5010   |
| Postgres | `localhost:7401`        |

## Working on individual projects

-   `pnpm api` — Postgres plus the API only
-   `pnpm web` — Postgres plus the web app only
-   `pnpm build` — build everything through turbo
-   `pnpm test` — run the dice language tests and the API test suite
    (Alba + Testcontainers; needs Docker)

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

The API applies all Marten schema changes on startup in every environment, so a
stale local database can block startup. (`Marten:ApplySchemaOnStartup=false` turns
that off, for a database whose DDL is applied out of band.) To wipe it:

```bash
docker compose -p takeinitiative -f compose.dev.yml down -v
```

## Production

Production runs from images built by GitHub Actions and pushed to GHCR
(`.github/workflows/images.yml`), which Coolify pulls. Nothing on the server builds.

A release and a deploy are two decisions. Merging release-please's PR on `dev` cuts a release and
publishes `1.2.3`; merging the `release:` PR that then opens into `main` is what ships it, by
pinning each Coolify application to that version's exact image **digest** over the tailnet.

| File | What it is |
| --- | --- |
| `docs/deploy/pipeline.md` | **Start here.** The deploy pipeline: one-time setup, rollback, and what to do when it fails |
| `.github/workflows/deploy.yml` | Deploys prod on a push to `main`. Builds nothing |
| `deploy-targets.json` | Which Coolify application each app is, and the per-app on/off switch |
| `scripts/ci/` | Every decision a deploy makes, unit tested — `pnpm deploy:test` |
| `docs/deploy/coolify.md` | How the deployment was built by hand (sections 5 and 11 superseded) |
| `docs/deploy/operations.md` | Running it afterwards: deploys, rollback, backups, the KB ingest tunnel |
| `docs/deploy/production.env.example` | Every environment variable production needs, which are secret, and where each is set |
| `compose.prod.yml` | **No longer what Coolify runs.** Kept to run the production images locally with production-shaped config, and as the retreat if the pipeline breaks |

Postgres is not in any of that — it is a Coolify-managed PostgreSQL 16 resource, so its lifecycle
and its backups are separate from app redeploys. See `docs/roadmap/29-deploy.md` for the reasoning
behind all of it, including its 2026-10-07 amendment.
