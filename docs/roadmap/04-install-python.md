# 04 — Install Python so the app runs as-is

## Goal

Get the embedded CPython interpreter working on this machine so dice rolling
functions, without changing how it works. This is explicitly a **stopgap** —
Stage 2 deletes all of it. Keep the footprint small and easy to remove.

## Depends on

**01** (the setup script hooks into `setup_environment`) and **03** (the app must
build before it can run).

## Files touched

- `scripts/setup-python.mjs` (create)
- `package.json` — add `setup:python`, and add it to the `setup_environment` chain
- `apps/TakeInitiative.Api/src/boostrap/Bootstrap.cs` — small change to
  `AddDiceRollers`
- `.gitignore` — ensure `.venv/` is ignored (`appsettings.Development.json`
  already is, at `.gitignore:487`)

## Steps

### 1. Why uv, and why 3.11

`uv 0.11.5` is already installed. It provides standalone CPython builds that
**include the `libpython` shared library** — which is the whole problem here.
The system `python3` is 3.9.6 from the Xcode command line tools and ships no
usable dylib, so pythonnet cannot embed it at all.

Target **3.11** to match the Dockerfile's hardcoded discovery path and CI's
`3.11.9`, and because `pythonnet 3.0.3` predates 3.13 support.

### 2. `scripts/setup-python.mjs`

Idempotent, and safe to run on every `pnpm dev`:

1. `uv python install 3.11`
2. `uv venv --python 3.11 .venv` at the repo root
3. `uv pip install -r requirements.txt` (`d20==1.1.2`)
4. Resolve absolute paths and write `apps/TakeInitiative.Api/appsettings.Development.json`:

```jsonc
{
    "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
    "PythonDLL":  "<uv python dir>/lib/libpython3.11.dylib",
    "PythonHome": "<uv python dir>",
    "PythonPath": "<repo>/.venv/lib/python3.11/site-packages"
}
```

Resolve `<uv python dir>` at runtime rather than hardcoding — something like
`uv python find 3.11` and walking up from the returned interpreter path. Do not
bake `/Users/sam/...` into a committed file.

The file is gitignored, which is exactly why it is the right home for
machine-specific absolute paths.

**Preserve an existing file if the developer has customised it** — merge the three
Python keys rather than overwriting wholesale.

**Non-macOS:** the extension differs (`.so` on Linux, `.dll` on Windows). Either
branch on `process.platform` or glob for `libpython3.11.*` in `lib/`.

### 3. Bootstrap change

`AddDiceRollers` currently sets only `Runtime.PythonDLL`. That is not enough when
the interpreter and the packages live in different prefixes: CPython finds its
stdlib but not `d20`. Add the two optional settings before `Initialize()`:

```csharp
Runtime.PythonDLL = pythonConfig;

var pythonHome = configuration.GetValue<string>("PythonHome");
if (!string.IsNullOrWhiteSpace(pythonHome))
    PythonEngine.PythonHome = pythonHome;

var pythonPath = configuration.GetValue<string>("PythonPath");
if (!string.IsNullOrWhiteSpace(pythonPath))
    PythonEngine.PythonPath = pythonPath;

PythonEngine.Initialize();
PythonEngine.BeginAllowThreads();
```

Both must be set **before** `Initialize()`; setting them afterwards silently does
nothing. Leave the existing Linux `.so` autodiscovery branch (`pythonConfig == "null"`)
untouched — the container still uses it.

### 4. Wire into the dev script

```jsonc
"setup:python": "node scripts/setup-python.mjs",
"setup_environment": "(pnpm i) && (pnpm run setup:python) && (docker compose -p takeinitiative -f compose.dev.yml up -d postgres)",
```

## Verify

Interpreter and package, outside the app:
```bash
node scripts/setup-python.mjs
ls -la .venv/lib/python3.11/site-packages/d20
.venv/bin/python -c "import d20; print(d20.roll('2d20kh1'))"
```

Then end-to-end — the fastest proof the *embedded* interpreter resolved both its
stdlib and `d20`:

1. `pnpm dev`
2. Sign up, create a campaign
3. Add a character with initiative `2d20kh1`

That path runs `DiceRoller.EvaluateRoll` through `UnevaluatedCharacterInitiativeValidator`,
so a successful save means the whole chain works. A failure here surfaces as a
validation error containing a CPython message.

## Notes / gotchas

- **The committed `appsettings.json` points at
  `C:/Users/sam.gorbatov/AppData/Local/Programs/Python/Python312/python312.dll`** —
  a Windows path from another machine. Leave it alone; `appsettings.Development.json`
  overrides it. Step 11 removes the key from both.
- **`PYTHONHOME` may not be necessary.** python-build-standalone usually infers
  its prefix from the dylib location. Setting it explicitly is the robust choice,
  but if it causes trouble, try omitting it before debugging further.
- **`uv pip install` into a managed interpreter** — installing directly into the
  uv-managed Python (skipping the venv) would avoid needing `PythonPath` at all,
  but uv may refuse to write to an interpreter it manages. The venv route is
  guaranteed to work, which is why it's the default here.
- **Keep the footprint removable.** Everything in this step is deleted in step 11.
  Don't scatter Python setup across multiple files.
- `PythonEngine.BeginAllowThreads()` is already called, which is correct for a web
  app — it releases the GIL so requests don't serialise on it. `DiceRoller`
  re-acquires it per call with `using (Py.GIL())`.
- Tests never touch Python: `WebAppWithDatabaseFixture` substitutes `IDiceRoller`.
  So `dotnet test` passing does **not** prove this step worked — use the manual
  path above.
