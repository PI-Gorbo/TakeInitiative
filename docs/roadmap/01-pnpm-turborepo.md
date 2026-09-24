# 01 — pnpm + Turborepo

## Goal

Replace Bun and the Makefile with a pnpm 10 workspace driven by Turborepo, using
the same dev-script idiom as Ripple. At the end, `pnpm install` succeeds and
`pnpm dev` starts Postgres and launches both apps in a turbo TUI — even though
the API itself won't build yet (that needs steps 02 and 03).

## Depends on

Nothing. Start here.

## Files touched

**Create**
- `pnpm-workspace.yaml`
- `turbo.json`
- `.npmrc`
- `apps/TakeInitiative.Api/package.json`
- `apps/TakeInitiative.Api.Tests/package.json`

**Rewrite**
- `package.json` (root)
- `apps/TakeInitiative.Web/package.json` (rename only)

**Delete**
- `makefile`
- `apps/TakeInitiative.Api/makefile`
- `bun.lock`, `bun.lockb`
- `apps/TakeInitiative.Web/bun.lock`, `apps/TakeInitiative.Web/bun.lockb`

## Steps

### 1. Workspace definition

`pnpm-workspace.yaml`:

```yaml
packages:
    - "apps/*"
    - "packages/*"
```

`.npmrc` — start empty or omit. Ripple's two `public-hoist-pattern` lines exist
only for Sentry/OpenTelemetry instrumentation, which this repo does not use.
Do not copy them.

### 2. Root `package.json`

Keep `"version": "1.0.14"` — step 06 makes this the version of record for
release-please, so it must not be reset.

Follow Ripple's script conventions: `( ... ) && ( ... )` subshell chains so a
failure short-circuits, and section-header keys for visual grouping.

```jsonc
{
    "name": "takeinitiative",
    "version": "1.0.14",
    "private": true,
    "type": "module",
    "scripts": {
        "___ SETUP ___": "",
        "setup_environment": "(pnpm i) && (pnpm run setup:python) && (docker compose -p takeinitiative -f compose.dev.yml up -d postgres)",
        "___ DEV ___": "",
        "dev": "pnpm run setup_environment && (pnpm turbo run dev --ui tui)",
        "api": "pnpm run setup_environment && (pnpm turbo watch dev --filter=@ti/api)",
        "web": "pnpm run setup_environment && (pnpm turbo watch dev --filter=@ti/web)",
        "___ UTILITY ___": "",
        "build": "pnpm turbo run build",
        "test": "pnpm turbo run test"
    },
    "devDependencies": {
        "turbo": "2.9.16"
    },
    "packageManager": "pnpm@10.33.0",
    "volta": { "node": "24.15.0" }
}
```

Notes on specific choices:

- `up -d postgres` starts **only** the database. The API and web run natively
  under turbo. This mirrors Ripple starting only `database` / `createbuckets`
  and never its own `api` / `pwa` compose services.
- `-p takeinitiative` sets an explicit compose project name, as Ripple does with
  `-p ripple`.
- `setup:python` does not exist yet — step 04 creates it. **Until then, leave it
  out of the chain** or the script fails. Add it when step 04 lands.
- Drop `@effect/cli`, `@effect/platform-node`, `effect`, `concurrently` and
  `@types/bun`. The Effect deps exist solely for `Scripts/Release.ts`, which
  step 06 deletes. `concurrently` is replaced by turbo.
- `volta` and `packageManager` match Ripple. There is no `engines` field in
  Ripple; don't invent one.

### 3. `turbo.json`

Ripple's is heavily tuned for remote caching across 11 packages. This repo has
three and no remote cache, so keep it minimal — do **not** copy Ripple's file
wholesale.

```jsonc
{
    "$schema": "https://turborepo.com/schema.json",
    "ui": "stream",
    "tasks": {
        "build": {
            "dependsOn": ["^build"],
            "outputs": ["dist/**", ".nuxt/**", ".output/**", "bin/**", "obj/**"]
        },
        "dev": {
            "dependsOn": ["^build"],
            "persistent": true,
            "cache": false
        },
        "test": {
            "dependsOn": ["^build"]
        }
    }
}
```

### 4. Package manifests per workspace

Turbo discovers workspaces through `package.json`, so the .NET projects need thin
ones. This is the agreed trade-off for Ripple parity — a `package.json` sitting
next to a `.csproj` looks odd but costs nothing.

`apps/TakeInitiative.Api/package.json`:
```json
{
    "name": "@ti/api",
    "private": true,
    "scripts": {
        "dev": "dotnet watch run",
        "build": "dotnet build"
    }
}
```

`apps/TakeInitiative.Api.Tests/package.json`:
```json
{
    "name": "@ti/api-tests",
    "private": true,
    "scripts": {
        "test": "dotnet test"
    }
}
```

`apps/TakeInitiative.Web/package.json` — rename `"nuxt-app"` to `"@ti/web"`.
Leave every dependency and every other script untouched; this step is not a
frontend upgrade.

### 5. Remove Bun

Delete both makefiles and all four bun lockfiles. Then `pnpm install` at the root
to generate `pnpm-lock.yaml`.

Add to `.gitignore` if not already covered: `.turbo/`.

## Verify

```bash
rm -rf node_modules apps/*/node_modules
pnpm install                      # generates pnpm-lock.yaml, no bun involved
pnpm turbo run build --filter=@ti/web   # nuxt build succeeds
pnpm dev                          # postgres starts; turbo TUI opens with api + web panes
```

The API pane **will** fail at this stage — it cannot restore `GP.MartenIdentity`
(step 02) and targets a runtime that isn't installed (step 03). That is expected.
The web pane must come up cleanly and serve on its dev port.

Confirm no Bun remains:
```bash
grep -rn "bun" --include=package.json --include=makefile . | grep -v node_modules
find . -name 'bun.lock*' -not -path '*/node_modules/*'
```

## Notes / gotchas

- **`bun` is not installed on this machine**, so nothing JS-side works today
  regardless. This step is a strict improvement, not a lateral move.
- The old `makefile` was already partly broken: `docker.compose`,
  `docker.publish` and all three `docker.refresh.*` targets run
  `docker compose up -d` against a `compose.yml` that does not exist — only
  `compose.dev.yml` does. `run.web` calls `npm` in a Bun project. Nothing of
  value is being lost.
- Ripple's root `package.json` has a vestigial npm-style `"workspaces"` array
  alongside `pnpm-workspace.yaml`. Don't copy it; pnpm reads only the YAML.
- Several Ripple sub-packages redundantly repeat `packageManager`, two of them
  with stale versions. Don't copy that either — declare it once at the root.
- Ripple's `dev` task also depends on a `copy-sw` task for its service worker.
  Not applicable here; this repo has no PWA yet.
- `dotnet watch run` inside turbo works, but turbo's TUI captures stdin. If
  `dotnet watch`'s hot-reload prompts become a problem, add
  `DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1` to the API's dev script to stop it
  asking.
