# The deploy pipeline

Operating `.github/workflows/deploy.yml`: what to set up once, how to run it, and what to do when it
goes wrong.

This is Ripple's release pipeline, ported. Same tailnet, same Coolify, same primitive — a deploy is
*pinning a Coolify application to an image digest and waiting for it*. Ripple's own runbook
(`/projects/Ripple/docs/knowledge/deployment/release-pipeline-runbook.md`) is the longer document and
most of its reasoning applies unchanged; this file records what is **different here** and the
one-time setup that is specific to this repo.

---

## The shape, in one pass

```
dev ──► release-please opens "chore(dev): release takeinitiative 1.1.0"
          │
          │ you merge it
          ▼
        release.yml ──► images.yml publishes 1.1.0, 1.1, latest, sha-<short>
          │
          │ merge-to-main.yml opens "release: deploy the release from dev to main"
          │ you merge it  ◄── THIS is the shipping decision
          ▼
        main push ──► deploy.yml
                        plan    resolve 1.1.0 ──► sha256:… (anonymous, from GHCR)
                        deploy  join tailnet ──► gate on Coolify ──► PATCH digest ──► poll
                                then tag that digest `prod`
```

Three things about this are worth stating because they are the design, not accidents.

**Nothing builds on `main`.** release-please publishes an immutable `1.1.0` tag from `dev`, and
`main` resolves that tag to the digest it already points at. The version tag never moves, so it
already answers "are these the bytes `dev` ran?" — which is the only question Ripple's
`image-key.ts` content-addressing exists to answer. Ripple needs the hashing because it deploys
every `dev` push, where no release version exists yet. We do not.

**One environment.** A dev environment was judged not worth a second VPS. So `main` is the only
deploying branch, `dev` publishes images and deploys nothing, and `deploy-targets.json` has a single
`prod` entry. Ripple's dev/prod credential split — where the dev Tailscale client genuinely cannot
mint a prod tag — has nothing to split here.

**No migrate job.** The API applies the Marten schema at boot, before the port opens
(`Marten__ApplySchemaOnStartup=true`), which is safe because `AddAsyncDaemon(DaemonMode.Solo)`
already means exactly one API container, forever. Ripple's `migrate` job and its per-environment
database Tailscale Service have no counterpart here, which also means **CI never reaches the
production database** — the only thing it reaches over the tailnet is Coolify.

> **When boot migration stops being the right answer.** It is fine while a deploy is one container
> replacing one container and a bad schema change means a failed boot and a rollback. It stops being
> fine the moment you want zero-downtime deploys (the old container is serving against the new
> schema), or more than one API replica (two racing migrators), or a migration that rewrites data
> rather than defining it. At that point the migrate job is the thing to port, and Ripple's is
> written to be copied: it joins the tailnet, gates on reaching the database, and runs before the
> deploy. Until one of those three is true, a separate job buys nothing and adds a credential.

## One-time setup

Nothing in this list is reversible-by-accident, and the whole pipeline ships inert:
`deploy-targets.json` has both apps `enabled: false`, so until you flip them a push to `main` plans,
reports "skipped: disabled in deploy-targets.json" and exits 0.

### 1. Two Coolify applications

This is the one structural difference from `docs/deploy/coolify.md`, which was written for a single
git-backed **Docker Compose** resource. The pipeline pins
`docker_registry_image_name` / `docker_registry_image_tag`, and those fields exist on a Coolify
**Application** sourced from a docker image — not on a compose resource. So:

| Resource | Kind | Image source | UUID |
|---|---|---|---|
| `takeinitiative-api` | Application, "Docker Image" | `ghcr.io/pi-gorbo/takeinitiative-api` | `lvb5hpab49yh618nttvev1fz` |
| `takeinitiative-web` | Application, "Docker Image" | `ghcr.io/pi-gorbo/takeinitiative-web` | `m1kq4p6ckky1ldfssvio5l6d` |
| Postgres 15 | Coolify-managed database | — | — |
| MinIO | Application or one-click service | `pgsty/minio`, pinned as `compose.prod.yml` records | — |

> **Both applications exist and their UUIDs are committed.** They were created undesignated, so
> **which one is the API and which is the web app is decided by `deploy-targets.json`**, not by
> Coolify — the table above is now the authority and Coolify has to be made to agree.
>
> Do two things in Coolify before enabling either: **name them to match** (`takeinitiative-api`,
> `takeinitiative-web`), and **set each one's image source to the matching GHCR repository**.
> Getting this backwards is the one mistake the pipeline cannot catch for you: it would PATCH the
> web image onto the API's application and deploy it *successfully*, leaving two containers each
> running the wrong thing behind the right domain. The symptom is a 404-ish mess on both hostnames
> with two green deploys in the log.
>
> What the pipeline *does* catch: a UUID that is empty while the app is enabled, and the same UUID
> used for both apps. Both fail the plan job, and both are tested.

What moves out of `compose.prod.yml` and where it goes:

- **Environment variables** → onto each Application, per `docs/deploy/production.env.example`. The
  API's list is the long one. `Is build variable?` stays **off** for every single one; nothing here
  builds.
- **`Blobs__ServiceUrl`** → no longer `http://minio:9000`. With MinIO as its own resource it is
  reached by that resource's internal hostname, exactly as `TAKEDB_CONNECTION` already is. Both
  resources need **"Connect to predefined network"** enabled or neither name resolves.
- **Volumes** → Coolify "Persistent Storage" entries: `/keys` on the API (the data-protection key
  ring — delete it and everybody is signed out on every deploy) and `/data` on MinIO (every image
  anyone has ever uploaded).
- **Healthchecks** → nowhere. Both images carry their own `HEALTHCHECK`, including the API's
  `Host:`-header trick for host-filtered `/healthz`, so **leave Coolify's healthcheck disabled** and
  let the image's own probe be what `running:healthy` means. This is why the split cost nothing
  here; it is the part most likely to have been painful.
- **`depends_on: minio: service_healthy`** → nothing enforces it any more. `BlobBucketInitializer`
  is idempotent and retries for 30 s, so a cold boot where MinIO is slower than the API recovers on
  the API's next restart. Worth knowing on the very first deploy.
- **`pull_policy: always`** → irrelevant. A digest reference is immutable; there is no stale tag to
  re-pull.

`compose.prod.yml` stays in the repo, and it is still useful — it is the way to run the production
images locally with production-shaped config before cutting a release. Its header now says so. It is
simply no longer what Coolify runs.

The UUIDs are already in `deploy-targets.json`. **`enabled` is still `false` for both, and that is
the only thing standing between a push to `main` and a real deploy** — so flip it last, after the
applications are configured and steps 2–6 below are done. A disabled app with a UUID is a valid,
inert state; an app that is `enabled` with no UUID **fails the plan job** rather than deploying
nothing quietly. That asymmetry is deliberate and is tested.

The recommended order for arming it, including a first run that changes nothing, is in
**[Operating it](#operating-it)** — set the api application's image to the digest it is already
running by hand, enable `api` alone, and dispatch with that tag. A correct pipeline then reports
`already running this digest and healthy` and deploys nothing, which proves the credentials, the
tailnet, Coolify reachability and the digest arithmetic in one run without touching production.

### 2. One new Tailscale tag

`tag:ci-takeinitiative-prod`, listed in `tagOwners` in the tailnet policy.

Project-scoped on purpose, following Ripple's reasoning: a grant carries access to a project's
resources, so a project-agnostic `tag:ci-deploy-prod` handed to another repo's CI would inherit
whatever this one can reach. The service names run the other way — `svc:rpi-coolify` is one control
plane shared by every project, so it takes no project prefix.

### 3. One grant, to a service that already exists

No new Tailscale Service. CI here reaches exactly one thing, and Ripple already advertises it:

```jsonc
{ "src": ["tag:ci-takeinitiative-prod"], "dst": ["svc:rpi-coolify"], "ip": ["443"] }
```

ACLs are default-deny, so `tagOwners` plus that one grant is the whole policy change. There is no
`svc:takeinitiative-prod-db` and deliberately no grant to any database.

> Everything in Ripple's `docs/knowledge/tailscale-services-guide.md` about diagnosing this still
> applies, and the headline is worth repeating: **nearly every misconfiguration presents as a
> connection timeout**, because the faults are all "no route" rather than "no listener". The deploy
> job's reachability gate exists to tell those apart — it prints the peer list,
> `tailscale dns status` and `/etc/resolv.conf` before it fails, and says whether resolution or
> connection broke.

### 4. One new Tailscale OAuth client, federated

A client that can mint `tag:ci-takeinitiative-prod`, with the writable `auth_keys` scope. The tag
must already be in `tagOwners` or the client cannot issue a key for it.

Federate it to trust **only** this repo's prod Environment:

```
repo:PI-Gorbo/TakeInitiative:environment:prod
```

GitHub's OIDC `sub` claim carries `environment:prod` verbatim when a job declares that Environment.
**That claim is the Environment's name exactly**, so three statements have to agree:

| Where | Value |
|---|---|
| `.github/workflows/deploy.yml`, the deploy job's `environment:` | from `githubEnvironment` |
| `deploy-targets.json`, `environments.prod.githubEnvironment` | `prod` |
| the federated identity's subject condition | `…:environment:prod` |

Whichever disagrees is the one to change. A mismatch surfaces as an auth rejection on the tailnet
join, which looks nothing like a network or DNS fault — so if the join fails before the reachability
gate even runs, check this before touching ACLs.

The workflow selects federation by passing `audience`; there is no `use-oidc` input. Workload
identity federation needs Tailscale 1.90.1 or later. If subject-matching will not express the
condition, fall back to `oauth-secret` with the client secret as a `prod` Environment secret — most
of the benefit, one more credential.

### 5. The GitHub `prod` Environment

Named `prod`, to match `githubEnvironment`. **No required reviewers** — a solo prod-only setup gets
little from a gate it always approves, and the plan summary is readable after the fact. The
Environment still has to exist: it is what puts `environment:prod` in the OIDC claim and what scopes
the two secrets below.

| Name | Kind | Scope | Notes |
|---|---|---|---|
| `TS_OAUTH_CLIENT_ID` | secret | Environment `prod` | The client from step 4 |
| `TS_AUDIENCE` | secret | Environment `prod` | The federation audience. Not really a credential, but scoped alongside the client id |
| `COOLIFY_API_TOKEN` | secret | Environment `prod` | See below |
| `COOLIFY_BASE_URL` | **variable** | repository | Must include `https://`. A variable, not a secret, because masking a hostname turns every failure into `***` and leaves nothing to debug |

`COOLIFY_BASE_URL` being a variable is a lesson Ripple paid for over two debugging runs. Do not
"harden" it into a secret.

Nothing else is needed. There is **no registry secret anywhere**: `GITHUB_TOKEN` covers the push in
`images.yml`, and the packages are public, so `scripts/ci/ghcr.ts` resolves a digest with an
anonymous pull token. The old `COOLIFY_DEPLOY_URL` / `COOLIFY_TOKEN` repository secrets are
superseded and can be deleted once the first pinned deploy has worked.

### 6. The Coolify token

**Reusing Ripple's prod token works only if TakeInitiative's resources live in the same Coolify
team.** A Coolify token is scoped to one team and to nothing finer — there is no per-project or
per-application scoping. So check which team the two Applications end up in:

- **Same team as Ripple prod** → the existing token works as-is. The cost is that this repo's CI can
  deploy Ripple's production applications and vice versa. For a solo hobby setup that is a real but
  acceptable blast radius, and it is the honest reason to mint a second token anyway: they rotate
  independently.
- **Its own team** → the existing token cannot see these applications at all and you need a new one.
  This is also the only arrangement that makes the token boundary match the project boundary, which
  is what Ripple's runbook recommends considering.

Either way, give it `read`, `write` and `deploy`. **Not `root`**, and not `read:sensitive` — the
pipeline reads only the image fields and `status`, never environment variables or secrets.

> **Digest mode is not configurable here, unlike in Ripple.** Ripple's `coolify-deploy.ts` carries a
> `COOLIFY_REFERENCE_MODE` of `digest` or `tag`, because at the time it was unproven that the
> instance would accept a digest in the image reference. It does — verified against this same
> Coolify instance during Ripple's rollout — so the port keeps only `digest` and the pipeline has
> one less variable to be set wrong. If a future Coolify ever rejects the
> `name=<repo>@sha256` / `tag=<hex>` split, re-adding the fallback is a small change to
> `referenceParts` and its tests say what the two shapes are.

| Call | Needs |
|---|---|
| `GET /api/v1/applications/{uuid}` | `read` |
| `PATCH /api/v1/applications/{uuid}` | `write` |
| `POST /api/v1/deploy?uuid=` | `deploy` |
| `GET /api/v1/deployments/{uuid}` | `read` |

## Operating it

### Shipping a release

Merge the `release:` PR that `merge-to-main.yml` opened. That is the whole procedure, and it is the
only moment anything reaches production.

### Checking what is live, without opening Coolify

A successful prod deploy re-points a `prod` tag at the digest it just shipped, using
`docker buildx imagetools create` — which moves no bytes, so the digest cannot change. So the
registry answers it:

```sh
docker buildx imagetools inspect ghcr.io/pi-gorbo/takeinitiative-api:prod
```

That step is `continue-on-error: true` on purpose: the artefact is already live by then, and failing
the run would report a working deploy as broken when only its labelling is.

### Rolling back

```
Actions ▸ Deploy ▸ Run workflow
  pin_tag: 1.0.13          # or sha-cca7c15 for one specific build
```

`pin_tag` deploys an already-published tag **without rebuilding it** — rebuilding is what would make
it a different artefact. Only a release version (`1.2.3`) or a build tag (`sha-<short>`) is accepted:
`latest`, `edge` and `prod` are refused, because resolving a moving tag now does not stop it moving
before Coolify pulls, and the digest that shipped would not be the one the summary showed. That
refusal is tested.

Rolling back **across a schema change** is the case to think about before you need it. Marten applied
the new schema at the new version's boot; the old image then starts against a schema it did not
expect. Additive changes are usually fine and anything that removed or retyped a column is not. See
`docs/deploy/operations.md`.

### Forcing things

| Input | Does |
|---|---|
| `force_deploy: true` | Redeploys even when Coolify already runs this digest and is healthy |
| `targets: api` | Acts on one app instead of both |
| `pin_tag: …` | As above |

A plain push to `main` that is not a release merge resolves the version already running, matches, and
deploys nothing — "already running this digest and healthy". Pushing docs to `main` is free, by
design.

### When it fails

Read which stage failed first; each has one likely cause.

| Symptom | Cause |
|---|---|
| `plan` fails with "is not published" | The release never published images. Check the `release.yml` run actually reached its `images` job — it is gated on `release_created`, which is true only on the run where the release PR merges |
| `plan` fails with "has no coolifyApplicationUuid" | `enabled: true` was committed before the Coolify application existed. Fill the UUID in or set it back to `false` |
| The tailnet join fails | Almost always the federated subject not matching `repo:PI-Gorbo/TakeInitiative:environment:prod`, or the tag missing from `tagOwners`. Not a network fault, despite looking like one |
| The reachability gate fails on resolution | MagicDNS did not reach the runner, or no Service is advertised under that name. The gate prints `/etc/resolv.conf`; it needs `100.100.100.100` |
| The reachability gate fails on connection | The name resolved but the port refused. The Service is not advertised on that port, or `tag:ci-takeinitiative-prod` has no grant to it |
| Coolify returns 401/403 | `COOLIFY_API_TOKEN` is wrong, lacks `read`/`write`/`deploy`, or belongs to a different team than the applications |
| Coolify returns 404 | The UUID in `deploy-targets.json` is not an application on that instance |
| The deployment polls to `failed` | Coolify's own deploy log says why, in the dashboard over the tailnet. Most likely the image cannot be pulled, or the container exits on a missing env var |

A dropped poll is tolerated — only three consecutive failures fail the job, because Coolify is
reached over the tailnet and one lost request must not fail a deploy that is still progressing.

### Changing the scripts

`scripts/ci/` holds every decision the pipeline makes, and it is unit tested:

```sh
pnpm deploy:test        # node --test "scripts/ci/*.test.ts"
pnpm deploy:explain     # what the current checkout would deploy, against the live registry
```

`deploy:explain` needs no credentials, which is the point of resolving digests anonymously. CI runs
both in `testScripts.yml`.
