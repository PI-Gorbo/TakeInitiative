# CLAUDE.md

## CI/CD runs with no warnings

**A warning is a build failure.** The .NET build in CI passes
`-p:TreatWarningsAsErrors=True`, so anything the compiler or the analyzers would
normally mutter about — an unused variable, an obsolete call, a nullable
dereference — stops the build outright. There is no "it's only a warning" here.

This bites hardest on nullability. Every project sets `<Nullable>enable</Nullable>`,
so `CS8602: Dereference of a possibly null reference` is an error like any other.
Code that reads fine locally in `Debug` can still fail the pipeline, because CI
builds `Release` with the flag on.

Do not silence a warning to get green. Fix the cause: narrow the type, bind a
local, add the guard the compiler is asking for. `#pragma warning disable`,
`<NoWarn>` and a blanket `!` sprinkled through a method are all ways of moving the
bug rather than removing it. When a suppression genuinely is the right answer,
document why next to it — `Directory.Build.props` is the model for that, where
each NuGet audit suppression carries the reasoning that justifies it.

The web half is held to the same bar: `nuxi typecheck` must be clean, and the
committed `schema.d.ts` must match what the API's OpenAPI document regenerates.

## Verify before you hand work back

Run the gates CI runs, from the repo root. These are the real commands from
`.github/workflows/`, not approximations:

```bash
# Api (testApi.yml) — this is the one that treats warnings as errors
dotnet restore
dotnet build --configuration Release --no-restore -p:TreatWarningsAsErrors=True
dotnet test -- --verbosity normal

# Web (testWeb.yml)
pnpm install --frozen-lockfile
pnpm turbo run gen:api --filter=@ti/web   # then: git diff --exit-code -- apps/TakeInitiative.Web/utils/api/schema.d.ts
pnpm --filter @ti/web exec nuxi typecheck
pnpm --filter @ti/web test
```

A build that reports `0 Warning(s), 0 Error(s)` is the only passing build.

Both workflows are path-filtered: `**.cs` and `**.csproj` trigger *both* of them,
so a C# change has to satisfy the web pipeline too.

## Stacked PRs

Work here often lands as a stack. Every PR in a stack builds on its own — a
branch that only compiles once the branch above it merges is a broken branch.
Run the gates on each one, not just on the tip.

## Layout

| Path | What it is |
|---|---|
| `apps/TakeInitiative.Api` | FastEndpoints + Marten API, `net10.0` |
| `apps/TakeInitiative.Api.Tests` | xUnit; integration tests host the API in-process via Alba |
| `apps/TakeInitiative.Web` | Nuxt 3 front end (`@ti/web`) |
| `packages/TakeInitiative.Dice` | The dice language, parser and evaluator |
| `docs/roadmap` | Source of truth for what is done and what is next — read `README.md` first |

pnpm + Turborepo drive the workspace; the .NET SDK is pinned in `global.json`.
