# Operations: running TakeInitiative after it is live

The runbook. `coolify.md` is how the deployment got built; this is what you do with it afterwards —
shipping a release, undoing one, keeping the data, feeding the knowledge base, and knowing when
something is wrong.

Everything here assumes the shape `coolify.md` set up: one Coolify compose resource running
`compose.prod.yml` from this repo, images pulled from public GHCR packages, Postgres as a separate
Coolify-managed resource on version 15, and no published host ports.

**The short version of the invariants**, because every procedure below respects them:

- **One API container, forever.** `AddAsyncDaemon(DaemonMode.Solo)` and the startup DDL both
  depend on it. Never scale the API.
- **The API owns the schema.** It applies every Marten change on startup
  (`Marten__ApplySchemaOnStartup=true`) before Kestrel opens its port. Nothing else migrates.
- **`/keys` is load-bearing.** Lose that volume and everybody is signed out.
- **`/healthz` on the API is host-filtered.** A probe without the right `Host` header gets 400.
- **Nothing on the server builds.**

---

## 1. Deploying a new version

### What a release looks like, end to end

| | |
|---|---|
| 1 | PRs merge into `dev`. Each merge that touches a watched path runs `.github/workflows/images.yml`, which publishes `:edge` and `:sha-<short>` to both GHCR packages. **Nothing deploys.** `:edge` is a thing you can pull and test; it is not what production runs |
| 2 | `release.yml` runs on the same pushes and keeps a **release PR** open, with release-please's changelog and version bump, plus `scripts/sync-version.mjs` propagating the version into the API's csproj |
| 3 | **You merge the release PR.** That is the deploy trigger, and the only one |
| 4 | On that push, `release.yml`'s `release` job creates the git tag and the GitHub Release, and then — in the *same run*, because a release created with the default `GITHUB_TOKEN` does not trigger other workflows — its `images` job calls `images.yml` with the version |
| 5 | `images.yml` publishes `1.2.3`, `1.2`, `latest` and `sha-<short>` for both images, and prints each one's digest in the run summary under **"Pin this:"** |
| 6 | Its `deploy` job POSTs Coolify's deploy webhook with `force=true`, which is what makes Coolify re-pull a moving tag instead of reusing the image already on the box |
| 7 | Coolify pulls `:latest` (`pull_policy: always`), **stops** the old containers and **starts** the new ones. This is stop-then-start, not a rolling swap, so there is a short outage — seconds to about a minute — and an old and a new container never overlap on the schema |
| 8 | The API applies any new Marten schema, then opens its port. The web container starts. Both go healthy |

**You will see two `Images` runs on that push.** One from the direct `push: dev` trigger (publishing
`edge` + `sha-…`) and one through `workflow_call` from `release.yml` (publishing the semver tags +
`latest`). That is expected: the concurrency key includes the version precisely so they do not
cancel each other, they publish disjoint tag sets, and the second reuses the first's
`:buildcache`. Do not "fix" it.

### Afterwards, every time

```sh
curl -s https://api.takeinitiative.<domain>/healthz            # 200
curl -sI https://takeinitiative.<domain>/      | head -1       # 200
curl -sI https://takeinitiative.<domain>/login | head -1       # 200
```

Then open `/`, `/login` and `/app` in a browser. **Check `/` and `/login`, not just `/app`** — they
are server-rendered and have their own failure mode, and `/healthz` answers before any page
renders, so a health check cannot see them break. A bug that did exactly that was fixed in
`d6f7f53`, *"fix(web): declare pinia, so production SSR stops returning 500"* — production SSR
returned 500 on every server-rendered route while `/app` and `/healthz` were both perfectly fine.

And confirm you are **still signed in**. If a deploy signs you out, the `/keys` volume has gone
missing; see `coolify.md` step 12.4.

### Deploying without a release

Sometimes you want a specific build on production — testing a fix, or a hotfix that has not been
released. Set `API_IMAGE` or `WEB_IMAGE` to that build's reference and redeploy, exactly as for a
rollback (section 2). **Then remember to clear it**, or the next release will publish into a
production that is pinned to an old image and nobody will understand why.

---

## 2. Rollback

`compose.prod.yml` takes `API_IMAGE` and `WEB_IMAGE` as **whole image references** rather than as
`<repo>:${TAG}`. That is deliberate: a whole reference can be a digest, and a digest is the only
form that cannot move.

| Tag | Published when | Moves? |
|---|---|---|
| `sha-<short>` | every build | **never** — readable, and safe to pin |
| `1.2.3` | a release | never |
| `1.2` | a release | yes, within the minor |
| `latest` | a release | yes |
| `edge` | every push to `dev` | yes |
| `…@sha256:…` | — | **never, by construction** |

### The steps

1. **Pick a known-good reference.** A digest if you want certainty, a `sha-…` tag if you want to be
   able to read it later. Section 2's next subsection is how to find one.
2. In Coolify, open the compose resource → **Environment Variables**, and add (or uncomment):

   ```
   API_IMAGE=ghcr.io/pi-gorbo/takeinitiative-api@sha256:<64 hex>
   WEB_IMAGE=ghcr.io/pi-gorbo/takeinitiative-web@sha256:<64 hex>
   ```

   Set only the one you are rolling back if only one is at fault — they are independent variables.
   **But prefer rolling both to the same release**: the web app is generated against the API's
   OpenAPI document, so a web/API pair from one release is a combination that has actually been
   tested together.

   Leave **"Is build variable?" off**, as with everything else.
3. **Redeploy.** Watch the log for `Pulling from` with the digest you pinned.
4. **Verify**: the three URLs from section 1, and that you are still signed in.
5. **When the fix ships, remove the pin** and redeploy. A pin is something somebody has to remember
   to remove — that is why `production.env.example` ships both variables commented out, and why the
   steady state is unset.

Note how long the whole thing took, the first time you rehearse it. Knowing that the number is "two
minutes" and not "I have no idea" is most of the value of having rehearsed it.

### Finding the digest of a known-good build

Four routes, best first:

```sh
# 1. Anonymous, from anywhere, no login — the most reliable.
docker buildx imagetools inspect ghcr.io/pi-gorbo/takeinitiative-api:1.2.3
#    prints:  Name: ghcr.io/…:1.2.3
#             Digest: sha256:…
```

```sh
# 2. On the VPS, for whatever is currently running or was recently pulled.
docker image inspect ghcr.io/pi-gorbo/takeinitiative-api:latest --format '{{.RepoDigests}}'
#    An empty result would mean the image was built locally, not pulled. It should never be empty.
```

3. **The Actions run summary.** Every `Images` run prints, per image, a block headed **"Pin this:"**
   containing `ghcr.io/…@sha256:…`, plus the list of tags it pushed. Find the run for the release
   you want:

   ```sh
   gh run list --workflow=images.yml --limit 15
   gh run view <run-id> --web       # the summary is on the web page, not in `gh run view` output
   ```

4. **The package's versions page** on GitHub: *repo → Packages → the package → the version list.*
   Every digest ever pushed is there, with its tags.

### What a rollback across a schema change does

Weasel (Marten's schema differ) uses `CreateOrUpdate`: it adds columns and indexes and **never
drops**. So an older image started against a newer schema generally still runs — the extra columns
are simply ignored. That is why a rollback is a normal operation here and not a crisis.

Where it stops being true: a renamed document type, a changed projection, or anything that makes an
old reader unable to interpret stored data. There is no automated protection against that; if a
release's notes mention a projection rebuild, assume the rollback needs a database restore too, and
read section 3 before you touch anything.

---

## 3. Backups

### What is actually irreplaceable

| | Rebuildable? | |
|---|---|---|
| The two app images | **yes** — they are in GHCR, from a public repo | Nothing to back up |
| The knowledge base (`knowledge_base_item`) | **yes** — re-run the ingest from the 5eTools folder on your machine (section 4) | Nothing to back up |
| `takeapi-keys` (`/keys`) | **yes** — a new key ring generates itself | Losing it signs everybody out once. Annoying, not data loss |
| **Postgres** | **no** | **The event store is the campaign.** Every note, every session, every roll, every account |
| **`takeminio-data`** (`/data`) | **no** | **Every photo anybody has ever uploaded.** Nothing else on the box holds them |

The plan's framing is that Postgres is the thing that cannot be rebuilt, and that is the right
emphasis — but **the bucket is the second irreplaceable thing and the plan does not specify a backup
for it at all.** Section 3.3 is written to fill that gap, and section 3.5 is honest about the rest
of what is unspecified.

### 3.1 The first line: Coolify's scheduled backups

The Postgres resource has a Backups section with a cron schedule, a retention count and a choice of
destination (local disk or an S3 bucket). `coolify.md` step 4.6 turns it on. Two things to
understand about it:

- **A backup on the same disk as the database is not a backup.** It survives a bad migration and a
  dropped table. It does not survive a dead VPS, a deleted server, or a ransomware day.
- **Point it off the box if you can.** If you have any S3-compatible destination — the same
  Cloudflare R2 account you might have used for the bucket, Backblaze B2, anything — use it. That
  single setting is the largest single improvement available to this deployment.

> **Decision, yours to make: the backup destination.**
> **Recommendation:** Coolify's scheduled backup to an **off-box S3 destination**, daily, retaining
> 7 dailies. If you will not set up an S3 destination, then: Coolify's scheduled backup to local
> disk daily, **plus** a weekly off-box pull using 3.2 below, **plus** a recurring calendar
> reminder, because a manual backup with no reminder is a backup that happened twice.
> The plan does not specify a destination, a retention policy, or encryption at rest for backups.
> Whatever you choose, write it down here.

### 3.2 Pulling a dump off the box yourself

Postgres has no published port, by design. Reach it over an SSH tunnel.

**The tunnel, and the thing that trips people up.** `ssh -L` resolves the forwarding destination
*from the VPS*, and the VPS host **cannot resolve Docker container names** — Docker's embedded DNS
at `127.0.0.11` only exists inside containers. So `ssh -L 55432:<pg-internal-host>:5432` fails with
a name resolution error even though that hostname works perfectly from inside a container. Container
*IPs*, however, are routable from the host. So:

```sh
# 1. Find the Postgres container's IP, from the VPS.
ssh <user>@<vps> 'docker ps --format "{{.Names}}" | grep -i postgres'
ssh <user>@<vps> 'docker inspect -f "{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}" <pg-container>'

# 2. Tunnel to that IP. -N: no remote command, just the forward. Leave it running.
ssh -N -L 55432:<pg-container-ip>:5432 <user>@<vps>
```

**That IP changes whenever the container is recreated** (a Postgres version change, a resource
edit, sometimes a host reboot), so re-read it rather than saving it. If you would rather not deal
with that, the alternative is to run the dump entirely on the VPS and copy the file down (3.2b).
Coolify can also expose the database on a public port — **do not**, unless you firewall it to your
own IP; an internet-reachable Postgres is a bad trade for the convenience.

**The dump.** Use a Postgres **15** client, or `pg_dump` will refuse on a server/client version
mismatch. The easiest way to guarantee that is to run the client in a container:

```sh
# macOS / Docker Desktop. On Linux add: --add-host=host.docker.internal:host-gateway
docker run --rm -v "$PWD:/out" -e PGPASSWORD='<password>' postgres:15-alpine \
    pg_dump -Fc --no-owner --no-privileges \
        -h host.docker.internal -p 55432 -U '<user>' -d '<db>' \
        -f "/out/takedb-$(date +%F-%H%M).dump"
```

`-Fc` is the custom format, which `pg_restore` can read selectively and which compresses. Or
install a local client (`brew install postgresql@15`) and drop the `docker run` wrapper.

**3.2b — without a tunnel**, if the container-IP dance is annoying:

```sh
ssh <user>@<vps> 'docker exec -e PGPASSWORD="<password>" <pg-container> \
    pg_dump -Fc --no-owner --no-privileges -U "<user>" -d "<db>"' > "takedb-$(date +%F-%H%M).dump"
```

The dump streams over SSH and never lands on the VPS's disk. This is the simpler command and the
one worth putting in a cron job on your own machine.

### 3.3 The bucket

Not specified by the plan. The straightforward route is to copy the Docker volume, from the VPS:

```sh
docker volume ls | grep takeminio-data          # Coolify may prefix the name; use the real one

docker run --rm -v <real-volume-name>:/data:ro -v /tmp:/backup alpine \
    tar czf "/backup/minio-$(date +%F).tar.gz" -C /data .
```

then pull it down with `scp`. Restoring is the same `tar` in reverse into a fresh volume.

**Be aware this is a hot copy.** MinIO writes each object as immutable files on disk, so objects
that were already fully uploaded come across intact; an upload in flight at that moment may not.
For four friends that is an acceptable trade. The only fully consistent copy is one taken with the
`minio` container stopped, which costs a minute of "photos do not load" and is worth doing for the
copy you actually care about.

An `mc mirror` out of the running container would be tidier, but the `pgsty/minio` rebuild's
tooling and entrypoint are not something this file has verified — **unverified**; use the volume
tar, which depends on nothing but `alpine`.

> **Decision, yours to make.** If this is the part you would rather not operate, **Cloudflare R2**
> is the alternative: set `Blobs__ServiceUrl` to the R2 endpoint, `Blobs__Region=auto`,
> `Blobs__CreateBucket=false`, and delete the `minio` service and its volume. It is free at this
> scale, needs no ops, and stops your friends' photos living on a single unbacked disk — at the
> cost of a Cloudflare account with a payment method on file, which brushes against the project's
> "no paid services" invariant. **Recommendation: stay on MinIO** until the volume tar above starts
> feeling like a chore, then move.

### 3.4 Testing a restore

**A backup nobody has restored is not a backup.** Do this once now, and once after any change to
how backups are taken.

```sh
# 1. A throwaway Postgres 15, on a port nothing else uses.
docker run -d --name ti-restore -p 7501:5432 \
    -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=takeinitiative postgres:15-alpine

# 2. Restore into it, as superuser so CREATE EXTENSION is allowed.
#    --no-owner --no-privileges because the production role names do not exist here.
docker run --rm -v "$PWD:/in" --add-host=host.docker.internal:host-gateway \
    -e PGPASSWORD=postgres postgres:15-alpine \
    pg_restore --no-owner --no-privileges \
        -h host.docker.internal -p 7501 -U postgres -d takeinitiative \
        /in/takedb-<the-file>.dump
```

Then **check it, in three ways that each catch something different:**

```sql
select extname from pg_extension;                 -- pg_trgm and unaccent must be there
select count(*) from mt_events;                   -- the event store: compare against production
select count(*) from mt_doc_campaign;             -- and a projection
select count(*) from knowledge_base_item;         -- re-ingestable, but it should be here
```

And then the real test — **start the API against it**, because a restore that Postgres accepts but
Marten cannot read is not a restore:

```sh
cd apps/TakeInitiative.Api
ConnectionStrings__TakeDB='Host=localhost;Port=7501;Database=takeinitiative;User ID=postgres;Password=postgres;' \
    dotnet run
```

Sign in with a real account and open a campaign. Then clean up:

```sh
docker rm -f ti-restore
```

### 3.5 What is not backed up, said plainly

Nothing on the VPS except what the commands above copy. Specifically, as the plan stands:

- **No point-in-time recovery.** There is no WAL archiving, so the worst case is losing everything
  since the last scheduled dump — up to a full day on a daily schedule.
- **No backup of the bucket** is specified by the plan, scheduled, or automated. Section 3.3 is a
  command you have to run.
- **No verification that a backup succeeded**, beyond whatever Coolify notifies you about. A
  scheduled backup that has been silently failing for a month is a real and common way to lose
  data.
- **No encryption of backups at rest**, and no stated retention policy.
- **`takeapi-keys` is not backed up.** That is fine — losing it is a one-time mass sign-out — but
  know that is what it costs.
- **No stated recovery objective.** Realistically: restoring from a dump into a fresh Coolify
  Postgres resource is an hour of careful work, most of it waiting.

---

## 4. The knowledge-base ingest

Step 26's ingest is a **consumer** of this deployment, not part of it. The operator runs
`apps/TakeInitiative.KnowledgeBase.Cli` from **their own machine** against the production database
over an SSH tunnel, because the operator is the person who has the 5eTools data folder and because
nothing in this repo downloads book content. No image carries the corpus and nothing is fetched at
runtime.

### Before you run it

- **The API must have started against that database at least once.** The API owns the schema; the
  CLI writes rows and nothing else. If the table is missing the CLI fails cleanly with a
  schema-missing message rather than creating anything.
- **Have the 5eTools data folder** — a checkout, or its `data/` directory. The CLI needs
  `bestiary/index.json` and `spells/index.json` to exist, and reports a *partial folder* if they do
  not, because the parser's own error for that case reads like a missing optional file.

### The tunnel

Same mechanics and same gotcha as section 3.2 — `ssh -L` resolves from the VPS, which cannot
resolve container names, so tunnel to the container's **IP**:

```sh
ssh <user>@<vps> 'docker inspect -f "{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}" <pg-container>'
ssh -N -L 55432:<pg-container-ip>:5432 <user>@<vps>
```

Leave that running in its own terminal. The connection string for the CLI is `TAKEDB_CONNECTION`
with the host and port rewritten to the local end of the tunnel.

### The commands

Put the connection string in the environment rather than on the command line, so the production
password does not end up in your shell history:

```sh
export KnowledgeBase__ConnectionString='Host=localhost;Port=55432;Database=<db>;User ID=<user>;Password=<password>;'

# 1. ALWAYS dry-run first. Prints the diff and writes nothing at all.
dotnet run --project apps/TakeInitiative.KnowledgeBase.Cli -- ingest \
    --from ~/5etools-src/data --dry-run

# 2. If the diff looks right, run it for real.
dotnet run --project apps/TakeInitiative.KnowledgeBase.Cli -- ingest \
    --from ~/5etools-src/data
```

The diff it prints before writing:

```
5etools · ~/5etools-src/data
  parsed   2,847 items  (2,103 monsters · 618 spells · 126 items)
  new         12
  updated      3
  unchanged 2,832
  missing      0   (in the database, absent from this source)
```

Read `missing` before you read anything else. A large `missing` on a folder you expected to be
complete means you pointed the tool at the wrong place.

### The safety behaviour, and why you can run this without fear

- **A plain run is additive.** It inserts and updates and has **no way to delete**. Point it at one
  bestiary file instead of thirty and the worst outcome is rows that are out of date — not an
  entry's link that broke.
- **`--prune` is what deletes**, opt-in, and even then:
  - **a row any entry links to is never deleted.** It is marked `stale = true` and the entry's link
    still resolves, rendering as *"this link's source is no longer in your data"*. A link is
    something a user made; the ingest does not get to erase it.
  - **a prune that would remove more than 20% of a provider's rows refuses** without `--force`, and
    the whole run is rolled back. Mass deletion is the signature of pointing the tool at the wrong
    folder, and that is exactly the moment to need a second keystroke.
  - These rules live in `KnowledgeBaseStore`, not in the CLI, so a second caller cannot bypass
    them.
- **Exit codes**, for scripting: `0` done or nothing to do, `1` a parse failure with nothing
  written, `2` a prune refused by the threshold. The two failure codes are distinct on purpose: `1`
  means *fix the data*, `2` means *look at what you pointed me at*.

### The other flags

| Flag | For |
|---|---|
| `--connection` | the connection string, if you would rather not use the env var |
| `--provider` | which corpus these rows belong to; defaults to `5etools` |
| `--prune` / `--force` | see above |
| `--min-monsters` | the floor the parse refuses to go under (default 1000), as a guard against the wrong folder. Lower it **only** to ingest a small test corpus |

### If something goes wrong

- *"no connection string"* / *"no source folder"* — nothing was written. The flags or the env var.
- A **schema-missing** error — the API has never started against this database. Deploy, let it come
  up healthy, retry.
- A connection or socket error — the tunnel is not up, or the container IP changed. Re-read it.
- A **partial folder** report — the data folder is missing one of the two required `index.json`
  files. Point it at the right directory.
- Exit `2` — a prune refused. Nothing was deleted; the run rolled back. Check what you pointed it
  at before reaching for `--force`.

---

## 5. Watching it

### The honest position

**There is no uptime monitoring in this deployment, and the plan says so on purpose.** No alerting,
no log shipping, no metrics, no error tracking. For a group of four friends, *"my friend says it's
down"* is a perfectly good monitor, and every piece of infrastructure you add is infrastructure you
have to operate.

What you do get for free, and should turn on if `coolify.md` step 1.2 was skipped: **Coolify's own
notifications** (email, Discord, Telegram or a generic webhook, under its global settings). Enable
deployment-failure notices at minimum. That covers the most likely bad day — a release that did not
come up — and costs nothing.

### The cheapest sensible addition

An external HTTP check, from outside the VPS, on a schedule. Either:

- **self-hosted Uptime Kuma**, as another Coolify resource on the same box — free, five minutes to
  set up, and it can notify the same Discord webhook. Its weakness is obvious: it is on the box it
  is watching, so it cannot tell you the box is gone.
- **a free hosted ping** (there are several with a free tier adequate for two URLs) — it *can* tell
  you the box is gone, which is the failure that matters most.

Whichever you pick, **check `https://takeinitiative.<domain>/` and not only `/healthz`.**

That is not fussiness. The web `/healthz` is a Nitro route that answers before any page renders, so
it stays green while the server-rendered pages 500 — which is exactly the bug that shipped and was
fixed in `d6f7f53` (an undeclared `pinia` dependency). A check on `/` that asserts a 200 **and** a
string from the landing page's
content catches a class of failure that a liveness probe structurally cannot.

The API side is simpler than it looks: `https://api.takeinitiative.<domain>/healthz` works fine
from an external monitor, because Traefik passes the real `Host` header. The host-filtering trap
only affects probes made *inside* the container.

> **Decision, yours to make.** **Recommendation:** Coolify notifications now (two minutes), plus one
> external check on `https://takeinitiative.<domain>/` whenever you next have an idle half hour.
> Skip metrics, log shipping and error tracking until something has actually gone wrong twice.

---

## 6. The logs you will actually want

### Reading them

**In Coolify**, on the compose resource:

- a **Logs** tab, with a picker for which container's output to stream — `api`, `web` or `minio`.
  This is the application log.
- a **Deployments** / deployment-history view, where each deploy has its own log. This is where
  `Pulling from` lives, and where a failed deploy explains itself.

Those are two different things, and the mistake is reading the deployment log when you want the
application log. If a container is up and misbehaving, you want the Logs tab.

**On the box**, which is faster and sometimes necessary:

```sh
docker ps --format '{{.Names}}\t{{.Status}}'          # who is up, and healthy or not
docker logs -f --tail 200 <api-container>
docker logs -f --tail 200 <web-container>
docker inspect --format '{{json .State.Health}}' <api-container>   # the last healthcheck outputs
```

The API logs through Serilog to the console at `Information` and above, so the output is sparse by
design — an absence of lines is not an absence of health.

### What a healthy API startup looks like

The sequence to expect:

1. **Nothing much, for a while.** Marten applies every database change on startup. It does not log
   each DDL statement at `Information`, so on a fresh database this looks like a pause — tens of
   seconds is normal, and it is why the compose healthcheck has a 60-second `start_period`.
2. Then Kestrel's startup lines:

   ```
   Now listening on: http://[::]:8080
   Application started. Press Ctrl+C to shut down.
   Hosting environment: Production
   Content root path: /app
   ```

   **`Hosting environment: Production` is worth reading every time.** If it ever says anything else,
   `ASPNETCORE_ENVIRONMENT` has been set, which means Swagger is exposed and the auth cookie is no
   longer unconditionally `Secure`.
3. Then the container goes healthy and the async daemon starts working through projections.

The thing that matters is **the DDL finishing before the port opens**, which is the ordering the
deployment relies on: a request never races a migration, and Coolify's stop-then-start means an old
and a new container never overlap on the schema. In practice, read the log for *an absence of
exceptions, followed by `Now listening on`*. If you ever see those two interleaved differently than
described, the hosted-service ordering has changed; it is not worth chasing, because the generous
`start_period` is what actually protects the deploy.

### Lines that mean something is wrong

| In the log | What it is |
|---|---|
| `Npgsql… Name or service not known` / `No such host is known` | the API cannot resolve Postgres. "Connect to predefined network" is off, or the internal hostname is wrong. `coolify.md` 12.2 |
| `Npgsql… password authentication failed` / `database "…" does not exist` | the connection string's credentials do not match what Coolify generated |
| anything about **not being able to persist keys** / an **ephemeral key ring** | the `/keys` volume. Everyone is about to be signed out on the next deploy. `coolify.md` 12.4 |
| `NoSuchBucket`, `SignatureDoesNotMatch`, `The AWS Access Key Id you provided does not exist` | the bucket. All three read like a credentials problem whichever cause it is. `coolify.md` 12.5 |
| `function word_similarity(…) does not exist` at search time | `pg_trgm` was never created, which means the schema was not applied on startup |
| a healthcheck log showing curl exit 22 against `/healthz` while the app works on its domain | the host-filtered probe. `coolify.md` 12.1 — and read that before touching any healthcheck on this API |
| a stack trace in the **web** container, with `/` or `/login` in it | a server-rendered page failing. Nothing in the health probes sees this |

### A note for whoever edits a healthcheck next

The API's `/healthz` is behind ASP.NET Core's host filtering, which is active whenever
`AllowedHosts` is set — and production sets it. A probe without a matching `Host` header gets
**400**, `curl --fail` fails on a 400, `restart: unless-stopped` flaps the container forever, and
**Coolify reports a failed deploy while the API behind it is working perfectly.** Every log line
looks fine.

Both the image's built-in `HEALTHCHECK` and `compose.prod.yml`'s override already handle it, by
taking the first `;`-separated name out of the container's own `AllowedHosts` and presenting that,
falling back to `localhost`. One source of truth, nothing to keep in sync. **If you simplify either
of them to a plain `curl http://localhost:8080/healthz`, you will spend an afternoon chasing a
phantom failure.**
