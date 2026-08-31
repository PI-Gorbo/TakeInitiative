# 11 — Remove Python

## Goal

Delete every trace of Python from the repository. At the end, `grep -ri python`
returns nothing meaningful, and the Docker image and CI no longer install an
interpreter.

## Depends on

**10**, and the checkpoint at the end of it having been seen working.

## Files touched

**Delete**
- `requirements.txt`
- `scripts/setup-python.mjs`
- `.venv/` (and its `.gitignore` entry)

**Modify**
- `apps/TakeInitiative.Api/TakeInitiative.Api.csproj` — drop `pythonnet`
- `apps/TakeInitiative.Api/src/boostrap/Bootstrap.cs` — delete the Python block
- `apps/TakeInitiative.Api/appsettings.json` — drop `PythonDLL`
- `apps/TakeInitiative.Api/dockerfile` — drop all Python installation
- `.github/workflows/testApi.yml` — drop `setup-python` and the `PythonDLL` export
- `package.json` — drop `setup:python` from the `setup_environment` chain
- `README.md` — drop the Python requirement
- The ~12 FluentValidation validators — point them at `Check`
- `apps/TakeInitiative.Web/components/Campaign/Character/InitiativeRollInput.vue`

## Steps

### 1. Delete the bootstrap

Remove the whole block from `Bootstrap.AddDiceRollers`: the `PythonEngine.IsInitialized`
guard, the `PythonDLL` config read, the `InvalidConfigurationException`, the Linux
`.so` autodiscovery branch, `Runtime.PythonDLL`, `PythonEngine.Initialize()`,
`PythonEngine.BeginAllowThreads()`, and the `PythonHome` / `PythonPath` settings
added in step 04.

What remains should be a plain `services.AddTransient<IDiceRoller, DiceRoller>()`
alongside the existing `IInitiativeRoller` and `IHealthRoller` registrations.

Drop `using Python.Runtime;` from `Bootstrap.cs`.

### 2. Docker and CI

**This also retires the base-image Python problem deferred from step 03.** Check
the note recorded at the top of that step and undo whatever stopgap was applied —
if the API Dockerfile was left on the .NET 8 base, move it to `aspnet:10.0` now.

From the Dockerfile, remove: the `apt-get install python3 python3-dev python3-pip
libffi-dev`, the `rm /usr/lib/python3.11/EXTERNALLY-MANAGED` hack, `pip install d20`,
and `ENV PythonDLL=null`.

From `testApi.yml`, remove the `actions/setup-python` step and the
`export PythonDLL=...` line before `dotnet test`.

Also delete the stray empty `apps/TakeInitiative.Api/dockerignore` (beside the
real `.dockerignore`) if step 05 didn't.

### 3. Point the validators at `Check`

Roughly a dozen validators inject `IDiceRoller` purely to decide whether an
expression is valid, and do it by evaluating a throwaway roll:

- `UnevaluatedCharacterInitiativeValidator`
- `UnevaluatedCharacterHealthRollValidator`
- `UnevaluatedCharacterHealthValidator`
- `CharacterValidator` and its subclasses
- `PostPlannedCombatNpcRequestValidator`, `PutPlannedCombatNpcRequestValidator`
- `PostAddStagedCharacterRequestValidator`, `PutUpdateStagedCharacterRequestValidator`
- `PlayerCharacterDTOValidator`, `PostPlayerCharacterRequestValidator`,
  `PutPlayerCharacterRequestValidator`
- `StagedCombatCharacterDtoValidator`, `StagedCombatCharacterWithoutIdDtoValidator`,
  `PlannedCombatCharacterValidator`

Switch each to `Check`. Two benefits: no dice are rolled just to validate, and the
error message becomes the structured one from the spec instead of a generic
failure string.

Grep for `EvaluateRoll` afterwards — the only remaining callers should be places
that genuinely want a number.

### 4. Frontend errors and help text

`InitiativeRollInput.vue`:

- Update the placeholder from `1d20 + 2` and the tooltip, which currently teaches
  the legacy syntax: *"For Advantage, you can use `2d20kh1`, and disadvantage is
  `2d20kl1`"*. Teach `adv(1d20)` / `dis(1d20)` instead, and mention the old form
  still works.
- Surface the structured error inline. The component already takes an `error`
  prop; the win is that the message is now written for a DM rather than being a
  CPython exception.
- Fix the stray `>` in the tooltip text (`...a dice roll. >An example...`).

Consider showing `Evaluation` somewhere in the combat UI — it is stored, is in the
frontend Zod schema, and is currently rendered nowhere. `1d20 [14] + 3 = 17` at
the table is genuinely useful. Optional, but this is the natural moment.

## Verify

```bash
grep -ri "python\|pythonnet\|d20==" --include='*.cs' --include='*.csproj' \
    --include='*.json' --include='*.yml' --include='*.md' --include='dockerfile' \
    . | grep -v node_modules | grep -v docs/roadmap
```

Should return nothing but incidental matches. Then:

```bash
dotnet build --configuration Release -p:TreatWarningsAsErrors=True
dotnet test
docker compose -p takeinitiative -f compose.dev.yml build api
```

The image should be noticeably smaller — no interpreter, no dev headers.

End to end, on a machine with **no Python at all** available to the app:
`pnpm dev`, create a campaign, add characters using `1d20 + 2`, `adv(1d20)` and
the legacy `2d20kh1`, start a combat, roll initiative, end a turn.

Finally, confirm a bad expression produces a readable inline error rather than
anything mentioning Python or an exception type.

## Notes / gotchas

- **Point of no return.** Step 10's checkpoint exists so this step is only
  reached once the new roller is proven. If it hasn't been seen working
  end-to-end, go back.
- `d20==1.1.2` in `requirements.txt` was the entire Python dependency. There is
  no `.py` file anywhere in the repo and no `Process.Start` — the interpreter was
  embedded in-process via pythonnet purely to call one library.
- Removing `pythonnet` also removes its transitive native loading, which is what
  made the app sensitive to base-image changes in the first place.
- CI gets faster: no `setup-python` step, no pip install.
- After this, the only reason `.gitignore` mentions `appsettings.Development.json`
  is ordinary local config. The file itself can stay — step 05's other settings
  may still live there.
