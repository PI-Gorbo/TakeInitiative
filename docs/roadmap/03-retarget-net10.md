# 03 — Retarget to net10.0

## Goal

Move both .NET projects from `net8.0` to `net10.0` so they build and run on the
only runtime installed on this machine. At the end, `dotnet build` is clean and
the test suite passes.

## Depends on

**02** — the API cannot build while `GP.MartenIdentity` is unresolvable.

## Files touched

- `apps/TakeInitiative.Api/TakeInitiative.Api.csproj`
- `apps/TakeInitiative.Api.Tests/TakeInitiative.Api.Tests.csproj`
- `global.json` (create)
- `.github/workflows/testApi.yml`
- `apps/TakeInitiative.Api/dockerfile`

## Steps

### 1. Retarget

`<TargetFramework>net8.0</TargetFramework>` → `net10.0` in both csproj files.

### 2. Pin the SDK

Create `global.json` so this never silently drifts again:

```json
{
    "sdk": {
        "version": "10.0.400",
        "rollForward": "latestFeature"
    }
}
```

`latestFeature` allows 10.0.4xx patches without allowing a jump to .NET 11.

### 3. Bump framework-tied packages

Move the `Microsoft.*` packages onto the `10.0.x` line:

- `Microsoft.Extensions.Identity.Stores` (added in step 02)
- `Microsoft.AspNetCore.OpenApi` — currently `8.0.1`

`Microsoft.AspNetCore.Cors 2.2.0` is an ancient package that has been part of the
shared framework since ASP.NET Core 3.0. **Remove it entirely** rather than
bumping it — it does nothing.

### 4. Keep Marten at 7.31.1

Deliberate. Marten 7.31.1 targets `net8.0` and runs fine on net10 — library TFMs
roll forward. Marten 8 reworked the projection APIs, and Stage 3 rewrites the
projections anyway, so taking that upgrade here means absorbing breaking changes
for zero benefit.

Same reasoning for `FastEndpoints 5.22.0` and the other third-party packages:
retarget the app, not the dependency tree.

### 5. CI

`.github/workflows/testApi.yml`:
```diff
- dotnet-version: 8.0.x
+ dotnet-version: 10.0.x
```

### 6. Docker base images

`apps/TakeInitiative.Api/dockerfile`: `sdk:8.0` → `sdk:10.0`,
`aspnet:8.0` → `aspnet:10.0`.

**This breaks the container's Python — read the gotcha below before proceeding.**

## Verify

```bash
dotnet --list-runtimes            # Microsoft.NETCore.App 10.0.x present
dotnet restore
dotnet build --configuration Release -p:TreatWarningsAsErrors=True
dotnet test
```

Warnings-as-errors matters: CI already enforces it, so a build that passes locally
without it can still fail CI. .NET 10 ships new analyzers, so expect a handful of
new diagnostics — fix them rather than suppressing the flag.

Then run the app:
```bash
docker compose -p takeinitiative -f compose.dev.yml up -d postgres
dotnet run --project apps/TakeInitiative.Api
curl -f http://localhost:5010/healthz
```

The Alba + Testcontainers suite is the real signal here — it stubs `IDiceRoller`
via NSubstitute, so it exercises the Marten and identity wiring **without needing
Python**. If those tests pass, steps 02 and 03 are both good.

## Notes / gotchas

### The Docker Python collision

This is the one genuinely awkward part of this step.

The runtime image apt-installs `python3`, and `Bootstrap.AddDiceRollers` contains
a hardcoded discovery path:

```csharp
var configName = new DirectoryInfo("/usr/lib/python3.11")
    .EnumerateDirectories("config-3.11-*").First();
pythonConfig = configName.FullName + "/libpython3.11.so";
```

Moving to the .NET 10 base image changes the Debian release, which changes the
default `python3` version, which breaks that path — and possibly exceeds what the
pinned `pythonnet 3.0.3` supports (3.13 support landed later in the 3.x line).

**Do not solve this properly.** Stage 2 deletes Python from the container
entirely, so any fix here is throwaway work. Pick the cheapest option that keeps
the image building:

1. Pin the container's Python explicitly to 3.11 or 3.12 rather than inheriting
   the base image default, and widen the discovery glob to match.
2. Or leave the API Dockerfile on the .NET 8 base for now and note it — local dev
   via `dotnet run` is what matters for Stage 1, and the compose API service was
   already unbuildable before step 02 anyway.

Whichever is chosen, record it at the top of `11-remove-python.md` as something to
undo.

### Other

- Only `Microsoft.NETCore.App 10.0.11` and `Microsoft.AspNetCore.App 10.0.11` are
  installed. There is no net8 runtime, which is why `dotnet run` fails today even
  before the NuGet problem.
- `InvariantGlobalization` is enabled in the API csproj. Unchanged by this step,
  but worth knowing if any culture-sensitive formatting starts misbehaving.
- `InternalsVisibleTo $(MSBuildProjectName).Tests` is set — the test project sees
  internals. Keep it.
- The test project pins `xunit 2.9.0` while the API pins `xunit 2.8.0` (the API
  referencing xunit at all is odd). Not worth fixing here, but if version
  conflicts surface under the new SDK, align them.
