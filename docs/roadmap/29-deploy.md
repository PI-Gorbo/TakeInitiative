# 29 — Deploy: images in Actions, pulled by Coolify

## Goal

The app runs on a machine other people can reach, from images **built by GitHub Actions and
pushed to GHCR**, which **Coolify pulls and runs**. Coolify never builds: building on a small
VPS is slow, memory-hungry and pointless when a free public runner already does it. The two
Dockerfiles that exist today were written for `compose.dev.yml` (the API runs as root and bakes
`curl` in for a compose healthcheck; the web image bakes the API's URL in at build time), so
both need a production pass before an image is worth publishing. The database schema needs one
too: `ApplyAllDatabaseChangesOnStartup()` is called **only in Development**, which means nothing
today creates `pg_trgm` and `unaccent` in production and ⌘K's fuzzy matching would fail on the
first search.

The deployment is four things: the **API**, the **web app**, **Postgres**, and an
**S3-compatible bucket** for images. Dev gets the bucket from MinIO in `compose.dev.yml`;
production can use anything that speaks S3, and this step picks one.

"Running" at the end: a friend opens `https://<web-domain>` on their phone, signs up, joins the
campaign from a join code, posts a note in the current session with an `@mention` and a photo,
and sees it appear on the owner's screen without a refresh. On the VPS,
`docker image inspect … --format '{{.RepoDigests}}'` prints a `ghcr.io/…@sha256:…` for both app
images — proof they were pulled, not built — and that digest matches the one in the GitHub
Actions run summary. `docker buildx du` on the VPS is empty.

The step ships as eight PRs stacked on `dev`, starting with this file's docs PR. Each PR leaves
the repo runnable: `pnpm dev`, `dotnet test` and the web suite keep working at every point, and
nothing is deployed until 29g.

| PR | Branch | Sub-step | Runnable state after merge | Status |
|---|---|---|---|---|
| 29a | `v2/29a-deploy-plan` | This plan | Docs only || [ ] |
| 29b | `v2/29b-api-production-ready` | Schema on startup, data-protection keys, proxy headers | The API applies its Marten schema in every environment behind `Marten:ApplySchemaOnStartup`, keeps its cookie keys on a volume, and trusts Traefik's `X-Forwarded-*`. Dev behaviour is unchanged || [ ] |
| 29c | `v2/29c-api-image` | Harden `apps/TakeInitiative.Api/dockerfile` | Non-root, one RID's native assets instead of thirteen, `/app`, a healthcheck, OCI labels. `compose.dev.yml` still builds and runs || [ ] |
| 29d | `v2/29d-web-image` | Harden `apps/TakeInitiative.Web/Dockerfile`, and make the API URL a **runtime** setting | The same image works for any domain: `NUXT_PUBLIC_AXIOS_BASE_URL` at container start replaces `--build-arg API_URL`. `/healthz` exists. The build context is allowlisted || [ ] |
| 29e | `v2/29e-ghcr-workflow` | `.github/workflows/images.yml` and the release hook | Every push to `dev` publishes `…:edge` and `…:sha-<short>` to GHCR; every release publishes `…:1.2.3`, `…:1.2`, `…:latest` || [ ] |
| 29f | `v2/29f-compose-prod` | `compose.prod.yml` and the env example | A committed production spec that references GHCR images and has **no `build:` key anywhere**. `docker compose -f compose.prod.yml config` validates || [ ] |
| 29g | `v2/29g-coolify` | `docs/deploy/coolify.md`, and the deployment itself | The app is live on its domain with TLS. This step's Verify passes || [ ] |
| 29h | `v2/29h-operations` | `docs/deploy/operations.md`: rollback, backups, the KB ingest tunnel | A rehearsed rollback, a restored backup, and the documented route for step 26's CLI || [ ] |

## Depends on

- **13–20, 23, 25** are shipped; **26–28** are the MVP work still to land. Nothing in 29 depends
  on their code, but 29g should be the last thing done, so the first deployment carries the
  whole MVP.
- **26** (`26-knowledge-base.md`) is a **consumer** of this step, not a dependency: its ingest
  CLI connects to the production database with `--connection` over an SSH tunnel, and its
  Decision 4 leaves "a one-off container in Coolify" to step 29. 29h documents the tunnel; it
  does not design the CLI. 26 also asserts that the knowledge-base table is created by "the
  API's `ApplyAllDatabaseChangesOnStartup` path" — which is exactly the thing 29b has to make
  true in production.
- **16** (`16-images.md`) for the blob store: `BlobOptions`, `S3BlobStore.CreateClient` and
  `BlobBucketInitializer`.
- **23** (`23-suggestions.md`) for the 196 MB of model weights the web image fetches at build
  time, and its "Where the weights come from" section, which this step does not revisit.
- **06** (`06-release-please.md`) for the version: release-please owns it, `scripts/sync-version.mjs`
  propagates it, and 29e reads it for the image tags.
- **CLAUDE.md**: CI builds `Release` with `-p:TreatWarningsAsErrors=True`, and `**.cs` /
  `**.csproj` trigger **both** workflows. 29b touches C#, so its PR must also satisfy
  `testWeb.yml` (schema regeneration, typecheck, vitest) even though it changes no API surface.

Invariant 10 — **no paid services in v1** — bounds this step. A VPS and a domain are the
unavoidable exceptions; everything else here is chosen to stay free (GitHub Actions and public
GHCR packages cost nothing on a public repo).

---

## What is there today, honestly

### `apps/TakeInitiative.Api/dockerfile`

Two stages, `sdk:10.0` → `aspnet:10.0`, built from the repo root with an allowlist
`dockerfile.dockerignore` so it can reach `packages/TakeInitiative.Dice`. What it gets right:
the restore layer is separated from the source copy, and `ASPNETCORE_HTTP_PORTS=8080` matches
`compose.dev.yml`. What has to change:

| Problem | Why it matters | Fix (29c) |
|---|---|---|
| Runs as **root** | A container escape is a root escape, and it is one line to avoid | `USER $APP_UID` — the `aspnet:10.0` image already defines a non-root `app` user and `APP_UID` |
| `WORKDIR ./app` | A relative workdir on top of `/`, which happens to work | `WORKDIR /app` |
| No RID on `dotnet publish` | `SkiaSharp.NativeAssets.Linux.NoDependencies` ships **13** `runtimes/linux-*` folders at ~12 MB each. A framework-dependent publish with no RID copies **all of them**: ~155 MB of native Skia, of which one file is ever loaded | `dotnet restore -a $TARGETARCH` and `dotnet publish -a $TARGETARCH`, which also makes cross-arch builds cheap (below) |
| `apt-get install curl` in the runtime stage | Only there for the compose healthcheck; adds a layer and an apt cache to a production image | Keep it, but `--no-install-recommends`, and put it **before** the `COPY --from=build` so the layer caches. The healthcheck is worth 10 MB; see "Decisions" for the chiseled alternative |
| No `HEALTHCHECK` in the image | The healthcheck lives in `compose.dev.yml`, so an image run any other way has none | `HEALTHCHECK` on `/healthz` in the Dockerfile; compose can still override it |
| No OCI labels | Nothing links the GHCR package to the repo | `docker/metadata-action` emits them in 29e; the Dockerfile needs nothing |
| No writable key directory | 29b persists the cookie keys to a volume, and a named volume mounted at a path the image does not own is root-owned and unwritable by a non-root user | `RUN mkdir -p /keys && chown $APP_UID:$APP_UID /keys` — Docker copies the image's ownership onto a fresh named volume |

`/healthz` already exists (`app.UseHealthChecks("/healthz")` with `AddHealthChecks()` and no
registered checks), so it answers 200 as soon as the host is listening. That is a liveness probe,
not a readiness probe, and that is what we want here: a probe that fails when Postgres blips
would have Coolify restart a perfectly healthy API.

### `apps/TakeInitiative.Web/Dockerfile`

Better shape already: `base` → `build` → `production`, a dedicated non-root `nuxtjs` user, and
only `.output` in the final stage. Two real problems.

**1. The API URL is baked in at build time.** `nuxt.config.ts` sets

```ts
runtimeConfig: { public: { axios: { baseURL: process.env.API_URL }, webUrl: process.env.WEB_URL } }
```

and `nuxt.config.ts` is evaluated **during `nuxt build`**, inside the image. `compose.dev.yml`
says so in as many words: *"nuxt.config.ts reads these at BUILD time, so they have to be build
args — setting them as container env vars does nothing."* For a prebuilt image that is the
classic trap: the image is welded to one domain, a staging deploy needs a second build, and
changing the API's hostname means a release.

It does not have to be that way, and the fix is two characters of defaulting. What was checked:

- Nitro overrides runtime config from the environment at **request** time
  (`nitropack/dist/runtime/internal/config.mjs` → `applyEnv`), with Nuxt's `NUXT_` prefix. The
  env name is `snakeCase(path).toUpperCase()`, which for `public.axios.baseURL` is exactly
  **`NUXT_PUBLIC_AXIOS_BASE_URL`**, and for `public.webUrl` **`NUXT_PUBLIC_WEB_URL`**
  (computed against the vendored `scule@1.3.0`, not guessed).
- `applyEnv` walks the *inlined* config object, so **a key that is not in the built config
  cannot be overridden**. `baseURL: process.env.API_URL` with `API_URL` unset is `undefined`,
  and an `undefined` does not reliably survive serialization into the build. So the config must
  carry a real default: `process.env.API_URL ?? ""`.
- `/app/**` is `ssr: false`, which is the part worth checking, because a client-only route could
  plausibly get its config from a file written at build time. It does not:
  `@nuxt/nitro-server/dist/runtime/utils/renderer/build-files.mjs` builds the SPA response with
  `const config = useRuntimeConfig(ssrContext.event)` and sets
  `ssrContext.config = { public: config.public, app: config.app }`, which
  `utils/renderer/payload.mjs` writes into the HTML as `window.__NUXT__.config`. **Per request.**
  So a container env var does reach the browser, including on the SPA routes.

So 29d changes `nuxt.config.ts` to default both to `""`, keeps `ARG API_URL` / `ARG WEB_URL` so
`compose.dev.yml` is untouched, and production sets `NUXT_PUBLIC_AXIOS_BASE_URL` and
`NUXT_PUBLIC_WEB_URL` as ordinary container environment variables. One image, any domain.

**2. The build context is the whole repo.** The web build uses the repo root as its context and
there is no `apps/TakeInitiative.Web/Dockerfile.dockerignore`, so BuildKit falls back to the root
`.dockerignore` — which excludes exactly two paths (`public/models/`, `.data/`). Everything else
goes up the wire: `.git` (15 MB), every `node_modules` (673 MB locally), every `bin/` and `obj/`.
29d adds `apps/TakeInitiative.Web/Dockerfile.dockerignore` with an allowlist, mirroring the API's.

> **Gotcha:** a `<dockerfile>.dockerignore` **replaces** the root `.dockerignore` for that build.
> The new file must therefore re-exclude `apps/TakeInitiative.Web/public/models/` itself, or a
> developer's local 196 MB of weights will be uploaded and then overwritten by the fetch layer.

Also missing: a health route. The web container has nothing to probe — `/` is a real page, but
probing it renders the landing page on every interval. 29d adds
`apps/TakeInitiative.Web/server/routes/healthz.get.ts` returning `{ ok: true }` and a
`HEALTHCHECK` using busybox `wget` (there is no `curl` in `node:24-alpine`, and adding one is not
worth a layer).

### The database schema in production

`Bootstrap.AddMartenDB(config)` ends with (the `IsDevelopment` parameter was its own only
use and went with the fix in 26c):

```csharp
if (IsDevelopment)
{
    martenOpts.ApplyAllDatabaseChangesOnStartup();
}
```

Its own comment explains the intent — *"rather than leaning on Marten's implicit auto-create,
which makes a fresh database's behaviour depend on which endpoint happens to be hit first"* —
which is a precise description of what production does today, because the API's container sets
no `ASPNETCORE_ENVIRONMENT` and therefore runs as **Production**.

Two consequences, one merely untidy and one a bug:

1. **Untidy:** document tables, indexes and the event store are created lazily, the first time
   each document type is touched. Whether the first request is fast, and whether a schema
   conflict surfaces as a startup failure or a 500 on someone's first note, depends on traffic.
2. **A bug:** `opts.Storage.ExtendedSchemaObjects.Add(new Extension("pg_trgm"))` and the same for
   `unaccent` are not attached to any document type, so nothing in a request path asks Marten to
   ensure them. `CREATE EXTENSION` never runs, and ⌘K's `similarity()` / `word_similarity()`
   calls fail at query time with *function does not exist*. The comment beside those lines
   assumes the opposite: *"A host that refuses fails startup loudly on `CREATE EXTENSION` rather
   than at the first search."* That is true in Development only. Step 26's knowledge-base table
   arrives through the same `ExtendedSchemaObjects` path and would be missing for the same reason.

**Prove it before fixing it** (29b, step 1): run the API with `ASPNETCORE_ENVIRONMENT=Production`
against a fresh database and search for a misspelled entry name. Whatever the result, the fix is
the same and covers both readings.

**The fix (29b):** apply all database changes on startup in *every* environment, behind
`Marten:ApplySchemaOnStartup`, default `true`. Dev behaviour is byte-for-byte what it is now.

Why that is safe here, specifically:

- **One API container, always.** `AddAsyncDaemon(DaemonMode.Solo)` already means exactly one
  process may run the projection daemon. The deployment must therefore never scale the API past
  one replica, which also removes the "two startups race on DDL" problem.
- **Weasel's `CreateOrUpdate` never drops.** A column or index is added; nothing is deleted. An
  older image started against a newer schema generally still runs.
- **DDL finishes before the port opens**, so a request never races a migration.
- Coolify's compose deploy is stop-then-start, not a rolling swap, so an old and a new container
  do not overlap on the schema.

Where this would stop being safe: more than one replica, a blue/green deploy, or a schema change
that is not additive (a renamed document, a changed projection). For those, the schema becomes
its own step — an `--apply-schema` flag on the API run as a Coolify pre-deploy command. That is
noted, not built: this step is one container and one owner.

### Cookie keys, and why every deploy would log everyone out

Auth is cookie-based (`AddCookieAuth`, `CookieAuth.SignInAsync`), so the ticket is encrypted with
ASP.NET Core **Data Protection**. Nothing in the repo calls `AddDataProtection()` or
`PersistKeysTo*` — `grep -rn "AddDataProtection\|PersistKeysTo\|DataProtection"` over `*.cs`
returns nothing. With no configuration, the key ring is written to
`$HOME/.aspnet/DataProtection-Keys` inside the container, which is thrown away with the
container. So **every redeploy invalidates every session**: everybody is signed out on every
release, which is a bad first impression for a group of friends who were mid-session.

29b adds, guarded so dev is unchanged:

```csharp
var keyPath = config.GetValue<string>("DataProtection:KeyPath");
if (!string.IsNullOrWhiteSpace(keyPath))
{
    services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
        .SetApplicationName("TakeInitiative");
}
```

`SetApplicationName` matters: the default is the content-root path, and pinning it means the
keys stay valid even if the container's working directory changes. Production sets
`DataProtection__KeyPath=/keys` and mounts a named volume there; 29c makes `/keys` writable by
the non-root user.

`JWTSigningKey` is bound to `JWTOptions` and **never read anywhere else** — it is a leftover from
the pre-cookie auth. Production should still set a long random value (it is `required` on the
options class and `appsettings.json` ships a dev string), but it protects nothing today. Do not
spend thought on it.

### Behind Traefik

Coolify puts Traefik in front and terminates TLS there, forwarding plain HTTP to the container.
Two things follow:

- `CookieSecurePolicy.Always` in non-development sets `Secure` on the cookie regardless of what
  the API thinks the scheme is, so sign-in works — but any code that reads `Request.Scheme` or
  the client IP sees `http` and Traefik's address. 29b adds `UseForwardedHeaders` for
  `XForwardedFor | XForwardedProto`, with `KnownNetworks`/`KnownProxies` cleared (the proxy is on
  a Docker bridge network whose address is not predictable). Do it as the *first* middleware.
- `AllowedHosts` must name the API's real host, and `CORS:MainApp` the web app's real origin, or
  the browser gets a CORS failure that looks like the API being down.

### Cookies across two hostnames

With `https://<web>` and `https://api.<web>` on the same registrable domain, `CookieDomain`
becomes `.<domain>` and `SameSite=Lax` (the cookie default) still works, because same-registrable-domain
requests are *same-site*. No `SameSite=None` is needed. Put the API on a genuinely different
domain and it breaks: cross-site XHR needs `SameSite=None; Secure`, and that is a code change
nobody needs. **Keep the API on a subdomain of the web app's domain.**

---

## GHCR, since the point is partly to learn it

The images are:

```
ghcr.io/pi-gorbo/takeinitiative-api
ghcr.io/pi-gorbo/takeinitiative-web
```

GHCR paths are always lowercase, so the owner `PI-Gorbo` becomes `pi-gorbo`. The image name is
free-form under the owner; it does not have to match the repo.

**Auth from Actions — no PAT.** A workflow job with

```yaml
permissions:
    contents: read
    packages: write
```

gets a `GITHUB_TOKEN` minted for that run, scoped to this repository and expiring with it. That
is all `docker/login-action` needs:

```yaml
- uses: docker/login-action@v3
  with:
      registry: ghcr.io
      username: ${{ github.actor }}
      password: ${{ secrets.GITHUB_TOKEN }}
```

**Auth from anywhere else — a PAT.** There is no `GITHUB_TOKEN` on your laptop or on the VPS, so
pulling a *private* package needs a personal access token with `read:packages` (classic tokens
are still the documented path for packages; fine-grained ones can do it but the scopes are named
differently):

```sh
echo "$GHCR_PAT" | docker login ghcr.io -u <github-username> --password-stdin
```

Push from outside Actions needs `write:packages`, and deleting versions needs `delete:packages`.
If the packages are public, none of this applies to pulling — which is one of the reasons to make
them public.

**Linking the package to the repo.** A package is linked by the
`org.opencontainers.image.source` label on the image, which `docker/metadata-action` fills in
from `github.repository`. Linking is what puts the repo on the package page, lets repository
collaborators inherit access, and lets the repo's own workflows push without per-package
permission grants. It is a label, not a setting — get the label right and the link appears.

**Tags.** 29e publishes, via `docker/metadata-action`:

| Tag | When | Moves? |
|---|---|---|
| `sha-<short>` | every build | never — this is the one to pin |
| `edge` | every push to `dev` | yes |
| `1.2.3` | a release-please release | never |
| `1.2` | a release | yes, within the minor |
| `latest` | a release | yes |

Production points at `latest` by default and re-pulls on a deploy; a rollback points at a
`sha-…` tag or a digest (see 29h). Tags are mutable and digests are not, so anything that must
be exact is written as `…@sha256:…`.

**Visibility: make the packages public.** Recommended, with reasons rather than a shrug:

1. The repo is already public. The API image contains compiled versions of code anyone can read,
   and after 29d the web image contains no environment-specific value at all — every URL, key
   and connection string is a runtime environment variable. Nothing secret is inside either
   image. (Check this again if that ever stops being true: an image layer is world-readable
   forever, and deleting the tag does not unpublish the layer.)
2. **Free.** Public packages do not count against a free account's package storage or data
   transfer. Private ones do, and the web image is ~400 MB because of the model weights: a
   handful of `sha-…` tags would exhaust a free account's package storage, and then pushes start
   failing in a way that looks like a workflow bug.
3. **One fewer secret on the VPS.** A public package means Coolify needs no registry credentials
   at all, so there is no PAT on the server to rotate or leak.
4. The model weights are redistributable: GLiNER small v2.5 is Apache-2.0 and
   `scripts/models/fetch-model.mjs` copies `LICENSE` and `NOTICE` in beside the files, so they
   ship inside the image where the licence requires them to be.

Accepted risk: anyone can `docker pull` and run the app. They get an empty database and none of
your data. That is the same exposure as the public source, which is already the case.

A package created by a workflow push starts **private** until you change it. That is a one-time
click per package: *repo → Packages → the package → Package settings → Change visibility →
Public*. Verify it with `docker logout ghcr.io && docker pull …` — an anonymous pull is the only
proof that matters.

**Housekeeping.** GHCR keeps every digest you push, and an `edge` retag leaves the previous
digest untagged but stored forever. After a few months of `dev` pushes that is real clutter.
Note for later, not for 29: a scheduled job using `actions/delete-package-versions` that keeps
the last N untagged versions.

---

## Architecture: arm64 (settled, 2026-10-01)

**The target is an Ubuntu box with an arm64 CPU** (the user, 2026-10-01). So:

- **`platforms: linux/arm64` only.** One platform, no manifest list, no QEMU, no cross-build.
- **`runs-on: ubuntu-24.04-arm`.** GitHub's arm64 runners are free for public repositories, and
  `PI-Gorbo/TakeInitiative` is public, so the build is native rather than emulated.
- **The dev machine is arm64 too** (Apple Silicon). That is a real piece of luck: what is built
  and run locally is the same architecture as production, so a local `docker run` is a genuine
  rehearsal rather than an emulated approximation. It also means 29d's measured **626 MB** web
  image is the production number, not a cross-arch guess.

Every pinned base image was checked for arm64 before settling this:

| Image | arm64 |
|---|---|
| `postgres:15-alpine` | yes (`linux/arm64/v8`) |
| `node:24-alpine` | yes (`linux/arm64/v8`) |
| `mcr.microsoft.com/dotnet/aspnet:10.0` | yes |
| `mcr.microsoft.com/dotnet/sdk:10.0` | yes |
| `pgsty/minio:RELEASE.2026-08-04T00-00-00Z` | yes — worth checking, since it is a community build and the one most likely to be amd64-only |

`SkiaSharp.NativeAssets.Linux.NoDependencies` ships **13** `linux-*` RID folders including
`linux-arm64`, so `dotnet publish -a arm64` keeps one of them. That is where the ~155 MB of
unused natives goes.

### If this ever needs to change

Both images are still written to cross-build, so adding amd64 later is a `platforms:` line
rather than a rewrite. But **do not reach for QEMU**: emulating a `dotnet publish` or a
`nuxt build` is 10–20× slower and will time the job out or bore you into disabling it. Both
images can cross-build cheaply instead, and 29c/29d write them that way from the start so the
option stays open:

- **API** — the .NET SDK cross-publishes natively. Pin the build stage to the *builder's*
  architecture and pass the target through:

  ```dockerfile
  FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
  ARG TARGETARCH
  RUN dotnet restore apps/TakeInitiative.Api/TakeInitiative.Api.csproj -a $TARGETARCH
  RUN dotnet publish apps/TakeInitiative.Api/TakeInitiative.Api.csproj -c Release -a $TARGETARCH --no-restore -o /app/out
  ```

  This is also the fix for the 155 MB of unused Skia natives: with a RID, only that RID's
  `runtimes/` folder is published.

- **Web** — `.output` is **architecture-independent**. Checked, because this only holds if no
  dependency is a native module: `onnxruntime-web` is WASM, and `@huggingface/tokenizers@0.2.0`
  is *"a pure JS/TS implementation"* with a single `dist/tokenizers.mjs` and no platform
  packages. There is no `sharp` (no `@nuxt/image`). So pinning the build stage to
  `--platform=$BUILDPLATFORM` and letting only the tiny runtime stage vary produces a real
  multi-arch manifest from one JavaScript build:

  ```dockerfile
  FROM --platform=$BUILDPLATFORM node:24-alpine AS build
  # …install, fetch weights, nuxt build…
  FROM node:24-alpine AS production      # per target platform
  COPY --from=build --chown=nuxtjs:nodejs /repo/apps/TakeInitiative.Web/.output /app
  ```

Since both the dev machine and the VPS are arm64, nothing here runs under emulation and no
`--platform` flag is needed anywhere.

---

## Image size

| Image | Roughly | What dominates |
|---|---|---|
| API, today | ~500 MB | `aspnet:10.0` (~220 MB) + publish output, of which **~155 MB is thirteen copies of libSkiaSharp** |
| API, after 29c | ~350 MB | one RID's Skia (~12 MB), `curl` (~10 MB) |
| Web | ~400 MB+ | `node:24-alpine` (~140 MB), **196 MB of GLiNER weights**, 14 MB of onnxruntime WASM, the app bundle |

The model is the web image, essentially. `docs/roadmap/23-suggestions.md` already made that
trade deliberately ("the cost is a web image a few hundred MB bigger, which is free on the
hosting the app uses") and this step does not reopen it — but the escape hatch is one build arg:
`--build-arg SUGGESTIONS_MODEL=skip` plus `NUXT_PUBLIC_SUGGESTIONS_BASE_URL` pointing at the
Hugging Face mirror. See Decisions.

---

## Configuration and secrets

Every value is set as a container environment variable. Two notes on form:

- ASP.NET reads `__` as the section separator, so `ConnectionStrings__TakeDB` is
  `ConnectionStrings:TakeDB`. `compose.dev.yml` uses both spellings; **production uses `__`
  throughout**, because `:` in a variable name upsets some `.env` parsers and web UIs.
- `ASPNETCORE_ENVIRONMENT` is deliberately **not set**. Unset means `Production`, which is what
  turns off Swagger, skips `appsettings.development.json`, and switches the cookie to
  `SecurePolicy.Always`.

### API

| Variable | Example | Secret | Set where |
|---|---|---|---|
| `ConnectionStrings__TakeDB` | `Host=<pg-host>;Port=5432;Database=takeinitiative;User ID=takeinitiative;Password=…;` | **yes** | Coolify env |
| `CORS__MainApp` | `https://takeinitiative.<domain>` | no | Coolify env |
| `CORS__AdminApp` | `https://takeinitiative.<domain>` | no | Coolify env — there is no admin app, but `Program.cs` throws if the key is absent |
| `AllowedHosts` | `api.takeinitiative.<domain>` | no | Coolify env |
| `CookieDomain` | `.takeinitiative.<domain>` | no | Coolify env |
| `TakeUrls__Web` | `https://takeinitiative.<domain>` | no | Coolify env — the link in confirmation emails |
| `Email__Domain` | `takeinitiative.<domain>` | no | Coolify env — becomes `no-reply@…` |
| `SendGrid__ApiKey` | `SG.…` | **yes** | Coolify env |
| `JWTSigningKey` | 64 random chars | **yes** | Coolify env — bound but unread; set it anyway |
| `Blobs__ServiceUrl` | `http://minio:9000` | no | `compose.prod.yml` (internal) |
| `Blobs__Bucket` | `takeinitiative` | no | `compose.prod.yml` |
| `Blobs__Region` | `us-east-1` (`auto` for R2) | no | `compose.prod.yml` |
| `Blobs__AccessKey` / `Blobs__SecretKey` | — | **yes** | Coolify env |
| `Blobs__ForcePathStyle` | `true` | no | `compose.prod.yml` |
| `Blobs__CreateBucket` | `true` for a self-hosted bucket, `false` for a managed one | no | `compose.prod.yml` |
| `Marten__ApplySchemaOnStartup` | `true` | no | `compose.prod.yml` (new in 29b) |
| `DataProtection__KeyPath` | `/keys` | no | `compose.prod.yml` (new in 29b) |
| `Images__*` | defaults | no | — `appsettings.json`'s 20 MB upload cap and sweeper timings are fine |

### Web

| Variable | Example | Secret |
|---|---|---|
| `NUXT_PUBLIC_AXIOS_BASE_URL` | `https://api.takeinitiative.<domain>` | no |
| `NUXT_PUBLIC_WEB_URL` | `https://takeinitiative.<domain>` | no |
| `NUXT_PUBLIC_SUGGESTIONS_BASE_URL` | unset — self-hosted from `/models/**` | no |
| `HOST`, `PORT`, `NODE_ENV` | already in the image | no |

### Postgres and the bucket

`POSTGRES_PASSWORD`, `POSTGRES_USER`, `POSTGRES_DB`; `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`.
All secret. The bucket credentials must match `Blobs__AccessKey` / `Blobs__SecretKey`.

### GitHub Actions secrets

| Secret | For |
|---|---|
| `COOLIFY_DEPLOY_URL` | the deploy webhook 29e calls on a release |
| `COOLIFY_TOKEN` | the bearer token for it |

No registry secret: `GITHUB_TOKEN` covers the push.

### Consumers outside the deployment

Step 26's knowledge-base CLI reads `KnowledgeBase:ConnectionString` (env
`KnowledgeBase__ConnectionString`, flag `--connection`) and, per its own plan, runs **from the
operator's machine against the production database over an SSH tunnel**. That means Postgres
must stay unpublished — no host port — and the tunnel is `ssh -L`. 29h writes down the two
commands. Nothing about the CLI is designed here.

---

## Coolify

Coolify is a UI, so this part is steps to take, not code to write. The one non-negotiable: the
resource must be given a compose file that has **no `build:` key**, so building is not merely
discouraged but impossible.

### Shape

- **Postgres: a Coolify-managed database resource** (pinned to **15**, matching `compose.dev.yml`
  and the Alba/Testcontainers fixtures). Reasons: its lifecycle is then separate from app
  redeploys, Coolify manages the volume, and it has scheduled backups in the UI — which is the
  whole argument, because otherwise the only copy of everyone's campaign lives on one disk with
  no backup. The compose resource must have **"Connect to predefined network"** enabled to reach
  it by its internal hostname.
- **API, web and the bucket: one "Docker Compose" resource** from this repo's `compose.prod.yml`.
  One resource means one deploy, one env list, and one place to look.
- **Git-backed, not pasted.** Point the resource at the public repo and at
  `compose.prod.yml`, so the committed file is the truth and a change is a commit. The
  "Coolify might build it" worry is answered by the file itself: nothing in it has a build
  context.

### By hand, in order

1. **The server.** A VPS with Docker, Coolify installed, and its own domain for the Coolify UI.
   Specs unknown to this plan — see Decisions. Rough need: 2 vCPU / 4 GB is comfortable for
   Postgres + MinIO + two small containers; 2 GB will be tight once Postgres has shared buffers
   and the async daemon is running. **Nothing on the VPS ever builds, so the usual "Coolify needs
   4 GB to build Nuxt" advice does not apply.**
2. **DNS.** `A` records for `takeinitiative.<domain>` and `api.takeinitiative.<domain>` → the
   VPS IP, both proxied off (Coolify issues the certificate itself).
3. **Project and environment.** One project, one `production` environment.
4. **Postgres.** Add a PostgreSQL 15 resource. Set a generated password. Note the internal
   hostname it shows. Turn on scheduled backups and point them somewhere off the box if a
   bucket is available.
5. **The compose resource.** New resource → *Docker Compose* → the public repo, branch `dev`,
   compose path `compose.prod.yml`.
6. **Domains per service.** The web service gets `https://takeinitiative.<domain>`, the API
   `https://api.takeinitiative.<domain>`. Coolify can take these from the UI per service, or from
   `SERVICE_FQDN_WEB_3000` / `SERVICE_FQDN_API_8080` magic variables in the compose file —
   the exact spelling drifts between Coolify versions, so set them in the UI first, confirm the
   generated Traefik labels, and only then move them into the file if you prefer. Let's Encrypt
   certificates are automatic once DNS resolves.
7. **Environment variables.** Paste the table above. Mark the secret ones as secret, and leave
   *"Is build variable?"* **off** for all of them — after 29d there is nothing the web image
   needs at build time, and a build variable on a resource that never builds is a trap for
   future-you.
8. **Persistent storage.** `takeminio-data` → `/data` on the bucket service, `takeapi-keys` →
   `/keys` on the API. If Postgres is in the compose instead, `takedb-data` →
   `/var/lib/postgresql/data`.
9. **Deploy.** Watch the log: it must show `Pulling from pi-gorbo/takeinitiative-api` and never
   `Step 1/…` or `exporting to image`.
10. **The webhook.** Copy the resource UUID, build
    `https://<coolify-host>/api/v1/deploy?uuid=<uuid>&force=true`, mint an API token in Coolify,
    and store both as the GitHub Actions secrets above. `force=true` is what makes Coolify
    re-pull a moving tag rather than reuse the local image.
11. **Only if the packages are private:** `docker login ghcr.io` on the VPS with a `read:packages`
    PAT, or add the registry under Coolify's registry/source settings. With public packages, skip
    this entirely.

### The object store in production

Default: the **same MinIO image as dev** (`pgsty/minio`, pinned), in `compose.prod.yml`, with a
volume and `Blobs__CreateBucket=true` so `BlobBucketInitializer` creates the bucket on a fresh
volume (it is idempotent and retries for 30 s while MinIO starts).

`compose.dev.yml` already records why the image is unusual: MinIO stopped publishing community
images in 2025 and its own registries no longer serve them anonymously, so `pgsty/minio` is a
community rebuild of the same server. Three consequences for production:

- **Pin the digest, not just the tag**, so a retag upstream cannot change what you run.
- **Mirror it into GHCR**, so the deployment depends on exactly one registry:
  `docker buildx imagetools create -t ghcr.io/pi-gorbo/minio:RELEASE.2026-08-04T00-00-00Z pgsty/minio@sha256:…`.
  This copies the manifest without a local pull, and is a nice use of the registry you are
  learning anyway.
- **Licence:** the MinIO server is AGPL-3.0. Running an unmodified copy to serve your own app
  triggers no obligation. Modify it and you owe source to its users. We modify nothing.

Alternatives, with the trade-offs, are in Decisions — including the one that removes the
single-disk durability problem entirely.

---

## `compose.prod.yml`

Committed in 29f. Shape (the real file gets the comments this repo's compose files carry):

```yaml
# Production. NOTHING HERE BUILDS: every image is pulled from GHCR, which is the
# whole point of docs/roadmap/29-deploy.md. If you ever add a `build:` key to this
# file you have moved the build onto the VPS.
#
# Coolify runs this file as a Docker Compose resource. Values in ${...} come from
# the resource's environment variables; docs/deploy/production.env.example lists them.
# No service publishes a host port: Traefik reaches them on the compose network,
# and Postgres and the bucket are not reachable from outside at all.

services:
    api:
        image: ${API_IMAGE:-ghcr.io/pi-gorbo/takeinitiative-api:latest}
        pull_policy: always
        restart: unless-stopped
        depends_on:
            minio:
                condition: service_healthy
        expose:
            - "8080"
        environment:
            - "ConnectionStrings__TakeDB=${TAKEDB_CONNECTION}"
            - "CORS__MainApp=${WEB_ORIGIN}"
            - "CORS__AdminApp=${WEB_ORIGIN}"
            - "AllowedHosts=${API_HOST}"
            - "CookieDomain=${COOKIE_DOMAIN}"
            - "TakeUrls__Web=${WEB_ORIGIN}"
            - "Email__Domain=${EMAIL_DOMAIN}"
            - "SendGrid__ApiKey=${SENDGRID_API_KEY}"
            - "JWTSigningKey=${JWT_SIGNING_KEY}"
            - "Marten__ApplySchemaOnStartup=true"
            - "DataProtection__KeyPath=/keys"
            - "Blobs__ServiceUrl=http://minio:9000"
            - "Blobs__Bucket=takeinitiative"
            - "Blobs__Region=us-east-1"
            - "Blobs__ForcePathStyle=true"
            - "Blobs__CreateBucket=true"
            - "Blobs__AccessKey=${BLOBS_ACCESS_KEY}"
            - "Blobs__SecretKey=${BLOBS_SECRET_KEY}"
        volumes:
            - takeapi-keys:/keys
        healthcheck:
            test: ["CMD-SHELL", "curl --fail http://localhost:8080/healthz || exit 1"]
            interval: 30s
            retries: 5
            start_period: 60s
            timeout: 10s

    web:
        image: ${WEB_IMAGE:-ghcr.io/pi-gorbo/takeinitiative-web:latest}
        pull_policy: always
        restart: unless-stopped
        depends_on:
            - api
        expose:
            - "3000"
        environment:
            # Read at RUNTIME (29d): one image serves any domain.
            - "NUXT_PUBLIC_AXIOS_BASE_URL=${API_ORIGIN}"
            - "NUXT_PUBLIC_WEB_URL=${WEB_ORIGIN}"
        healthcheck:
            test: ["CMD-SHELL", "wget -q --spider http://127.0.0.1:3000/healthz || exit 1"]
            interval: 30s
            retries: 5
            start_period: 30s
            timeout: 10s

    minio:
        # See docs/roadmap/29-deploy.md, "The object store in production": a community
        # rebuild, pinned by digest, mirrored into our own GHCR.
        image: ghcr.io/pi-gorbo/minio:RELEASE.2026-08-04T00-00-00Z@sha256:...
        command: server /data
        restart: unless-stopped
        environment:
            - "MINIO_ROOT_USER=${BLOBS_ACCESS_KEY}"
            - "MINIO_ROOT_PASSWORD=${BLOBS_SECRET_KEY}"
        volumes:
            - takeminio-data:/data
        healthcheck:
            test: ["CMD", "mc", "ready", "local"]
            interval: 10s
            retries: 5
            start_period: 10s
            timeout: 5s

volumes:
    takeapi-keys:
    takeminio-data:
```

Postgres is deliberately absent: it is a Coolify-managed resource, and `TAKEDB_CONNECTION`
points at it. 29f also commits `docs/deploy/production.env.example` listing every variable with
a comment and no real value.

Note the `${API_IMAGE}` / `${WEB_IMAGE}` shape rather than `:${IMAGE_TAG}`. A whole reference in
one variable means a rollback can be a **digest** (`…@sha256:…`), which a `:${TAG}` form cannot
express.

---

## `.github/workflows/images.yml`

`release.yml` today only opens the release PR and syncs the version onto its branch; the tag and
the GitHub Release appear when that PR merges, on the same workflow's next run. So there are two
sane triggers and one trap.

> **The trap.** A release or tag created by a workflow using the default `GITHUB_TOKEN` **does
> not trigger other workflows**. An `on: release` or `on: push: tags:` image workflow would
> silently never run. The fix is not a PAT: it is to make the image build a **job in the same
> run**, gated on release-please's `release_created` output. A reusable workflow keeps that from
> turning `release.yml` into a wall of YAML.

```yaml
name: Images

on:
    push:
        branches: [dev]
        paths:
            - ".github/workflows/images.yml"
            - "apps/TakeInitiative.Api/**"
            - "apps/TakeInitiative.Web/**"
            - "packages/**"
            - "scripts/models/**"
            - "Directory.Build.props"
            - "global.json"
            - "pnpm-lock.yaml"
    workflow_call:
        inputs:
            version:
                description: The release version, e.g. 1.0.15. Empty for an edge build.
                required: false
                type: string
                default: ""
    workflow_dispatch: ~

concurrency:
    group: images-${{ github.ref }}
    cancel-in-progress: true

permissions:
    contents: read
    packages: write

jobs:
    build:
        runs-on: ubuntu-latest
        strategy:
            fail-fast: false
            matrix:
                include:
                    - image: takeinitiative-api
                      dockerfile: apps/TakeInitiative.Api/dockerfile
                    - image: takeinitiative-web
                      dockerfile: apps/TakeInitiative.Web/Dockerfile
        steps:
            - uses: actions/checkout@v4
            - uses: docker/setup-buildx-action@v3
            - uses: docker/login-action@v3
              with:
                  registry: ghcr.io
                  username: ${{ github.actor }}
                  password: ${{ secrets.GITHUB_TOKEN }}
            - id: meta
              uses: docker/metadata-action@v5
              with:
                  images: ghcr.io/${{ github.repository_owner }}/${{ matrix.image }}
                  tags: |
                      type=sha,format=short,prefix=sha-
                      type=raw,value=edge,enable=${{ inputs.version == '' }}
                      type=semver,pattern={{version}},value=${{ inputs.version }}
                      type=semver,pattern={{major}}.{{minor}},value=${{ inputs.version }}
                      type=raw,value=latest,enable=${{ inputs.version != '' }}
            - uses: docker/build-push-action@v6
              with:
                  context: .
                  file: ${{ matrix.dockerfile }}
                  platforms: linux/amd64
                  push: true
                  tags: ${{ steps.meta.outputs.tags }}
                  labels: ${{ steps.meta.outputs.labels }}
                  cache-from: type=registry,ref=ghcr.io/${{ github.repository_owner }}/${{ matrix.image }}:buildcache
                  cache-to: type=registry,ref=ghcr.io/${{ github.repository_owner }}/${{ matrix.image }}:buildcache,mode=max
            - name: Record the digest
              run: echo "\`${{ matrix.image }}\` → \`${{ steps.meta.outputs.tags }}\`" >> "$GITHUB_STEP_SUMMARY"

    deploy:
        needs: build
        if: ${{ inputs.version != '' }}
        runs-on: ubuntu-latest
        steps:
            - name: Tell Coolify to pull
              env:
                  URL: ${{ secrets.COOLIFY_DEPLOY_URL }}
                  TOKEN: ${{ secrets.COOLIFY_TOKEN }}
              run: |
                  if [ -z "$URL" ]; then echo "No COOLIFY_DEPLOY_URL; skipping."; exit 0; fi
                  curl -fsS -X POST -H "Authorization: Bearer $TOKEN" "$URL"
```

Why **registry** cache rather than `type=gha`: GitHub's Actions cache is 10 GB per repository and
evicts least-recently-used. The web image's layers include 196 MB of model weights and a pnpm
store; a few builds would evict the .NET restore layer, and the cache becomes a coin flip. A
`:buildcache` tag in GHCR is free for a public package and never evicted. It does mean two extra
image tags on the package page, which is noise worth the determinism.

`release.yml` gains outputs and a calling job:

```yaml
jobs:
    release:
        outputs:
            release_created: ${{ steps.release.outputs.release_created }}
            version: ${{ steps.release.outputs.version }}
        # …existing steps unchanged…

    images:
        needs: release
        if: ${{ needs.release.outputs.release_created == 'true' }}
        permissions:
            contents: read
            packages: write
        uses: ./.github/workflows/images.yml
        with:
            version: ${{ needs.release.outputs.version }}
        secrets: inherit
```

`secrets: inherit` is what lets the reusable workflow see `COOLIFY_*`. Permissions must be
declared on the calling job; they are not inherited.

This workflow is separate from `testApi.yml` and `testWeb.yml` and does not duplicate them: the
gates run on the PR, the images are built after the merge. An image build failing does not make
`dev` red retroactively, which is the right split — but it does mean the Dockerfiles have no CI
coverage before a merge. Accepted: the `dev` push build is the first thing to look at after a
merge, and `workflow_dispatch` lets a Dockerfile PR be tested on its own branch.

---

## Files touched

Paths from the repo root.

**29a (docs)**
- `docs/roadmap/29-deploy.md`: this file
- `docs/roadmap/README.md`: step 29's status

**29b**
- `apps/TakeInitiative.Api/src/boostrap/Bootstrap.cs`: `ApplySchemaOnStartup` in every
  environment behind config; `AddDataProtection().PersistKeysToFileSystem(...)` when
  `DataProtection:KeyPath` is set
- `apps/TakeInitiative.Api/Program.cs`: `UseForwardedHeaders` first in the pipeline
- `apps/TakeInitiative.Api/appsettings.json`: `Marten:ApplySchemaOnStartup` and
  `DataProtection:KeyPath` with dev defaults that preserve today's behaviour
- `apps/TakeInitiative.Api/src/boostrap/Options/`: a small options class if the flags earn one
- `apps/TakeInitiative.Api.Tests/`: a test that the flag is honoured and that the fixtures still
  get their schema (`Scopes/Integration/*Fixture.cs` used to call `AddMartenDB(..., IsDevelopment: true)`
  and now rely on `Marten:ApplySchemaOnStartup` defaulting to true, which `SchemaOnStartupTests` pins
  and must keep working unchanged)
- `README.md`: the "Resetting the database" note now says the schema is applied on startup
  everywhere unless the flag is off

**29c**
- `apps/TakeInitiative.Api/dockerfile`, `apps/TakeInitiative.Api/dockerfile.dockerignore`

**29d**
- `apps/TakeInitiative.Web/nuxt.config.ts`: `?? ""` on `axios.baseURL` and `webUrl`
- `apps/TakeInitiative.Web/Dockerfile`; **new** `apps/TakeInitiative.Web/Dockerfile.dockerignore`
- **new** `apps/TakeInitiative.Web/server/routes/healthz.get.ts`
- `compose.dev.yml`: a healthcheck on `web`; the build args stay
- `apps/TakeInitiative.Web/TEMPLATE.env`, `README.md`: note that the two variables are
  build-time for dev and `NUXT_PUBLIC_*` at runtime for a container

**29e**
- **new** `.github/workflows/images.yml`; `.github/workflows/release.yml` (outputs + `images` job)

**29f**
- **new** `compose.prod.yml`; **new** `docs/deploy/production.env.example`; `README.md` gains a
  "Production" pointer

**29g**
- **new** `docs/deploy/coolify.md`

**29h**
- **new** `docs/deploy/operations.md` (rollback, backup, restore, the KB ingest tunnel)

## Steps

### 0. Start the stack (29a)

```sh
git switch -c v2/29a-deploy-plan origin/dev
```

Each later sub-step branches from the one before (`gh stack` hangs in this repo; use
`git push -u` and `gh pr create --base <previous branch>`).

### 29b. The API, ready for production

> **The schema half of this sub-step moved to 26c.** This plan found that
> `ApplyAllDatabaseChangesOnStartup()` is guarded by `if (IsDevelopment)`
> (`Bootstrap.cs:136`) and that the API container sets no `ASPNETCORE_ENVIRONMENT`, so
> `pg_trgm` and `unaccent` are never created in production and ⌘K's `word_similarity()`
> would fail there. That is a live bug in step 17, and step 26 needs the same path to create
> its knowledge-base table, so `Marten:ApplySchemaOnStartup` and its before-and-after
> measurement belong in **26c** — the step that adds a schema object should be the step that
> makes schema objects get created. See 26's "The schema is not applied in production".
> By the time 29 runs, assume that fix has landed and **verify** it rather than repeating it.

1. **Confirm 26c's fix holds in a production-shaped run.** With
   `ASPNETCORE_ENVIRONMENT=Production` against a freshly reset database
   (`docker compose -p takeinitiative -f compose.dev.yml down -v && up -d postgres minio`),
   check `select extname from pg_extension` lists `pg_trgm` and `unaccent`, and that the
   knowledge-base table exists. If it does not, stop: 26c regressed, and nothing below is
   worth doing until it is fixed there.
2. `DataProtection:KeyPath`: when set, persist the key ring there and `SetApplicationName("TakeInitiative")`.
   When unset (dev, tests), change nothing.
3. `UseForwardedHeaders` for `XForwardedFor | XForwardedProto`, `KnownNetworks` and `KnownProxies`
   cleared, as the first middleware. Comment why the lists are empty (the proxy is on a Docker
   network with no fixed address) and that the API is not reachable except through Traefik.
4. Tests: the data-protection path with the setting present and absent, and the forwarded-headers
   middleware; the integration fixtures are unaffected.

Verify: `dotnet build --configuration Release -p:TreatWarningsAsErrors=True` reports
`0 Warning(s), 0 Error(s)`; `dotnet test` green; `pnpm dev` behaves exactly as before; the
Production run from step 1 now creates both extensions and every table before the port opens.

### 29c. The API image

1. `WORKDIR /app`. `FROM --platform=$BUILDPLATFORM …sdk:10.0 AS build`, `ARG TARGETARCH`,
   `-a $TARGETARCH` on both `restore` and `publish`.
2. Runtime stage: `apt-get install -y --no-install-recommends curl` before the `COPY --from=build`,
   `mkdir -p /keys && chown $APP_UID:$APP_UID /keys`, then `USER $APP_UID`.
3. `HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=5 CMD curl -fsS http://localhost:8080/healthz || exit 1`.
4. Keep `ASPNETCORE_HTTP_PORTS=8080` and `EXPOSE 8080` — `compose.dev.yml` maps `7402:8080`.
5. `dockerfile.dockerignore`: add `!global.json` if the RID-specific restore needs it, and
   re-check the allowlist still covers everything the build reads.

Verify: `docker build -f apps/TakeInitiative.Api/dockerfile -t ti-api:local .` on the Mac;
`docker image ls` shows the size dropped by roughly the twelve unused Skia copies;
`docker run --rm ti-api:local id` prints a non-zero uid; the full
`docker compose -p takeinitiative -f compose.dev.yml up -d --build` still comes up healthy.

### 29d. The web image, and the runtime API URL

1. `nuxt.config.ts`: `baseURL: process.env.API_URL ?? ""` and `webUrl: process.env.WEB_URL ?? ""`.
   Comment that the empty default is what keeps the key in the built config so
   `NUXT_PUBLIC_AXIOS_BASE_URL` can replace it at request time, and that `/app/**` being
   `ssr: false` does not change this because the SPA shell is rendered per request.
2. `server/routes/healthz.get.ts`: `export default defineEventHandler(() => ({ ok: true }))`.
3. `Dockerfile`: `FROM --platform=$BUILDPLATFORM node:24-alpine AS build`; keep `ARG API_URL` /
   `ARG WEB_URL` for `compose.dev.yml`; add `HEALTHCHECK … wget -q --spider http://127.0.0.1:3000/healthz`.
   Keep the model layer and `SUGGESTIONS_MODEL=skip` exactly as step 23 left them.
4. **New** `Dockerfile.dockerignore`, allowlist style, and it must itself exclude
   `apps/TakeInitiative.Web/public/models/` and `.data/` — the root `.dockerignore` no longer
   applies to this build once the file exists.
5. `compose.dev.yml`: a healthcheck on `web`.

Verify: `docker build -f apps/TakeInitiative.Web/Dockerfile -t ti-web:local .`; the build log's
context upload is megabytes, not hundreds of megabytes; then

```sh
docker run --rm -p 3100:3000 -e NUXT_PUBLIC_AXIOS_BASE_URL=https://example.invalid ti-web:local
curl -s localhost:3100/app | grep -o 'https://example.invalid'
```

finds it in `window.__NUXT__.config` — that grep is the whole point of the sub-step. Then
`pnpm --filter @ti/web exec nuxi typecheck` and `pnpm --filter @ti/web test`.

### 29e. Publish to GHCR

1. Add `images.yml` as above. Start with `push: false` on the build step, merge, watch a `dev`
   push build both images, then flip to `push: true` in a follow-up commit — a failed build is
   cheaper to debug than a failed push.
2. First successful push: open each package, set visibility to **Public**, confirm the repo link
   appears (the `org.opencontainers.image.source` label), then `docker logout ghcr.io` and pull
   both anonymously.
3. `release.yml`: outputs on the `release` job, and the `images` job calling the reusable
   workflow when `release_created`. Leave the `deploy` job's secrets unset for now; it skips.
4. Note the digests in the run summary.

Verify: a `dev` push produces `edge` and `sha-<short>` on both packages; `workflow_dispatch`
works; a `docker pull ghcr.io/pi-gorbo/takeinitiative-api:edge` on the Mac needs
`--platform linux/amd64` and runs (slowly). The second build is visibly faster than the first —
that is the `:buildcache` tag working.

### 29f. The production spec

1. `compose.prod.yml` as above. No `ports:`, no `build:`, `pull_policy: always` on both app
   services.
2. `docs/deploy/production.env.example`: every variable from the tables, each with a one-line
   comment, secrets as `CHANGE_ME`.
3. Mirror the pinned MinIO image into GHCR and put the digest in the file.
4. `README.md`: a short "Production" section pointing at `docs/deploy/`.

Verify: `docker compose -f compose.prod.yml --env-file .env.prod config` renders with no
warnings and no `build` section; `grep -n "build:" compose.prod.yml` finds nothing. Then a full
local dry run on the Mac: fill `.env.prod` with localhost values, add a throwaway Postgres, and
`docker compose -f compose.prod.yml up -d` — both app containers reach healthy from **pulled**
images, and sign-up works.

### 29g. Coolify, and the first deploy

Follow "By hand, in order" above, writing each screen down in `docs/deploy/coolify.md` as you go
— including the things that did not work, because that is the part nobody remembers a year
later. Then run this step's **Verify** end to end.

### 29h. Rollback, backups, and the ingest tunnel

1. **Rollback**, rehearsed not theorised: set `API_IMAGE` (or `WEB_IMAGE`) to the previous
   release's digest, redeploy, confirm the app still works, then go back. Write down where to
   find a digest: the Actions run summary, `docker image inspect` on the VPS, or the package's
   versions page.
2. **Backups.** `pg_dump` over the SSH tunnel to a file on your machine; `mc mirror` (or
   `rclone sync`) for the bucket. Both as copy-pasteable commands. Then **restore one** into a
   local Postgres 15 and start the API against it — a backup nobody has restored is not a backup.
3. **The knowledge-base ingest tunnel** (step 26's consumer):

   ```sh
   ssh -N -L 55432:<pg-internal-host>:5432 <user>@<vps>
   dotnet run --project apps/TakeInitiative.KnowledgeBase.Cli -- ingest \
       --from ~/5etools-src/data \
       --connection "Host=localhost;Port=55432;Database=takeinitiative;User ID=…;Password=…"
   ```

   With the note 26 asks for: run `--dry-run` first, and the API must have started against that
   database at least once, because the API owns the schema.
4. **What is not backed up**, said plainly: nothing on the VPS except what these commands copy.

## Verify

Run in order. Every item is checkable; the last one is the point of the step.

1. **Gates, on every PR in the stack** (from the repo root, the real CI commands):

   ```sh
   dotnet restore
   dotnet build --configuration Release --no-restore -p:TreatWarningsAsErrors=True   # 0 Warning(s), 0 Error(s)
   dotnet test -- --verbosity normal

   pnpm install --frozen-lockfile
   pnpm turbo run gen:api --filter=@ti/web
   git diff --exit-code -- apps/TakeInitiative.Web/utils/api/schema.d.ts
   pnpm --filter @ti/web exec nuxi typecheck
   pnpm --filter @ti/web test
   ```

   29b touches `.cs`, so it triggers **both** workflows; the schema must be unchanged, not just
   regenerated.

2. **Dev is unharmed.** `pnpm dev` from a clean clone; then
   `docker compose -p takeinitiative -f compose.dev.yml up -d --build` and all four services
   healthy.

3. **The images are real.** On the Mac:
   `docker pull --platform linux/amd64 ghcr.io/pi-gorbo/takeinitiative-web:latest`, run it with
   `NUXT_PUBLIC_AXIOS_BASE_URL` pointing at a local API, and sign in. Same image, different URL,
   no rebuild.

4. **Anonymous pull.** `docker logout ghcr.io` then pull both `:latest`. If this fails, the
   packages are still private.

5. **Pulled, not built.** On the VPS:

   ```sh
   docker image inspect ghcr.io/pi-gorbo/takeinitiative-api:latest --format '{{.RepoDigests}}'
   docker image inspect ghcr.io/pi-gorbo/takeinitiative-web:latest --format '{{.RepoDigests}}'
   docker buildx du
   ```

   Both print `ghcr.io/…@sha256:…` (a locally built image has an empty `RepoDigests`), the
   digests match the Actions run summary, and `buildx du` shows an empty cache. Coolify's deploy
   log contains `Pulling from` and no build output. `grep -rn "build:" compose.prod.yml` is empty.

6. **TLS and the domains.** `https://takeinitiative.<domain>` and
   `https://api.takeinitiative.<domain>/healthz` both answer over a valid certificate;
   `http://` redirects.

7. **Schema.** On the production database: `select extname from pg_extension;` includes `pg_trgm`
   and `unaccent`; `\dt` lists `mt_doc_campaign`, `mt_doc_entry`, `mt_events` and the rest; and
   the entry table has the generated `tsvector` column step 17 added. A ⌘K search for a
   misspelled entry name returns the entry.

8. **Sessions survive a deploy.** Sign in, redeploy from Coolify, reload: **still signed in.**
   That is the data-protection volume doing its job, and it is the single easiest thing to get
   wrong.

9. **Rollback.** Point `WEB_IMAGE` at the previous digest, redeploy, confirm the app works, put
   it back. Note how long it took.

10. **A friend, on their phone.** Send the URL to somebody who has never seen the app:
    1. They sign up and land in the app.
    2. They join the campaign with the join code.
    3. They post a note in the current session with an `@mention` and a photo from the camera
       roll, and the photo renders (the API streamed it out of the bucket — there are no
       presigned URLs).
    4. The owner, on a laptop, sees the note appear without refreshing (SignalR through Traefik).
    5. They add the app to their home screen and it opens standalone; sharing a photo to it from
       the share sheet lands in the composer.
    6. "✨ Find suggestions" on their own note downloads the model from the app's own origin and
       offers a suggestion.
11. **Emails.** A password reset arrives, from `no-reply@<Email__Domain>`, with a link on the
    real web domain. (Sign-up itself does not depend on this: `PostSignUp` logs a send failure
    and signs the user in anyway.)
12. **A backup, restored.** As 29h.2.

## Notes / gotchas

- **The build-time-vs-runtime trap, resolved.** `nuxt.config.ts` runs during `nuxt build`, so
  `process.env.API_URL` is baked into the image — `compose.dev.yml` says as much. But Nitro
  re-applies the environment to `runtimeConfig` on **every request**
  (`nitropack/.../internal/utils.env.mjs`, `applyEnv`), and the SPA renderer serializes
  `useRuntimeConfig(event).public` into `window.__NUXT__.config` per request
  (`@nuxt/nitro-server/.../renderer/build-files.mjs`), so `ssr: false` on `/app/**` does **not**
  break it. Two conditions: the key must exist in the built config (hence `?? ""`), and the env
  name must be the `scule` snake-case of the path — `public.axios.baseURL` →
  `NUXT_PUBLIC_AXIOS_BASE_URL`, `public.webUrl` → `NUXT_PUBLIC_WEB_URL`. Both computed against
  the vendored `scule@1.3.0`, not guessed.
- **`ApplyAllDatabaseChangesOnStartup()` is Development-only today**, and the container runs as
  Production because `ASPNETCORE_ENVIRONMENT` is never set. `Storage.ExtendedSchemaObjects`
  (`pg_trgm`, `unaccent`, and step 26's knowledge-base table) hang off no document type, so
  lazy auto-create has no reason to touch them. Measure it (29b.1), then fix it for every
  environment.
- **`DaemonMode.Solo` means one API container, forever.** Do not scale the API in Coolify. If it
  is ever scaled, the daemon mode and the startup DDL both have to change first.
- **Data protection keys are ephemeral without a volume**, so every deploy signs everyone out.
  A named volume mounted at a path the image does not own is root-owned and unwritable by
  `USER $APP_UID`; `mkdir` + `chown` it in the Dockerfile so a fresh volume inherits the
  ownership.
- **~155 MB of the API image is unused SkiaSharp.** `SkiaSharp.NativeAssets.Linux.NoDependencies`
  4.152.1 ships 13 `runtimes/linux-*` folders at ~12 MB each, and a publish with no RID copies
  them all. `-a $TARGETARCH` keeps one.
- **arm64 vs amd64.** The dev machine is arm64, GitHub's default runner is amd64, and the VPS is
  unknown. Default `linux/amd64`. If both are ever needed, cross-build — the .NET SDK does it
  with `-a $TARGETARCH`, and the web `.output` is architecture-independent (checked:
  `@huggingface/tokenizers` is pure JS, `onnxruntime-web` is WASM, no `sharp`), so pinning the
  build stages to `$BUILDPLATFORM` gives a multi-arch manifest without QEMU. Never build these
  under QEMU.
- **A `<dockerfile>.dockerignore` replaces the root `.dockerignore`** for that build. Adding
  `apps/TakeInitiative.Web/Dockerfile.dockerignore` therefore has to re-exclude
  `public/models/`, or 196 MB of local weights join the build context.
- **The web build context is the whole repo today** — `.git`, every `node_modules`, every
  `bin`/`obj`. That is slow and it busts the cache on unrelated changes.
- **A release created with `GITHUB_TOKEN` does not trigger `on: release`.** Hence the reusable
  workflow called from `release.yml`, gated on `release_created`. A PAT would also work and is
  worse: another secret, another rotation.
- **`secrets: inherit` and explicit `permissions:`** are both required on a job that calls a
  reusable workflow. Permissions are not inherited.
- **GHA cache is 10 GB and evicts; a `:buildcache` tag in GHCR does not.** With a 196 MB model
  layer and a pnpm store in the mix, use the registry cache.
- **MinIO in production is the awkward part.** Community images stopped being published, so
  `compose.dev.yml` uses `pgsty/minio`, a community rebuild; recent community builds also
  stripped most of the web console. Pin the digest, mirror it into GHCR so production depends on
  one registry, and remember the server is AGPL-3.0 (running an unmodified copy for your own app
  is fine). `Blobs__ForcePathStyle=true` and the `WHEN_REQUIRED` checksum settings in
  `S3BlobStore.CreateClient` are already what non-AWS stores need — do not "fix" them.
- **`Blobs__CreateBucket` must be `true` on a fresh self-hosted volume**, or the bucket never
  exists and every upload fails with an error that looks like a credentials problem.
  `BlobBucketInitializer` retries for 30 s and then only logs, so the API starts healthy either
  way — the failure surfaces at the first photo.
- **There are no presigned URLs.** `GetImageVariant` streams every image through the API after a
  visibility check, so the bucket must **not** be publicly reachable and needs no CORS
  configuration at all.
- **Postgres 15, not latest.** `compose.dev.yml` records that `postgres:latest` is 18+, which
  moved its data directory. Pin 15 in production too, or a dump taken from dev will not restore.
- **Keep the API on a subdomain of the web app's domain** so `CookieDomain=.<domain>` works with
  the default `SameSite=Lax`. A different domain needs `SameSite=None`, which is a code change.
- **`CORS:AdminApp` must be set even though there is no admin app**: `Program.cs` throws on the
  missing key.
- **`JWTSigningKey` is bound and never read.** Set a random value; expect nothing from it.
- **The web Dockerfile copies only four `package.json` files** (root, api, api.tests, web) but
  the lockfile has six importers. It works because
  `packages/TakeInitiative.Dice{,.Tests}` are `{}` — empty importers. The moment a workspace
  package gains a real dependency (step 26 adds `packages/TakeInitiative.KnowledgeBase.Tests`),
  `pnpm install --frozen-lockfile` in the image can start failing. If it does, copy that
  `package.json` too.
- **Image uploads are capped at 20 MB** (`Images:MaxUploadBytes`). Traefik imposes no body limit
  by default, but if a large photo fails with a 413 the middleware is where to look, not the API.
- **Not in 29:** more than one API replica, blue/green or zero-downtime deploys, a separate
  migration step, uptime monitoring or alerting, log shipping, a staging environment, a CDN, and
  running step 26's ingest inside the deployment (26's Decision 4 leaves that here, and the
  answer for now is "from your machine, over a tunnel").

### Decisions for the user

Each has the default this plan assumes.

1. **The VPS: which provider, how big, and what architecture.** *Default assumed: an amd64 VPS
   with 2 vCPU and 4 GB.* Unknown to this plan, and it decides 29e's `platforms:` line. If it is
   arm64 (Hetzner CAX, Oracle Ampere), say so and 29e becomes `runs-on: ubuntu-24.04-arm` with
   `platforms: linux/arm64`. Because nothing builds on the box, 2 GB is workable but leaves
   little headroom once Postgres and MinIO are both resident.
2. **The domain, and the two hostnames.** *Default assumed: `takeinitiative.<your-domain>` for
   the web app and `api.takeinitiative.<your-domain>` for the API, with
   `CookieDomain=.takeinitiative.<your-domain>`.* Unknown to this plan. The alternative — one
   hostname with the API on `/api` and `/campaignHub` — removes CORS and the cookie-domain
   question entirely and would let `NUXT_PUBLIC_AXIOS_BASE_URL` be empty, but it needs
   hand-written Traefik path rules for two prefixes instead of two tick-boxes.
3. **Package visibility.** *Default: **public**.* Free (a private ~400 MB web image would eat a
   free account's package storage), no registry credentials on the VPS, and nothing secret is in
   either image once 29d lands. Say so if you would rather keep them private, and 29g gains the
   `docker login` step on the server.
4. **Where the bucket lives.** *Default: MinIO in `compose.prod.yml`, pinned and mirrored into
   GHCR.* It matches dev exactly and needs no third-party account, which keeps invariant 10
   literal. The alternative worth considering: **Cloudflare R2** — a free tier, no ops, and your
   friends' photos stop living on a single unbacked VPS disk; the cost is a Cloudflare account
   with a payment method on file, which brushes against "no paid services". Garage and SeaweedFS
   are the self-hosted alternatives if `pgsty/minio` ever becomes unusable; both need a config
   file, which MinIO does not.
5. **Postgres: Coolify-managed or in the compose file.** *Default: Coolify-managed, pinned to 15.*
   Its lifecycle is then independent of app redeploys and it gets Coolify's scheduled backups,
   which is the whole reason. The alternative — Postgres as a service in `compose.prod.yml` —
   makes the committed file the entire deployment and portable to any Docker host, at the cost of
   rolling your own backups (which 29h writes down anyway).
6. **The compose resource's source: git or pasted.** *Default: git-backed, pointing at this repo
   and `compose.prod.yml`.* One source of truth, and a change is a commit. Pasting the file into
   a Coolify "empty compose" resource avoids giving Coolify repo access at all, but then the
   committed file and the running file drift.
7. **When images get built.** *Default: every push to `dev` (`edge` + `sha-…`) and every release
   (semver + `latest`), with a paths filter so a docs-only push builds nothing.* Actions minutes
   are free on a public repo and an `edge` image is something you can actually test. If the
   ~5-minute web build on every merge annoys you, drop the `push:` trigger and keep
   `workflow_dispatch`.
8. **What production points at.** *Default: `API_IMAGE`/`WEB_IMAGE` unset, so the compose file's
   `:latest` applies, and the release workflow pings Coolify to re-pull.* Rollback and pinning
   are then done by setting those variables to a digest. The alternative is pinning the exact
   version in Coolify on every release — more deliberate, one more manual step per release.
9. **The model weights in the web image.** *Default: keep them (196 MB), as step 23 decided.*
   `SUGGESTIONS_MODEL=skip` plus `NUXT_PUBLIC_SUGGESTIONS_BASE_URL=<the Hugging Face mirror>`
   makes the image ~200 MB smaller and moves the download to a third party who then sees your
   users' requests — which is exactly the trade 23 declined.
10. **SendGrid.** *Default: a free SendGrid account with a verified sender on your domain.*
    Unknown whether one exists. Without it, password reset and email confirmation do nothing
    (sign-up still works — the send failure is logged and the user is signed in). Its free tier
    is the only reason this does not break invariant 10; if it has become paid, the alternatives
    are another provider behind the same `IEmailSender`, or accepting no email for the MVP.
11. **The API's chiseled base image.** *Default: stay on `aspnet:10.0` (Debian) with `curl`.*
    `aspnet:10.0-noble-chiseled` is ~100 MB smaller and non-root by default, but has no shell, so
    the container healthcheck has to move out to Coolify, and SkiaSharp's behaviour there is
    unverified. Worth a look once the deployment is boring.
12. **Uptime monitoring.** *Default: none in 29.* A free Uptime Kuma next to Coolify, or a
    third-party ping on `/healthz`, is a small follow-up — but "my friend says it's down" is a
    perfectly good monitor for four people.
