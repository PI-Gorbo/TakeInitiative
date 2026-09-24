# 02 — Drop the GorboPackages dependency

## Goal

Inline `GP.MartenIdentity` so the repo owns its identity code, and `dotnet restore`
succeeds with only `nuget.org` configured. This removes the single hardest
blocker: right now the project cannot restore at all without a GitHub PAT.

## Depends on

Nothing. Can run in parallel with 01.

## Files touched

**Create** — copied from `/Users/sam/projects/GorboPackages/GP.MartenIdentity/`
into `apps/TakeInitiative.Api/src/Identity/`:

```
Identity/
├── MartenUserStore.cs               (490 lines)
├── MartenRoleStore.cs               (81)
├── Extensions.cs                    (11, optional — see below)
└── Entities/
    ├── MartenIdentityUser.cs        (14)
    ├── MartenIdentityRole.cs        (2)
    ├── MartenIdentityClaim.cs       (20)
    ├── MartenIdentityUserLogin.cs   (19)
    ├── MartenIdentityUserToken.cs   (13)
    └── MartenIdentityRecoveryCode.cs (6)
```

**Modify**
- `apps/TakeInitiative.Api/TakeInitiative.Api.csproj`
- `apps/TakeInitiative.Api/src/Features/User/Models/ApplicationUser.cs`
- `apps/TakeInitiative.Api/src/Features/User/Models/ApplicationUserRole.cs`
- `apps/TakeInitiative.Api/src/boostrap/Bootstrap.cs`
- `.github/workflows/testApi.yml`
- `apps/TakeInitiative.Api/dockerfile`
- `README.md` (delete the `## Nuget` section)

## Steps

### 1. Copy the source

Copy the 9 files listed above. **Copy — do not revert commit `438aca2`.**

That commit ("feat: Swapped to Gorbo Packages role store", 2024-12-03) deleted the
*pre-generic* `MartenUserStore.cs` / `MartenRoleStore.cs`, but every current call
site is written against the generic `MartenUserStore<TUser, TRole>` /
`MartenRoleStore<TRole>` shape. Copying means zero call-site rewrites; reverting
would mean undoing four of them. Keep `git show 438aca2^:<path>` as a
cross-reference if something looks wrong.

### 2. Rename the namespace

`GP.MartenIdentity` → `TakeInitiative.Api.Identity` across all 9 files, then fix
the three `using` lines that reference it:

- `src/Features/User/Models/ApplicationUser.cs`
- `src/Features/User/Models/ApplicationUserRole.cs`
- `src/boostrap/Bootstrap.cs`

(Keeping the original namespace would need zero edits, but leaves a `GP.` prefix
in a repo that no longer has anything to do with GorboPackages.)

### 3. Fix two bugs while copying

Both in `MartenRoleStore.cs`:

| Line | Bug | Fix |
|---|---|---|
| ~57 | `GetRoleIdAsync` returns `role.ToString()` — the **type name**, not the id | `role.Id.ToString()` |
| ~26 | `DeleteAsync` returns `IdentityResult.Failed()` after a successful delete + save | `IdentityResult.Success` |

Neither is currently exercised — there are no role-management endpoints — but both
are live landmines the moment roles get used.

### 4. csproj

```diff
- <PackageReference Include="GP.MartenIdentity" Version="1.1.1" />
+ <PackageReference Include="Microsoft.Extensions.Identity.Stores" Version="..." />
```

`Microsoft.Extensions.Identity.Stores` brings `IUserStore` / `IRoleStore` plus
`Microsoft.Extensions.Identity.Core`, which arrived transitively via
`GP.MartenIdentity` before. Match the version to the target framework — step 03
moves this to `net10.0`, so use the `10.0.x` line if 03 has already landed,
otherwise `8.0.10` and bump it in 03.

The other three refs from GorboPackages' csproj
(`Microsoft.Extensions.DependencyInjection`, `.Abstractions`,
`Logging.Abstractions`) are framework-provided under `Microsoft.NET.Sdk.Web`.
**Do not add them.**

`Marten` is already at `7.31.1`, which is exactly what GorboPackages built
against. No change.

### 5. Purge the private feed

`.github/workflows/testApi.yml` — delete the whole step:
```yaml
- name: Add Gorbo Packages Nuget Source.
  run: dotnet nuget add source ... "https://nuget.pkg.github.com/PI-Gorbo/index.json"
```

`apps/TakeInitiative.Api/dockerfile` — delete the `ARG personal_github_token`
and the `dotnet nuget add source` line it feeds.

`README.md` — delete the entire `## Nuget` section documenting the
`nuget.config` shape.

The `GORBO_PACKAGES_GITHUB_TOKEN` GitHub secret becomes dead and can be revoked.

### 6. Optional: adopt the unique email index

`Extensions.cs` provides `StoreOptions.RegisterIdentityModels<TUser,TRole>()`,
which registers the Marten schema **and adds a unique index on
`NormalizedEmail`**. `Bootstrap.cs` currently does its own
`opts.Schema.For<ApplicationUser>()` without that index.

That is a real data-integrity gap — nothing stops two users sharing an email.
Worth adopting, but note it will **fail on existing data if duplicates already
exist**. Check first:

```sql
SELECT data->>'NormalizedEmail', count(*) FROM mt_doc_applicationuser
GROUP BY 1 HAVING count(*) > 1;
```

If this is deferred, delete `Extensions.cs` rather than copying a file nothing calls.

## Verify

```bash
dotnet nuget list source          # should show ONLY nuget.org
rm -rf apps/TakeInitiative.Api/obj apps/TakeInitiative.Api/bin
dotnet restore                    # must succeed with no PAT
grep -rn "GP.MartenIdentity" --include='*.cs' --include='*.csproj' --include='*.yml' . | grep -v node_modules
grep -rn "personal_github_token\|nuget.pkg.github.com" . | grep -v node_modules
```

The last two greps must return nothing. Then:

```bash
grep -c "GP.MartenIdentity" apps/TakeInitiative.Api/obj/project.assets.json   # 0
```

Build and test verification belongs to step 03 — until the retarget lands,
`dotnet build` still fails on the missing net8 runtime.

## Notes / gotchas

- **Scope is small**: 656 lines, 9 files, one namespace, zero third-party
  dependencies. Everything it needs is either already referenced or
  framework-provided.
- **Ignore `GP.IdentityEndpoints`** — the F# sibling project (318 lines). Nothing
  in TakeInitiative references it; verified by grepping for its namespace, module
  names, `IConfirmEmailSender` and `AttachCookieToContext`. Pulling it in would
  mean adding an F# project plus FluentValidation and FsToolkit for no benefit.
- **No git history to preserve.** Despite the recollection that identity code was
  extracted *from* TakeInitiative, GorboPackages' history shows greenfield
  authoring — first commit `32886f6 (wip) Created The GP.MartenIdentity Package`,
  2024-10-24, with no import commit. `git log --all --grep=TakeInitiative -i`
  in that repo returns nothing.
- **Version is the tip of main.** TakeInitiative pins `1.1.1`; GorboPackages'
  latest tag is `MartenIdentity.1.1.1`. Nothing newer to reconcile, nothing older
  to worry about.
- **Design detail worth knowing**: `MartenIdentityUser.Roles` is `List<TRole>` —
  full role documents embedded by value in each user document, so role renames do
  not propagate. Also, TakeInitiative's `ApplicationUserRole` adds
  `IList<Claim> Claims` using `System.Security.Claims.Claim` rather than the
  serializable `MartenIdentityClaim`. That is an existing serialization hazard;
  note it, don't fix it here.
- `MartenUserStore` queries nested arrays via LINQ (`u.Claims.Any(...)`), which
  Marten translates to Postgres JSONB containment. Relevant only if storage ever
  changes.
