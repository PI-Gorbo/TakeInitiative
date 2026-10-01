# Coolify: the first deployment, by hand

This is the walkthrough for standing TakeInitiative up on a Coolify server for the first time. It
assumes you have never used Coolify before and that you know your way around a Linux box, Docker
and DNS. Follow it top to bottom; it is written to be done once, in one sitting, without going
back and forth.

**The one rule.** Coolify **pulls** images from GHCR and runs them. It never builds. GitHub
Actions already built them on a free runner (`.github/workflows/images.yml`), and
`compose.prod.yml` contains no build key anywhere, so building is not merely discouraged — it is
impossible. Step 9 below is how you prove it, and that proof is the point of the whole approach.

**What is settled before you start**, so you do not have to decide it here:

| | |
|---|---|
| Target | an Ubuntu box, **arm64** |
| Images | `linux/arm64` only, built on `ubuntu-24.04-arm` |
| Registry | GHCR, **public packages** — so the server needs no registry credentials |
| Postgres | **Coolify-managed, pinned to 15** — it is deliberately not in `compose.prod.yml` |
| Object store | MinIO, as a service in `compose.prod.yml`, on a volume |
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
- [ ] **A VPS** with a public IP, Ubuntu, arm64, and Docker. Coolify's own installer puts Docker
      on for you. Rough sizing: **2 vCPU / 4 GB is comfortable** for Postgres + MinIO + two small
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
| Web app | `https://takeinitiative.<your-domain>` | |
| API | `https://api.takeinitiative.<your-domain>` | |
| Cookie domain | `.takeinitiative.<your-domain>` | the leading dot is what makes one auth cookie valid on both |

**Keep the API on a subdomain of the web app's domain.** That is what lets
`CookieDomain=.takeinitiative.<domain>` work with the cookie's default `SameSite=Lax`, because
same-registrable-domain requests are *same-site*. Put the API on a genuinely different domain and
cross-site XHR needs `SameSite=None; Secure`, which is a code change nobody needs.

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
docker.io/pgsty/minio:RELEASE.2026-08-04T00-00-00Z@sha256:b6bfe723…   # the bucket, digest-pinned
```

GHCR paths are lowercase, so the owner `PI-Gorbo` is `pi-gorbo`. You will also see a
`:buildcache` tag on each package — that is the registry build cache the workflow writes, not
something to deploy. Ignore it.

MinIO is the one image not from GHCR, because MinIO stopped publishing community images in 2025
and `pgsty/minio` is a community rebuild of the same server. It is pinned by digest so a retag
upstream cannot change what runs. If you would rather depend on exactly one registry, mirror it —
the command is in the comment above the `minio` service in `compose.prod.yml` — and set
`MINIO_IMAGE` to the result. Remember to make *that* package public too, or you have just
reintroduced the registry credentials the app images avoid.

---

## 1. The server

Install Coolify per its own documentation and get to its dashboard. Then, before anything else:

1. **Check the architecture.** `uname -m` must print `aarch64`. The published images are
   `linux/arm64` only; on an amd64 box they will not run at all, and the error
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
takeinitiative.<your-domain>       A    <vps-ip>
api.takeinitiative.<your-domain>   A    <vps-ip>
```

**If your DNS is Cloudflare, leave the proxy OFF (grey cloud, "DNS only") for both.** Coolify
issues the Let's Encrypt certificate itself, and an orange-cloud proxy intercepts the HTTP-01
challenge, so the certificate never issues and you get a Cloudflare error page instead of your
app. This is a common and very confusing first failure.

Wait for both to resolve before you ask Coolify for a certificate:

```sh
dig +short takeinitiative.<your-domain>
dig +short api.takeinitiative.<your-domain>
```

## 3. Project and environment

In Coolify, create **one project** and use its **`production`** environment. Everything else in
this guide lives inside it. There is no staging environment in this plan — that is listed as "not
in scope" deliberately.

## 4. Postgres 15

Postgres is **not** a service in `compose.prod.yml`. It is a Coolify-managed database resource, and
the reasons are worth knowing because they are the whole argument for the extra step:

- its lifecycle is independent of app redeploys, so a bad deploy cannot take the database with it;
- Coolify owns the volume;
- it gets **scheduled backups from the UI** — otherwise the only copy of everybody's campaign
  lives on one unbacked disk.

Steps:

1. Add a new resource in the project → **Databases → PostgreSQL**.
2. **Pin the version to 15.** You are looking for the image/version field on the new-database form
   (or, if your version does not offer one there, the image field in the resource's settings
   before the first start). `postgres:15` or `postgres:15-alpine`.

   This matters: `postgres:latest` is 18+, which moved its data directory, so a dump taken from
   dev will not restore into it. `compose.dev.yml` and the Alba/Testcontainers fixtures are all 15.
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
services: `api`, `web`, `minio`. Do not deploy yet.

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

Substituting `<your-domain>` throughout:

| Variable | Value | Secret? | Notes |
|---|---|---|---|
| `WEB_ORIGIN` | `https://takeinitiative.<your-domain>` | no | Scheme included, **no trailing slash**. Becomes `CORS__MainApp`, `CORS__AdminApp`, `TakeUrls__Web` and `NUXT_PUBLIC_WEB_URL` |
| `API_ORIGIN` | `https://api.takeinitiative.<your-domain>` | no | Scheme included, no trailing slash. Becomes `NUXT_PUBLIC_AXIOS_BASE_URL` |
| `API_HOST` | `api.takeinitiative.<your-domain>` | no | **Host name only** — no scheme, no port. Becomes `AllowedHosts` |
| `COOKIE_DOMAIN` | `.takeinitiative.<your-domain>` | no | **The leading dot is load-bearing** |
| `EMAIL_DOMAIN` | `takeinitiative.<your-domain>` | no | Becomes `no-reply@<this>`; must be verified with SendGrid |
| `TAKEDB_CONNECTION` | see below | **yes** | |
| `BLOBS_ACCESS_KEY` | **generate** | **yes** | ≥ 3 characters or MinIO refuses to start |
| `BLOBS_SECRET_KEY` | **generate** | **yes** | ≥ 8 characters or MinIO refuses to start |
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

`BLOBS_ACCESS_KEY` / `BLOBS_SECRET_KEY` are **one pair used twice**: they are MinIO's root
credentials *and* the API's `Blobs__AccessKey` / `Blobs__SecretKey`. `compose.prod.yml` wires both
sides from the same variables so they cannot drift. Keep a copy — you will want them if you ever
run `mc` against the bucket.

**Leave `API_IMAGE`, `WEB_IMAGE` and `MINIO_IMAGE` unset.** Unset is the steady state:
`compose.prod.yml`'s `:latest` default applies and the release workflow tells Coolify to re-pull.
A value in any of them is a **pin** that somebody has to remember to remove. They exist for
rollback, and `operations.md` is where that is written down.

Everything else is fixed inside `compose.prod.yml` because it is a property of the deployment
rather than of the environment: `Blobs__ServiceUrl`, `Blobs__Bucket`, `Blobs__Region`,
`Blobs__ForcePathStyle`, `Blobs__CreateBucket`, `Marten__ApplySchemaOnStartup` and
`DataProtection__KeyPath`. Do not re-declare them here, and in particular do not "fix"
`Blobs__ForcePathStyle=true` — that is what a non-AWS S3 needs.

## 7. Domains and TLS

Coolify runs **Traefik** in front of everything and terminates TLS there, forwarding plain HTTP to
the container. Traefik routes by the request's `Host` header: you tell Coolify which hostname
belongs to which service, Coolify generates the Traefik labels, and Traefik does the rest. You
never write a Traefik rule by hand for this shape.

So, for a compose resource, there is a **domain field per service**:

| Service | Domain | Container port |
|---|---|---|
| `web` | `https://takeinitiative.<your-domain>` | 3000 |
| `api` | `https://api.takeinitiative.<your-domain>` | 8080 |
| `minio` | **none — leave it blank** | — |

Coolify infers the container port from the service's exposed port when there is only one, and both
of these services expose exactly one. If your version asks for it, or routes to the wrong port,
Coolify's domain field accepts a **`https://host:port`** form — `https://api.takeinitiative.<your-domain>:8080`
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

Two volumes, both declared at the bottom of `compose.prod.yml`, so Coolify should create them from
the file. **Check that they exist rather than assuming** — the resource's Persistent Storage tab
per service is where to look.

| Service | Volume | Mount | What breaks without it |
|---|---|---|---|
| `api` | `takeapi-keys` | `/keys` | **Every single deploy signs everybody out, mid-session.** The auth cookie is an ASP.NET Core Data Protection payload; with no volume the key ring is written inside the container and thrown away with it. This was a real bug. The volume is the fix, not tidiness |
| `minio` | `takeminio-data` | `/data` | **Every image anyone has ever uploaded is gone.** Nothing else on the box holds them and nothing backs this up automatically |

Two things worth knowing:

- The API image does `mkdir -p /keys && chown $APP_UID:$APP_UID /keys` precisely so that a **fresh**
  named volume inherits ownership the unprivileged user can write. A volume mounted at a path the
  image does not own arrives root-owned, and the first sign-in then fails on a directory it cannot
  write. You do not need to do anything — but if you ever create the volume by hand, or pre-seed
  it, this is what you have to preserve.
- Coolify may prefix the actual Docker volume names with the resource's identifier. Find the real
  names once, on the box, and write them down — `operations.md` needs them for the bucket backup:

  ```sh
  docker volume ls | grep -E 'takeapi-keys|takeminio-data'
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
   curl -sI https://takeinitiative.<your-domain>/ | head -1
   curl -s  https://api.takeinitiative.<your-domain>/healthz
   curl -sI http://takeinitiative.<your-domain>/ | head -1    # expect a 3xx redirect to https
   ```

   Both over a valid certificate. Note that `/healthz` answers **200 through Traefik** because
   Traefik passes the real `Host` header — the host-filtering trap in step 12 only bites probes
   made from *inside* the container.

2. **The server-rendered pages, not just the app.** Open all three in a browser:

   | | |
   |---|---|
   | `https://takeinitiative.<your-domain>/` | the landing page — **server-rendered** |
   | `https://takeinitiative.<your-domain>/login` | **server-rendered** |
   | `https://takeinitiative.<your-domain>/app` | the SPA (`ssr: false`) |

   **Check all three, deliberately.** A bug that broke exactly `/` and `/login` while `/app` was
   perfectly fine was fixed in `d6f7f53` (*"fix(web): declare pinia, so production SSR stops
   returning 500"*), and a health check on `/healthz` alone would not have caught it — `/healthz` is
   a Nitro route that answers before any page renders. Watch for a 500, and for a page that renders
   but is missing its content.

3. **The runtime API URL reached the browser.** View source on `/app` (or
   `curl -s https://takeinitiative.<your-domain>/app | grep -o 'https://api[^"]*'`) and find your
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
    -H "Host: api.takeinitiative.<your-domain>" http://127.0.0.1:8080/healthz'   # expect 200
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
  (`API_HOST=api.takeinitiative.<domain>;localhost` — note the probe uses the **first** name, so
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

The `/keys` volume (step 8) is missing, or is mounted but root-owned so the unprivileged user
cannot write it. The log will usually carry a Data Protection warning about not being able to
persist keys to the file system, or about using an ephemeral key ring.

```sh
docker exec <api-container> ls -ld /keys        # must be owned by uid 1654
docker exec <api-container> ls -l  /keys        # must contain a key-*.xml after a first sign-in
docker exec <api-container> printenv DataProtection__KeyPath   # must print /keys
```

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
