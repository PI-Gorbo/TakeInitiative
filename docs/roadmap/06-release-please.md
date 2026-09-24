# 06 — release-please

## Goal

Automate releases with release-please and delete the hand-rolled release script.
At the end, merging a conventional commit to `dev` opens or updates a release PR,
and merging that PR tags the release.

## Depends on

**01** (root `package.json` must be the pnpm one, and must still say `1.0.14`).

## Files touched

- `.github/workflows/release.yml` (create)
- `release-please-config.json` (create)
- `.release-please-manifest.json` (create)
- `.husky/commit-msg` (create)
- `package.json` — add `"prepare": "husky"`, add husky to devDependencies
- `apps/TakeInitiative.Api/TakeInitiative.Api.csproj` — add `<Version>`
- **Delete** `Scripts/Release.ts`, root `tsconfig.json`

## Steps

### 1. Delete, don't port

`Scripts/Release.ts` is 126 lines of Effect CLI plus `import { $ } from "bun"`.
Its entire job — bump the version, branch `release/x.y.z`, commit, push, open a
PR — is what release-please does natively. The git history confirms that is the
whole flow:

```
dcd4acd Incremented package.json to version 1.0.14
ace894c Merge pull request #183 from PI-Gorbo/release/1.0.14
```

Porting it to pnpm would mean replacing the Bun shell API and running it under
`tsx`, to reimplement a solved problem. Delete it, along with the root
`tsconfig.json` (Bun-specific) and the `@effect/cli`, `@effect/platform-node` and
`effect` dependencies that exist only to serve it.

### 2. Single-version mode, not Ripple's manifest mode

This is the one place to **deliberately diverge from Ripple**.

Ripple uses manifest mode with 11 independently-versioned components because its
packages ship separately. TakeInitiative's API and web always ship together, so
the root `package.json` stays the single version of record.

`release-please-config.json`:
```json
{
    "$schema": "https://raw.githubusercontent.com/googleapis/release-please/main/schemas/config.json",
    "release-type": "node",
    "packages": {
        ".": { "changelog-path": "CHANGELOG.md" }
    },
    "changelog-sections": [
        { "type": "feat",     "section": "Features" },
        { "type": "fix",      "section": "Bug Fixes" },
        { "type": "perf",     "section": "Performance Improvements" },
        { "type": "refactor", "section": "Code Refactoring" },
        { "type": "chore",    "section": "Miscellaneous", "hidden": true },
        { "type": "docs",     "section": "Documentation", "hidden": true },
        { "type": "style",    "section": "Styles",        "hidden": true },
        { "type": "test",     "section": "Tests",         "hidden": true },
        { "type": "build",    "section": "Build System",  "hidden": true },
        { "type": "ci",       "section": "CI",            "hidden": true }
    ]
}
```

**Seed the manifest so numbering continues rather than restarting at 0.1.0:**
```json
{ ".": "1.0.14" }
```

### 3. Workflow

`.github/workflows/release.yml`, modelled on Ripple's but without the
per-app `.version` file step:

```yaml
name: Release
on:
  push:
    branches: [dev]
concurrency: ${{ github.workflow }}-${{ github.ref }}
permissions:
  contents: write
  pull-requests: write
jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: googleapis/release-please-action@v4
        with:
          config-file: release-please-config.json
          manifest-file: .release-please-manifest.json
```

Note `dev` is this repo's default branch, so that is the release branch — Ripple
also releases from `dev`.

### 4. Conventional Commits enforcement

release-please derives versions from commit messages, and this repo's history is
not conventional — `wip`, `Incremented package.json to version 1.0.14`, merge
commits, and one commit that is mostly capitalised profanity about LiteDB.

Nothing retroactive is needed; release-please only reads commits since the last
tag. But going forward it must be enforced.

Copy Ripple's approach — a **grep-based `.husky/commit-msg` hook**, not
commitlint. It has no dependencies and gives a good error message. See
`/Users/sam/projects/Ripple/.husky/commit-msg` for the exact shape.

Scopes for this repo: `api`, `web`, `identity`, `dice`, `root`, `ci`, `docs`.

Add `"prepare": "husky"` to the root `package.json` and husky to devDependencies.

### 5. Propagate the version

The old script only bumped `package.json`. Wire the version into the things that
actually ship:

- `<Version>` in `TakeInitiative.Api.csproj`
- The Docker image tag — the deleted makefile hardcoded `:latest`

Simplest approach: a small `scripts/sync-version.mjs` reading root
`package.json`, run in the release workflow after release-please opens its PR
(Ripple does the equivalent with its `.version` files step).

## Verify

```bash
git commit -m "bad message"          # rejected by the hook
git commit -m "feat(api): thing"     # accepted
```

Then, on a branch pushed to GitHub: land a `feat:` commit on `dev` and confirm
release-please opens a release PR proposing `1.1.0` (not `0.1.0` — that would mean
the manifest wasn't seeded). Merge it and confirm the tag and GitHub release
appear.

Confirm the old script is gone:
```bash
ls Scripts/ 2>/dev/null
grep -n "effect" package.json
```

## Notes / gotchas

- **Do not reset the version.** Seeding `.release-please-manifest.json` at
  `1.0.14` is what preserves continuity with the 14 existing releases and their
  `release/x.y.z` branches.
- `bump-minor-pre-major` / `bump-patch-for-minor-pre-major` in Ripple's config are
  for pre-1.0 packages. This repo is past 1.0, so omit them — otherwise version
  bumps behave unexpectedly.
- There is no `CHANGELOG.md` yet; release-please creates it on the first release.
- The `release/1.0.x` remote branches from the old flow are stale after this
  lands. Cleaning them up is optional tidying, not part of this step.
