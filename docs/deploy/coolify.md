# Coolify: the first deployment, by hand

> ## ⚠️ Sections 5 and 11 are superseded
>
> This guide builds the deployment as one git-backed Docker Compose resource. It is now **two
> Docker Image Applications** (api, web) beside Coolify's Postgres and Garage resources, because
> `deploy.yml` pins `docker_registry_image_name`/`_tag` and those exist on an Application.
> **`docs/deploy/pipeline.md` section 1 is the authority**, and lists what moved where.
>
> | Section | Now |
> |---|---|
> | **5. The compose resource** | Replaced by two Applications |
> | 6. Environment variables | Right list, set per Application. `Blobs__ServiceUrl` is Garage's resource hostname |
> | 7. Domains and TLS | Per Application |
> | 8. Persistent storage | Coolify "Persistent Storage" entries, not compose volumes |
> | **11. The deploy webhook** | Replaced by the Coolify API. `COOLIFY_BASE_URL` + `COOLIFY_API_TOKEN` |
>
> Everything else holds, including all eight failure modes in section 12 — they are about the app
> and the proxy. The by-hand path is kept deliberately: it is the retreat if the pipeline breaks.

This is the walkthrough for standing TakeInitiative up on a Coolify server for the first time. It
assumes you have never used Coolify before and that you know your way around a Linux box, Docker
and DNS. Follow it top to bottom; it is written to be done once, in one sitting, without going
back and forth.

**The one rule.** Coolify **pulls** images from GHCR and runs them. It never builds. GitHub
Actions already built them on a free runner (`.github/workflows/images.yml`), and an Application
whose source is a docker image has nothing to build from, so building is not merely discouraged — it
is impossible. Step 9 below is how you prove it, and that proof is the point of the whole approach.

**What is settled before you start**, so you do not have to decide it here:

| | |
|---|---|
| Target | an Ubuntu box, **x86_64** |
| Images | `linux/amd64` only, built on `ubuntu-latest` |
| Registry | GHCR, **public packages** — so the server needs no registry credentials |
| Postgres | **Coolify-managed, pinned to 16** — it is deliberately not in `compose.prod.yml` |
| Object store | Garage, its own Coolify resource, on a volume |
| API replicas | **exactly one, forever** (see step 12) |

> **A note on Coolify versions.** Coolify's UI moves between releases. Where a field name or a
> tab might have been renamed, this guide says *what you are looking for* and gives the name it
> had in the versions it was written against, rather than a click path that will be wrong in six
> months. Anything marked **unverified** is something to confirm on your own instance rather than
> to trust. If a label here does not exist, search the resource's settings for the concept — the
> concepts have been stable even when the words have not.

---

## 0. Before you start

### Have these in hand

- [ ] **A domain**, with access to its DNS. You will create two `A` records.
- [ ] **A VPS** with a public IP, Ubuntu, x86_64, and Docker. Coolify's own installer puts Docker
      on for you. Rough sizing: **2 vCPU / 4 GB is comfortable** for Postgres + Garage + two small
      containers. 2 GB works but leaves little headroom once Postgres has its shared buffers and
      the Marten async daemon is running. The usual *"Coolify needs 4 GB to build Nuxt"* advice
      does **not** apply, because nothing on this box ever builds.
- [ ] **Coolify installed**, reachable over HTTPS on its own hostname, and an admin login.
- [ ] **A SendGrid API key** from an account with a sender verified on your sending domain. Without
      it, password reset and email confirmation silently do nothing. Sign-up still works:
      `PostSignUp` logs the send failure and signs the user in anyway.
- [ ] **The images, public on GHCR.** Confirm before you touch Coolify:

      ```sh
      docker logout ghcr.io
      docker pull ghcr.io/pi-gorbo/takeinitiative-api:latest
      docker pull ghcr.io/pi-gorbo/takeinitiative-web:latest
      ```

      An anonymous pull is the only proof that matters. If either fails with
      `denied`/`unauthorized`, the package is still private: open
      *repo → Packages → the package → Package settings → Change visibility → Public*. A package
      created by a workflow push starts private; this is a one-time click per package.

- [ ] **A password manager or a scratch file** for the values you generate in step 6. You will
      generate five secrets and you will need some of them again.

### The two hostnames

Pick them now, because almost every environment variable is derived from them.

| | Default this guide uses | Why |
|---|---|---|
| Web app | `https://takeinitiative.samstack.org` | |
| API | `https://api-takeinitiative.samstack.org` | |
| Cookie domain | `.samstack.org` | the leading dot is what makes one auth cookie valid on both |

**Keep both hostnames on one registrable domain.** `SameSite=Lax` then treats requests between
them as same-site. A genuinely different domain needs `SameSite=None; Secure`, which is a code
change nobody needs.

> **Behind Cloudflare, the API cannot be a second-level subdomain.** Cloudflare's free Universal SSL
> certificate covers `example.org` and `*.example.org` — one label, no more. So
> `api.takeinitiative.example.org` has no certificate and fails the TLS handshake before any HTTP
> happens; `curl` reports `sslv3 alert handshake failure` and a browser shows a privacy warning.
> Hence `api-takeinitiative`, a sibling of the web host rather than a child. Measured 2026-10-09.
>
> **That changes `COOKIE_DOMAIN`.** Siblings share only the registrable domain, so the cookie has to
> be `.example.org` rather than `.takeinitiative.example.org`. The cost is real: the auth cookie is
> then sent to *every* host under that domain. Fine if the domain is only this project. If it is
> not, the one-hostname shape below avoids the question entirely.
>
> Getting this wrong is quiet — login succeeds and every subsequent call 401s, because the browser
> simply never sends the cookie.

> **Decision, yours to make.** The alternative shape is **one hostname**, with the API reverse-proxied
> under `/api` and `/campaignHub` on the same origin. That removes CORS and the cookie-domain
> question entirely and would let `NUXT_PUBLIC_AXIOS_BASE_URL` be empty — at the cost of
> hand-written Traefik path rules for two prefixes instead of two domain fields in a UI.
> **Recommendation: two hostnames**, as above. It is two tick-boxes versus hand-written routing,
> and everything in this repo is already written for it.

### The images you will reference

```
ghcr.io/pi-gorbo/takeinitiative-api:latest          # the API
ghcr.io/pi-gorbo/takeinitiative-web:latest          # the web app
docker.io/dxflrs/garage:v1.1.0                      # the bucket
```

GHCR paths are lowercase, so the owner `PI-Gorbo` is `pi-gorbo`. You will also see a
`:buildcache` tag on each package — that is the registry build cache the workflow writes, not
something to deploy. Ignore it.

Garage is the one image not from GHCR. Unlike MinIO it needs a config file and a one-off CLI
bootstrap — step 5 covers both. If you would rather depend on exactly one registry, mirror it into
GHCR with `docker buildx imagetools create` and set `GARAGE_IMAGE`; remember to make that package
public, or you have reintroduced the registry credentials the app images avoid.

---

## 1. The server

Install Coolify per its own documentation and get to its dashboard. Then, before anything else:

1. **Check the architecture.** `uname -m` must print `x86_64`. The published images are
   `linux/amd64` only; on an arm64 box they will not run at all, and the error
   (`exec format error`) is clear but only after a confusing pull.
2. **Set up a notification channel** — Coolify has email, Discord, Telegram and generic webhook
   notifications under its global settings (*Settings → Notifications* in the versions this was
   written against). Turn on at least deployment failure notices. This is the cheapest
   observability you will get and it takes two minutes. See `operations.md` for what else is
   worth adding later.

Nothing else on the server needs configuring by hand. In particular **do not** `docker login`
anywhere: the packages are public.

## 2. DNS

Create two `A` records pointing at the VPS's public IP:

```
takeinitiative.samstack.org       A    <vps-ip>
api-takeinitiative.samstack.org   A    <vps-ip>
```

**If your DNS is Cloudflare, leave the proxy OFF (grey cloud, "DNS only") for both.** Coolify
issues the Let's Encrypt certificate itself, and an orange-cloud proxy intercepts the HTTP-01
challenge, so the certificate never issues and you get a Cloudflare error page instead of your
app. This is a common and very confusing first failure.

Wait for both to resolve before you ask Coolify for a certificate:

```sh
dig +short takeinitiative.samstack.org
dig +short api-takeinitiative.samstack.org
```

## 3. Project and environment

In Coolify, create **one project** and use its **`production`** environment. Everything else in
this guide lives inside it. There is no staging environment in this plan — that is listed as "not
in scope" deliberately.

## 4. Postgres 16

Postgres is **not** a service in `compose.prod.yml`. It is a Coolify-managed database resource, and
the reasons are worth knowing because they are the whole argument for the extra step:

- its lifecycle is independent of app redeploys, so a bad deploy cannot take the database with it;
- Coolify owns the volume;
- it gets **scheduled backups from the UI** — otherwise the only copy of everybody's campaign
  lives on one unbacked disk.

Steps:

1. Add a new resource in the project → **Databases → PostgreSQL**.
2. **Pin the version to 16 — `postgres:16-alpine`.** The image/version field is on the
   new-database form, or in the resource's settings before the first start.

   16 because Marten's own CI runs against 15 and 16. Pin a major, never `postgres:latest`.

   > If this ever moves to 18+, the mount has to change with it: 18 made `PGDATA` version
   > specific (`/var/lib/postgresql/18/docker`) and moved its `VOLUME` to `/var/lib/postgresql`.
   > A volume left at the old path makes an 18 container exit 1. None of that applies on 16.

3. **Set the database name, user and password**, or note what Coolify generated. Coolify generates
   its own defaults, which are usually *not* `takeinitiative`/`takeinitiative` — whatever they
   are, they are what goes into the connection string in step 6, so copy them exactly. Use a
   **generated** password, long and random; do not choose one.
4. Start it. Then open the resource page and find the **internal connection string / internal
   hostname** — Coolify shows both an internal and an external form (labelled something like
   *Postgres URL (internal)* and *(external)*). **You want the internal one.** The hostname in it
   is usually the resource's UUID, something like `postgresql-database-abc123def456`.

   Write that hostname down. It is the `<pg-host>` in step 6.
5. **Leave the public port off.** Do not make Postgres publicly available. It has no business
   being reachable from the internet, and step 26's knowledge-base ingest reaches it over an
   `ssh -L` tunnel instead (`operations.md`).
6. **Turn on scheduled backups** on this resource. Coolify's database resources have a Backups
   section with a cron schedule, a retention count, and a choice of local disk or an S3
   destination. Set a daily schedule now, even to local disk — see `operations.md` for why local
   alone is not a backup and what to do about it.

> **Decision, yours to make.** The alternative is Postgres as a service in `compose.prod.yml`,
> which makes the committed file the entire deployment and portable to any Docker host — at the
> cost of rolling your own backups. **Recommendation: Coolify-managed**, as above. The backup UI
> is the reason, and `compose.prod.yml` is written on that assumption.

## 5. The compose resource

> **Superseded — do not follow this section.** The deployment is two Docker Image Applications now,
> not one compose resource. See `docs/deploy/pipeline.md` section 1 for what to create instead, and
> the banner at the top of this file for why. Kept because it is the retreat path, and because
> points 5 and 6 below are still true of the Applications that replaced it.

One resource holds the API, the web app and the bucket. One resource means one deploy, one
environment list, and one place to look.

1. Add a new resource in the project. You are looking for the **Docker Compose** option — in the
   versions this was written against it is reached by choosing a git source and then setting the
   **build pack to "Docker Compose"**, and there is also a "Docker Compose Empty" option for a
   pasted file.
2. **Source: the public repository.** `https://github.com/PI-Gorbo/TakeInitiative`, branch `dev`.
   Because the repo is public you can use Coolify's **Public Repository** source, which needs only
   the URL — **you do not have to install a GitHub App or grant Coolify access to your account.**
3. **Compose file location: `compose.prod.yml`** at the repo root. Some Coolify versions want this
   as `/compose.prod.yml` with a leading slash, and some pre-fill `/docker-compose.yaml`; the
   field is usually called *Docker Compose Location*. If the resource fails to load with a "no
   compose file" error, try the other spelling — that is the whole bug.

   *Git-backed* means Coolify re-reads this file from the repository on every deploy, so the
   committed file is the truth and changing the deployment is a commit. **The alternative** is
   pasting the file into an "empty compose" resource, which avoids pointing Coolify at the repo at
   all but lets the committed file and the running file drift. **Recommendation: git-backed.**
4. **Enable "Connect to predefined network"** on this resource. This is the one setting the whole
   deployment will not work without: it is what puts these containers on Coolify's shared network
   so they can resolve the Postgres resource by its internal hostname. It is usually a toggle in
   the resource's general or advanced settings. Without it, step 9 ends with the API restart-looping
   on `Name or service not known`.
5. **Do not enable automatic deploys on push to `dev`.** Releases drive the deployment, through
   the webhook in step 11. A `dev` push publishes `:edge` and should not go to production.
6. **Do not scale the API.** `AddAsyncDaemon(DaemonMode.Solo)` means exactly one process may run
   the projection daemon, and the startup DDL assumes the same. If Coolify offers a replica count
   for a compose service, leave the API at 1. Forever.

At this point Coolify will have parsed the compose file and will be showing you its three
services: `api`, `web`, `garage`. Do not deploy yet.

## 6. Environment variables

Everything the deployment needs is a container environment variable.
`docs/deploy/production.env.example` is the authoritative list, with a comment on every one. Set
them on the **compose resource** (not on the Postgres resource), in the resource's **Environment
Variables** tab. Most Coolify versions have a bulk/"developer view" paste mode, which is much less
error-prone than fourteen individual rows.

**Two rules for all of them:**

- **Leave "Is build variable?" OFF for every single one.** Nothing in this deployment builds, so a
  build variable is purely a trap for future-you.
- **Do not set `ASPNETCORE_ENVIRONMENT`.** Unset means `Production`, which is what turns off
  Swagger, skips `appsettings.development.json`, and switches the auth cookie to
  `SecurePolicy.Always`. Setting it to anything is how you accidentally ship Swagger.

Substituting `samstack.org` throughout:

| Variable | Value | Secret? | Notes |
|---|---|---|---|
| `WEB_ORIGIN` | `https://takeinitiative.samstack.org` | no | Scheme included, **no trailing slash**. Becomes `CORS__MainApp`, `CORS__AdminApp`, `TakeUrls__Web` and `NUXT_PUBLIC_WEB_URL` |
| `API_ORIGIN` | `https://api-takeinitiative.samstack.org` | no | Scheme included, no trailing slash. Becomes `NUXT_PUBLIC_AXIOS_BASE_URL` |
| `API_HOST` | `api-takeinitiative.samstack.org` | no | **Host name only** — no scheme, no port. Becomes `AllowedHosts` |
| `COOKIE_DOMAIN` | `.samstack.org` | no | **The leading dot is load-bearing** |
| `EMAIL_DOMAIN` | `takeinitiative.samstack.org` | no | Becomes `no-reply@<this>`; must be verified with SendGrid |
| `TAKEDB_CONNECTION` | see below | **yes** | |
| `BLOBS_ACCESS_KEY` | **generate** | **yes** | Must be `GK` + 24 hex — Garage rejects any other shape |
| `BLOBS_SECRET_KEY` | **generate** | **yes** | 32 bytes hex |
| `SENDGRID_API_KEY` | `SG.…` | **yes** | From your SendGrid account |
| `JWT_SIGNING_KEY` | **generate**, 64 chars | **yes** | Bound to `JWTOptions`, `required` there, and never actually read. Set it so startup does not fail; expect nothing from it |

`TAKEDB_CONNECTION`, using the hostname, database, user and password from step 4:

```
Host=<pg-internal-host>;Port=5432;Database=<db>;User ID=<user>;Password=<password>;
```

**Generate, do not choose**, the three marked *generate* plus the Postgres password:

```sh
openssl rand -base64 48 | tr -d '/+=' | cut -c1-64   # JWT_SIGNING_KEY
openssl rand -hex 16                                 # BLOBS_ACCESS_KEY
openssl rand -base64 36 | tr -d '/+='                # BLOBS_SECRET_KEY
```

`BLOBS_ACCESS_KEY` / `BLOBS_SECRET_KEY` are the API's `Blobs__AccessKey` / `Blobs__SecretKey`, and
the same pair you `garage key import` in step 5. Garage takes no credentials from the environment,
so unlike MinIO these two live in both places by hand. Keep a copy.

```sh
echo "GK$(openssl rand -hex 12)"   # BLOBS_ACCESS_KEY — the GK prefix is mandatory
openssl rand -hex 32               # BLOBS_SECRET_KEY
```

**Leave `API_IMAGE`, `WEB_IMAGE` and `GARAGE_IMAGE` unset.** Unset is the steady state:
`compose.prod.yml`'s `:latest` default applies and the release workflow tells Coolify to re-pull.
A value in any of them is a **pin** that somebody has to remember to remove. They exist for
rollback, and `operations.md` is where that is written down.

Everything else is fixed inside `compose.prod.yml` because it is a property of the deployment
rather than of the environment: `Blobs__ServiceUrl`, `Blobs__Bucket`, `Blobs__Region`,
`Blobs__ForcePathStyle`, `Blobs__CreateBucket` and `Marten__ApplySchemaOnStartup`. Do not
re-declare them here, and in particular do not "fix" `Blobs__ForcePathStyle=true` — that is what a
non-AWS S3 needs. They each also have an `appsettings.json` default, which is what makes omitting
them survivable on a deployment that does not run `compose.prod.yml`.

> **`DataProtection__KeyPath=/keys` is the exception, and must be set.** It was in the list above
> until SAM-27, where it cost a release: it is the only one of these with no `appsettings.json`
> default, so on a Coolify **application** — which does not read `compose.prod.yml` at all — it was
> simply absent, the key ring went back inside the container, and every deploy signed everybody out.
> Set it on the API application, with the `/keys` persistent storage of section 8. The API now
> refuses to boot in Production without it, so the failure is a failed deploy rather than a silent
> mass sign-out.

## 7. Domains and TLS

Coolify runs **Traefik** in front of everything and terminates TLS there, forwarding plain HTTP to
the container. Traefik routes by the request's `Host` header: you tell Coolify which hostname
belongs to which service, Coolify generates the Traefik labels, and Traefik does the rest. You
never write a Traefik rule by hand for this shape.

So, for a compose resource, there is a **domain field per service**:

| Service | Domain | Container port |
|---|---|---|
| `web` | `https://takeinitiative.samstack.org` | 3000 |
| `api` | `https://api-takeinitiative.samstack.org` | 8080 |
| `garage` | **none — leave it blank** | — |

Coolify infers the container port from the service's exposed port when there is only one, and both
of these services expose exactly one. If your version asks for it, or routes to the wrong port,
Coolify's domain field accepts a **`https://host:port`** form — `https://api-takeinitiative.samstack.org:8080`
— where the port is the *container's* port, not a published host port. (*Unverified for your
version; it is the convention in the versions this was written against.*)

`compose.prod.yml` publishes **no host ports at all**. Traefik reaches `api` and `web` on the
compose network; `minio` is not reachable from outside, and neither is Postgres. That is
deliberate — there are no presigned URLs anywhere in this app, so every image is streamed through
the API after a visibility check, and the bucket has no business being public and needs no CORS
configuration at all.

**TLS** is automatic once DNS resolves: Coolify requests a Let's Encrypt certificate per domain
and redirects HTTP to HTTPS. If a certificate does not issue, the cause is almost always step 2 —
DNS not propagated, or a Cloudflare proxy intercepting the challenge.

Coolify can alternatively take domains from `SERVICE_FQDN_WEB_3000` / `SERVICE_FQDN_API_8080`
"magic" variables inside the compose file. **The exact spelling of these drifts between Coolify
versions**, and `compose.prod.yml` deliberately does not use them. Set the domains in the UI, get
a working deploy, and only move them into the file later if you prefer that — never both at once.

## 8. Persistent storage

`compose.prod.yml` declares these volumes at the bottom, but **nothing creates them for you**: the
deployment is two Docker Image applications plus Coolify's own resources, and that compose file is
never read (`docs/deploy/pipeline.md` section 1). Add each one by hand as a **Persistent Storage**
entry on the resource that needs it, and **check that it is there rather than assuming**.

| Resource | Mount | What breaks without it |
|---|---|---|
| API application | `/keys` | **Every single deploy signs everybody out, mid-session.** The auth cookie is an ASP.NET Core Data Protection payload; with no volume the key ring is written inside the container and thrown away with it. Twice a real bug — see the `DataProtection__KeyPath` note in section 6, which is the other half of it and the half that is easier to miss |
| Garage service | `/var/lib/garage/meta` and `/var/lib/garage/data` | **Every image anyone has ever uploaded is gone.** Nothing else on the box holds them and nothing backs this up automatically |

Two things worth knowing:

- The API image does `mkdir -p /keys && chown $APP_UID:$APP_UID /keys` precisely so that a **fresh**
  named volume inherits ownership the unprivileged user can write. A volume mounted at a path the
  image does not own arrives root-owned, and the first sign-in then fails on a directory it cannot
  write. You do not need to do anything — but if you ever create the volume by hand, or pre-seed
  it, this is what you have to preserve.
- Coolify may prefix the actual Docker volume names with the resource's identifier. Find the real
  names once, on the box, and write them down — `operations.md` needs them for the bucket backup:

  ```sh
  docker volume ls | grep -E 'keys|garage'
  ```

## 9. The first deploy, and proving it pulled

Hit **Deploy**. Then read the log, which is the interesting part.

**What you must see:**

```
Pulling from pi-gorbo/takeinitiative-api
Pulling from pi-gorbo/takeinitiative-web
… Pull complete …
```

**What must not appear anywhere:**

- `Step 1/…` or `#1 [internal] load build definition`
- `exporting to image`
- any BuildKit `=> [build …]` lines

If you see those, something is building on your VPS and the whole design has been defeated. The
only way that happens is a `build:` key, so check that the resource is reading the committed
`compose.prod.yml` and not a pasted or edited copy.

Then prove it on the box, over SSH. This is the verification step 29 is actually built around:

```sh
# A PULLED image has a registry digest. A locally BUILT image's RepoDigests is empty.
docker image inspect ghcr.io/pi-gorbo/takeinitiative-api:latest --format '{{.RepoDigests}}'
docker image inspect ghcr.io/pi-gorbo/takeinitiative-web:latest --format '{{.RepoDigests}}'

# The build cache must be empty, because nothing here has ever built.
docker buildx du
```

Both must print `ghcr.io/…@sha256:…`, and those digests must match the ones in the GitHub Actions
run summary for the release (the workflow prints `<image>@<digest>` under "Pin this:"). `buildx du`
must show an empty cache.

Then wait for all three containers to go **healthy**:

```sh
docker ps --format '{{.Names}}\t{{.Status}}'
```

The API's `start_period` is 60 s, and generously so: on a fresh database Marten applies the entire
schema before Kestrel opens the port. If the API is still `health: starting` after a minute or two,
go to step 12.

## 10. Verify it works, end to end

In order. The last one is the point.

1. **TLS and the domains.**

   ```sh
   curl -sI https://takeinitiative.samstack.org/ | head -1
   curl -s  https://api-takeinitiative.samstack.org/healthz
   curl -sI http://takeinitiative.samstack.org/ | head -1    # expect a 3xx redirect to https
   ```

   Both over a valid certificate. Note that `/healthz` answers **200 through Traefik** because
   Traefik passes the real `Host` header — the host-filtering trap in step 12 only bites probes
   made from *inside* the container.

2. **The server-rendered pages, not just the app.** Open all three in a browser:

   | | |
   |---|---|
   | `https://takeinitiative.samstack.org/` | the landing page — **server-rendered** |
   | `https://takeinitiative.samstack.org/login` | **server-rendered** |
   | `https://takeinitiative.samstack.org/app` | the SPA (`ssr: false`) |

   **Check all three, deliberately.** A bug that broke exactly `/` and `/login` while `/app` was
   perfectly fine was fixed in `d6f7f53` (*"fix(web): declare pinia, so production SSR stops
   returning 500"*), and a health check on `/healthz` alone would not have caught it — `/healthz` is
   a Nitro route that answers before any page renders. Watch for a 500, and for a page that renders
   but is missing its content.

3. **The runtime API URL reached the browser.** View source on `/app` (or
   `curl -s https://takeinitiative.samstack.org/app | grep -o 'https://api[^"]*'`) and find your
   API origin inside `window.__NUXT__.config`. That is `NUXT_PUBLIC_AXIOS_BASE_URL` being applied
   per request. If it is empty or wrong, the variable is wrong — it is **not** a rebuild.

4. **The schema.** On the production database (`docker exec` into the Postgres container, or over
   the tunnel from `operations.md`):

   ```sql
   select extname from pg_extension;    -- must include pg_trgm and unaccent
   \dt                                  -- mt_doc_campaign, mt_doc_entry, mt_events, knowledge_base_item, …
   ```

   If `pg_trgm` and `unaccent` are missing, `Marten__ApplySchemaOnStartup` did not run and ⌘K's
   fuzzy matching will fail at query time with *function does not exist*. Those two extensions hang
   off no document type, so nothing in a request path would ever create them.

5. **Sign up, as yourself.** Create an account, create a campaign, post a note. Then:

6. **Sessions survive a deploy.** Stay signed in, hit **Redeploy** in Coolify, wait, reload.
   **You must still be signed in.** If you are not, the `/keys` volume is not doing its job — step
   8, and step 12's row on it. This is the single easiest thing in the whole deployment to get
   wrong and the easiest to not notice.

7. **A photo.** Post a note with an image. It must render. That proves the bucket exists, the
   credentials match, and the API can stream it back out.

8. **Email.** Trigger a password reset. It must arrive, from `no-reply@<EMAIL_DOMAIN>`, with a link
   on the real web domain.

9. **A friend, on their phone.** The actual finish line. Send the URL to somebody who has never
   seen the app:

   1. They sign up and land in the app.
   2. They join the campaign with the join code.
   3. They post a note in the current session with an `@mention` and a photo from their camera
      roll, and the photo renders.
   4. **You, on a laptop, see the note appear without refreshing.** That is SignalR over WebSockets
      through Traefik, which needs no configuration but is worth confirming — if real-time is dead
      while everything else works, the hub is the thing to look at.
   5. They add the app to their home screen, it opens standalone, and sharing a photo to it from
      the share sheet lands in the composer.
   6. "✨ Find suggestions" on their own note downloads the model from the app's own origin and
      offers a suggestion.

If all of that passes, you are live.

## 11. The deploy webhook

> **Superseded — do not follow this section.** A webhook POST can only tell Coolify to re-pull a
> MOVING tag, so it cannot say which bytes it deployed and leaves a rollback with no artefact to
> name. `.github/workflows/deploy.yml` pins an exact digest through the Coolify API and polls the
> deployment instead. The secrets it needs are `COOLIFY_BASE_URL` (a repository **variable**) and
> `COOLIFY_API_TOKEN` (a `prod` Environment secret) — see `docs/deploy/pipeline.md` section 5.
> `COOLIFY_DEPLOY_URL` and `COOLIFY_TOKEN` can be deleted once the first pinned deploy has worked.

Last, so that a release can deploy itself.

1. In the compose resource, find the **deploy webhook URL** (a Webhooks tab on the resource in the
   versions this was written against). It has the shape:

   ```
   https://<coolify-host>/api/v1/deploy?uuid=<resource-uuid>&force=true
   ```

   **`force=true` matters**: it is what makes Coolify re-pull a moving tag rather than reuse the
   image already on the box. If the URL Coolify gives you does not have it, add it.
2. Mint a **Coolify API token** (global settings → API tokens / Keys & Tokens).
3. Add both as GitHub Actions **repository secrets**:

   | Secret | Value |
   |---|---|
   | `COOLIFY_DEPLOY_URL` | the URL above |
   | `COOLIFY_TOKEN` | the API token |

4. Test it from your machine before relying on it:

   ```sh
   curl -fsS -X POST -H "Authorization: Bearer $COOLIFY_TOKEN" "$COOLIFY_DEPLOY_URL"
   ```

   A deploy should start in Coolify.

Until those secrets exist, `images.yml`'s `deploy` job prints *"No COOLIFY_DEPLOY_URL secret;
nothing to notify. Skipping."* and exits 0, so a release does not go red for want of a server.
There is deliberately **no registry secret** anywhere: `GITHUB_TOKEN` covers the push, and the
packages are public so nothing on the VPS needs credentials to pull.

## 11a. Cloudflare Tunnel, if the server has no public IP

Coolify's one-click **cloudflared** template works as-is. Two things its guide does not spell out
for a multi-server setup:

- **It goes on the server running the resources**, not on the Coolify control plane. The tunnel has
  to reach the Traefik that holds your hostnames' routes. On the control plane it would reach a
  proxy with no route for them.
- **`http://localhost:80`, not https.** Traefik serves plain HTTP on 80; Cloudflare terminates TLS
  at the edge. Pointing `https://` at it fails the handshake and surfaces as a 502. This works
  because the template sets `network_mode: host` — a cloudflared without it would resolve
  `localhost` to its own container and every route would dead-end.

One tunnel serves every hostname: add a **Public Hostname** entry per domain, all pointing at
`http://localhost:80`, and Traefik routes by `Host` from there.

Leave Coolify's Let's Encrypt off for these domains. With no inbound :80 the ACME challenge cannot
complete, and Cloudflare already provides the public certificate.

## 12. When it does not work

The failure modes in rough order of likelihood. Each has a **distinguishing symptom** — use that
rather than guessing, because several of these look identical from the outside.

### 1. Coolify reports a failed deploy while the API is working perfectly

**This is the most confusing failure available in this step, because every log line looks fine.**

`/healthz` needs no authentication, but it still passes through ASP.NET Core's **host filtering**,
which is active whenever `AllowedHosts` is set — and production sets it, to `API_HOST`. Measured:

| Request, inside the container | Result |
|---|---|
| `Host: api.example.test` | **200** |
| no `Host` header (so `127.0.0.1`) | **400** |
| `Host: evil.example` | **400** |

`curl --fail` fails on a 400, so the obvious probe never succeeds. With `restart: unless-stopped`
the container flaps forever and Coolify calls the deploy failed.

`compose.prod.yml`'s healthcheck already handles this: it takes the first `;`-separated name out of
the container's own `AllowedHosts` and presents it as the `Host` header, falling back to
`localhost`. So there is one source of truth and nothing to keep in sync.

**Symptom:** the app answers correctly on its domain, but `docker ps` shows the API `(unhealthy)`.

**Confirm it:**

```sh
docker inspect --format '{{json .State.Health}}' <api-container> | head -c 2000

docker exec <api-container> sh -c 'curl -s -o /dev/null -w "%{http_code}\n" \
    -H "Host: api-takeinitiative.samstack.org" http://127.0.0.1:8080/healthz'   # expect 200
docker exec <api-container> sh -c 'curl -s -o /dev/null -w "%{http_code}\n" \
    http://127.0.0.1:8080/healthz'                                               # expect 400
```

If the first is 200 and the second is 400, the app is fine and the probe is the problem. Two
causes:

- **`API_HOST` is wrong** — it must be the host *name* only, no scheme and no port, and it must
  match the domain you gave the `api` service in step 7.
- **Coolify mangled the `$$` escaping.** The healthcheck uses `$${AllowedHosts:-localhost}`, where
  `$$` is compose's escape for a literal `$` so that the expansion happens in the *container's*
  shell. Coolify does its own variable substitution pass over compose files, and if yours
  interferes, the probe will be looking for a variable that is empty.
  (*Unverified — it works under plain `docker compose`; check it here if the symptom above shows
  up with a correct `API_HOST`.*) The workaround, if so, is to add `localhost` to `AllowedHosts`
  (`API_HOST=api-takeinitiative.samstack.org;localhost` — note the probe uses the **first** name, so
  put the real host first and this will still work).

**Anyone editing a healthcheck on this API must know this**, or they will chase a phantom failure
for an afternoon.

### 2. The API restart-loops and cannot find the database

**Symptom:** the API container restarts repeatedly; `docker logs` shows an Npgsql error —
`Name or service not known`, `No such host is known`, or `Connection refused`.

In order of likelihood:

- **"Connect to predefined network" is off** on the compose resource (step 5.4). The container
  cannot resolve the Postgres resource's internal hostname at all. This is the single most common
  cause.
- **The internal hostname in `TAKEDB_CONNECTION` is wrong.** Re-copy it from the Postgres
  resource page (step 4.4). Use the *internal* connection string, not the external one.
- **The database name, user or password do not match** what Coolify generated. `Connection refused`
  is a network problem; `password authentication failed` or `database "takeinitiative" does not
  exist` is this.

Confirm from inside the API container:

```sh
docker exec <api-container> sh -c 'getent hosts <pg-internal-host>'
```

Nothing back means DNS, which means the network setting.

*Unverified, but worth knowing if nothing else fits:* Npgsql negotiates TLS by default
(`SSL Mode=Prefer`) and falls back to plaintext against a server that does not offer it, which is
the Coolify Postgres image. If you ever see an SSL/certificate error in that log line rather than
a name or auth error, append `SSL Mode=Disable;` to the connection string.

### 3. The app shell loads but every request fails

**Symptom:** the page renders, and the browser console is full of
`No 'Access-Control-Allow-Origin' header is present` or
`Cross-Origin Request Blocked`. It looks exactly like the API being down, and the API is fine.

`CORS__MainApp` (which is `WEB_ORIGIN`) must be the web app's origin **character for character**:
the right scheme, no trailing slash, no `www` if you did not use `www`. Compare it against what the
browser's network tab shows as the `Origin` header on the failed request. Those two strings being
different in a way you cannot see is the whole bug.

`CORS__AdminApp` must also be set even though there is no admin app: `Program.cs` throws on the
missing key. `compose.prod.yml` sets it to the same origin rather than to something that would
widen CORS.

### 4. Everybody is signed out after every deploy

**Symptom:** sign-in works; after a redeploy, every session is gone. Nothing errors.

**Check the environment variable first.** This has happened twice, and both times it was
`DataProtection__KeyPath` never having been set on the API application rather than anything wrong
with the volume — see the note in section 6 for why the docs themselves caused that. Unset, the API
configures no key ring at all and ASP.NET Core writes it to `$HOME/.aspnet/DataProtection-Keys`
inside the container, which is thrown away with the container.

Failing that, the `/keys` volume (section 8) is missing, or is mounted but root-owned so the
unprivileged user cannot write it. The log will usually carry a Data Protection warning about not
being able to persist keys to the file system, or about using an ephemeral key ring.

```sh
docker exec <api-container> printenv DataProtection__KeyPath   # must print /keys — check this FIRST
docker exec <api-container> ls -ld /keys        # must be owned by uid 1654
docker exec <api-container> ls -l  /keys        # must contain a key-*.xml after a first sign-in
```

On a current image the variable cannot be silently absent: the API refuses to boot in Production
without it, so a deploy that gets this wrong fails visibly instead. A container that *starts* has a
key path; what it may still lack is a volume under it.

If `/keys` is root-owned, the volume was created before the image's `chown` could apply to it.
Delete the (empty) volume and redeploy so Docker initialises a fresh one from the image's
ownership. **Everybody will be signed out once more** — that is the last time.

### 5. Everything is healthy and photos fail

**Symptom:** the app works, the API is healthy, and the first photo upload 500s. The log mentions
`NoSuchBucket`, or `SignatureDoesNotMatch`, or `The AWS Access Key Id you provided does not exist`
— which reads exactly like a credentials problem whichever cause it is.

`BlobBucketInitializer` is idempotent and retries for 30 s while MinIO comes up, and then **only
logs**. So the API starts healthy either way and the failure surfaces at the first photo.

- `Blobs__CreateBucket` must be `true` on a fresh self-hosted volume. It is, in
  `compose.prod.yml`; check nothing has overridden it.
- `BLOBS_ACCESS_KEY` / `BLOBS_SECRET_KEY` are used twice — as MinIO's root credentials and as the
  API's. If you edited one occurrence by hand rather than the variable, they have drifted.
- MinIO refuses to start at all with a user under 3 characters or a password under 8. Check the
  `minio` container's own log if it never went healthy.
- A 413 on a large photo is the proxy, not the API: uploads are capped at 20 MB
  (`Images:MaxUploadBytes`), and Traefik imposes no body limit by default, so if you see 413 look
  at middleware before you look at the app.

### 6. A Traefik error page, a 502, or no certificate

**Symptom:** you never reach the app at all — a Coolify/Traefik default page, a `404 page not
found` from Traefik, a 502, or a certificate warning.

- **Certificate never issued:** DNS has not propagated, or a Cloudflare proxy is intercepting the
  HTTP-01 challenge (step 2). Grey-cloud both records.
- **404 from Traefik:** the domain is not attached to any service, or is attached to the wrong one.
  Re-check step 7, including which service got which hostname.
- **502:** Traefik found a route but the container behind it is not listening — usually the wrong
  container port, or the container is unhealthy for one of the reasons above. Check
  `docker ps` first.

### 7. `/` and `/login` are broken but `/app` is fine

**Symptom:** the SPA works for anyone already signed in, and new visitors get a 500 or a blank
page. `/healthz` is green and no monitor fires.

The server-rendered routes have their own failure mode, and nothing in the health probes covers
them. Read the **web** container's log for the stack trace. This is why step 10.2 says to check all
three paths by hand on every deploy, and why `operations.md` recommends that any uptime check hit
`/` rather than only `/healthz`.

### 8. It built on the VPS

**Symptom:** `Step 1/…` or BuildKit `=>` lines in the deploy log, a non-empty `docker buildx du`,
or an image whose `RepoDigests` is empty.

This should be impossible: there is no build context anywhere in `compose.prod.yml`, and `grep -n
"build:" compose.prod.yml` finds nothing. If it happens, the resource is not reading the committed
file — check the compose file path and whether somebody pasted an edited copy.

### 9. Deployment fails at `docker compose pull` with "context not found"

```
unable to resolve docker endpoint: context "desktop-linux": context not found:
open /root/.docker/contexts/meta/<sha>/meta.json: no such file or directory
```

Nothing to do with the images or the app — it fails before the pull. A Docker Desktop leftover: a
`config.json` carries `"currentContext": "desktop-linux"` while that context does not exist.

**The path is inside the helper container, not on the host.** Coolify bind-mounts the
**connecting user's** `~/.docker/config.json` to `/root/.docker/config.json` in the helper — and it
connects as the SSH user, not root. So checking root's config proves nothing. Find the real one:

```sh
docker inspect <helper-container> --format '{{json .Mounts}}'
```

Then remove `currentContext` (and `credsStore: "desktop"`, another Desktop leftover that points at
a binary the helper does not have) from whatever file that names. `docker context ls` shows a broken
`currentContext` as a row with an ERROR, so a clean listing means you are looking at the wrong file.

### 10. `Blob store bucket … Resource temporarily unavailable`

`Blobs__ServiceUrl` names the Coolify **service UUID** rather than the container. The UUID appears
in Coolify's generated URLs but resolves to nothing on the Docker network. Get the real name:

```sh
docker ps --format '{{.Names}}' | grep -i garage
```

It looks like `garage-<uuid>`. Keep port 3900. If it still will not resolve, the two resources are
not on the same network — enable **Connect to predefined network** on both.

### 11. `Blob store bucket … Forbidden: Invalid signature`

The credentials reach Garage but the secret is wrong. Coolify's Garage template has two
similarly-named variables and only one is the S3 secret:

```yaml
GARAGE_ADMIN_TOKEN=$SERVICE_PASSWORD_GARAGE            # NOT this one
GARAGE_DEFAULT_SECRET_KEY=${SERVICE_PASSWORD_64_GARAGE} # this one
```

Compare what each container actually holds:

```sh
docker inspect <garage> --format '{{range .Config.Env}}{{println .}}{{end}}' | grep GARAGE_DEFAULT
docker inspect <api>    --format '{{range .Config.Env}}{{println .}}{{end}}' | grep Blobs__
```

A *region* mismatch is a different error — `AuthorizationHeaderMalformed … unexpected scope` — and
means `Blobs__Region` is not `garage`.

### 12. The hostname works on the server but not from your machine

```sh
curl -H "Host: api-takeinitiative.example.org" http://localhost/healthz   # 200 on the server
curl https://api-takeinitiative.example.org/healthz                       # could not resolve
```

A cached `NXDOMAIN` from querying the name before its DNS record existed. `dig` and `host` bypass
the OS resolver cache, so they succeed while `curl` and browsers fail — that split is the symptom.

```sh
sudo dscacheutil -flushcache && sudo killall -HUP mDNSResponder   # macOS
sudo resolvectl flush-caches                                      # systemd
```

Browsers cache separately (`chrome://net-internals/#dns`). To prove the server is fine meanwhile,
bypass DNS entirely:

```sh
curl --resolve api-takeinitiative.example.org:443:<cloudflare-ip> https://api-takeinitiative.example.org/healthz
```

---

## Appendix: write down what you did

Keep a note, in your password manager or wherever this deployment's secrets live:

- the VPS IP, the SSH user, and the Coolify URL;
- the Postgres resource's **internal hostname**, database, user and password;
- the two real Docker volume names (`docker volume ls`);
- `BLOBS_ACCESS_KEY` / `BLOBS_SECRET_KEY`;
- the compose resource's **UUID** (the deploy webhook needs it);
- the digests of the release you deployed first, so you have a known-good pin from day one.

And when something in this guide turns out to be wrong for your Coolify version, fix it here.
The things that did not work are the part nobody remembers a year later.

`operations.md` is the next file: rollback, backups, the knowledge-base ingest, and what a healthy
startup looks like in the log.
