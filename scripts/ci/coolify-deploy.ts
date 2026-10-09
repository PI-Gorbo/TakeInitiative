/**
 * Pointing one Coolify application at a digest and waiting for the deploy.
 *
 * Ported from Ripple's `scripts/ci/coolify-deploy.ts`, which this repo's pipeline
 * deliberately mirrors. The port is close to verbatim because the Coolify API is
 * the same API; what differs is only that there is one environment here.
 *
 * Two calls, not one: a deploy without first setting the reference would redeploy
 * whatever the application was already configured with. Polling the returned uuid
 * is what lets this job fail when the deploy does.
 */

import process from "node:process";
import { fileURLToPath } from "node:url";

const POLL_INTERVAL_MS = 5_000;
const POLL_TIMEOUT_MS = 20 * 60 * 1000;

/**
 * Coolify is reached over the tailnet, so a single dropped request must not fail a
 * deploy that is still progressing. Only consecutive failures count.
 */
const MAX_CONSECUTIVE_POLL_FAILURES = 3;

export type ApplicationState = {
    /** The image Coolify is configured to run — the *intended* image, not the running one. */
    readonly configuredReference: string;
    readonly status: string;
};

export type ImageReferenceParts = {
    readonly name: string;
    readonly tag: string;
};

const DIGEST_PATTERN = /^(?:sha256:)?([0-9a-f]{64})$/;

/**
 * Coolify has no digest field. It stores an image as a name and a tag, joins them
 * with `:`, and expresses a digest by keeping `@sha256` on the *name* and the bare
 * hex in the *tag* — so `{name}:{tag}` composes `image@sha256:<hex>`. That is what
 * the UI's "SHA256 Digest" box actually writes, and it is why putting the whole
 * `image@sha256:<hex>` in the name leaves a trailing `:` from the empty tag.
 */
export const referenceParts = (
    repository: string,
    digest: string,
): ImageReferenceParts => {
    const hex = DIGEST_PATTERN.exec(digest)?.[1];
    if (!hex) {
        throw new Error(
            `Expected a sha256 digest, got "${digest}". Coolify needs the bare hex.`,
        );
    }
    return { name: `${repository}@sha256`, tag: hex };
};

export const referenceOf = (parts: ImageReferenceParts): string =>
    parts.tag === "" ? parts.name : `${parts.name}:${parts.tag}`;

/**
 * Coolify reports `running:healthy`, `running:unhealthy`, `exited:…` and similar.
 * Only a running *and* healthy application counts, because an unhealthy one is the
 * case a deploy has to recover from.
 *
 * Both images carry their own `HEALTHCHECK` (see `apps/TakeInitiative.Api/dockerfile`,
 * which derives the `Host:` header from `AllowedHosts` because `/healthz` is host
 * filtered), so this status is the image's own verdict and not a Coolify guess.
 */
export const isHealthy = (status: string): boolean =>
    status.startsWith("running") && !status.includes("unhealthy");

export type DeployDecision = {
    readonly deploy: boolean;
    readonly reason: string;
};

/**
 * The configured reference is the image Coolify *intends* to run, so matching it is
 * not on its own evidence that the artefact is live: a crash-looped or stopped
 * container would be silently skipped. Health is the second half of the condition.
 *
 * This is also what makes a non-release push to `main` cheap — it resolves the
 * version already running, matches, and does nothing.
 */
export const decideDeploy = ({
    state,
    desiredReference,
    force,
}: {
    readonly state: ApplicationState;
    readonly desiredReference: string;
    readonly force: boolean;
}): DeployDecision => {
    if (force) return { deploy: true, reason: "forced" };
    if (state.configuredReference !== desiredReference) {
        return {
            deploy: true,
            reason: `configured for ${state.configuredReference || "(nothing)"}`,
        };
    }
    if (!isHealthy(state.status)) {
        return { deploy: true, reason: `already pinned but ${state.status}` };
    }
    return { deploy: false, reason: "already running this digest and healthy" };
};

export const isTerminal = (status: string): boolean =>
    status === "finished" || status === "failed" || status === "cancelled";

export type CoolifyApi = {
    readonly getApplication: (uuid: string) => Promise<ApplicationState>;
    readonly setImage: (uuid: string, parts: ImageReferenceParts) => Promise<void>;
    readonly deploy: (uuid: string) => Promise<string>;
    readonly deploymentStatus: (deploymentUuid: string) => Promise<string>;
};

export type DeployResult = {
    readonly deployed: boolean;
    readonly reason: string;
    readonly deploymentUuid?: string;
    readonly outcome?: string;
};

export const runDeploy = async ({
    api,
    uuid,
    parts,
    force,
    log,
    wait,
    now,
}: {
    readonly api: CoolifyApi;
    readonly uuid: string;
    readonly parts: ImageReferenceParts;
    readonly force: boolean;
    readonly log: (message: string) => void;
    readonly wait: (ms: number) => Promise<void>;
    readonly now: () => number;
}): Promise<DeployResult> => {
    const desiredReference = referenceOf(parts);
    const state = await api.getApplication(uuid);
    const decision = decideDeploy({ state, desiredReference, force });

    log(`Coolify reports ${state.status}; ${decision.reason}.`);
    if (!decision.deploy) return { deployed: false, reason: decision.reason };

    log(`Pinning ${uuid} to ${desiredReference}.`);
    await api.setImage(uuid, parts);

    const deploymentUuid = await api.deploy(uuid);
    log(`Deployment ${deploymentUuid} queued; waiting for it to finish.`);

    const deadline = now() + POLL_TIMEOUT_MS;
    let consecutiveFailures = 0;

    for (;;) {
        let status: string;
        try {
            status = await api.deploymentStatus(deploymentUuid);
            consecutiveFailures = 0;
        } catch (error: unknown) {
            consecutiveFailures += 1;
            if (consecutiveFailures >= MAX_CONSECUTIVE_POLL_FAILURES) {
                throw new Error(
                    `Could not read the status of Coolify deployment ${deploymentUuid} ${consecutiveFailures} times in a row: ${String(error)}`,
                );
            }
            log(`Could not read deployment status (${consecutiveFailures}); retrying.`);
            await wait(POLL_INTERVAL_MS);
            continue;
        }

        if (isTerminal(status)) {
            log(`Deployment ${deploymentUuid} ${status}.`);
            if (status !== "finished") {
                throw new Error(
                    `Coolify deployment ${deploymentUuid} ${status}. Check its logs in the Coolify dashboard (over the tailnet).`,
                );
            }
            return {
                deployed: true,
                reason: decision.reason,
                deploymentUuid,
                outcome: status,
            };
        }

        if (now() >= deadline) {
            throw new Error(
                `Coolify deployment ${deploymentUuid} was still "${status}" after ${POLL_TIMEOUT_MS / 60000} minutes.`,
            );
        }
        await wait(POLL_INTERVAL_MS);
    }
};

/**
 * Validated before anything is attempted, because a bare hostname fails deep inside
 * `fetch` as "Failed to parse URL" — and if the value is held as a secret rather than
 * a variable, GitHub masks it out of that message and leaves nothing to debug. That
 * is why `COOLIFY_BASE_URL` is a repository *variable*.
 */
export const parseBaseUrl = (value: string): string => {
    const trimmed = value.trim().replace(/\/+$/, "");

    if (!/^https?:\/\//.test(trimmed)) {
        throw new Error(
            `COOLIFY_BASE_URL must include the scheme, e.g. https://coolify.example.ts.net — got "${trimmed}".`,
        );
    }
    try {
        new URL(trimmed);
    } catch {
        throw new Error(
            `COOLIFY_BASE_URL is not a usable URL: "${trimmed}". Expected the form https://host[:port].`,
        );
    }
    return trimmed;
};

const requireEnv = (name: string): string => {
    const value = process.env[name];
    if (!value) throw new Error(`${name} is not set.`);
    return value;
};

const isJsonObject = (value: unknown): value is Readonly<Record<string, unknown>> =>
    typeof value === "object" && value !== null && !Array.isArray(value);

const asString = (value: unknown): string => (typeof value === "string" ? value : "");

export const parseApplication = (payload: unknown): ApplicationState => {
    if (!isJsonObject(payload)) {
        throw new Error("The Coolify application response is not an object.");
    }

    const name = asString(payload.docker_registry_image_name);
    const tag = asString(payload.docker_registry_image_tag);
    return {
        configuredReference: referenceOf({ name, tag }),
        status: asString(payload.status),
    };
};

/** Coolify has returned the deployment uuid under more than one key across versions. */
export const parseDeploymentUuid = (payload: unknown): string => {
    const fromObject = (value: Readonly<Record<string, unknown>>): string =>
        asString(value.deployment_uuid) || asString(value.uuid);

    if (Array.isArray(payload)) {
        const first = payload.at(0);
        if (isJsonObject(first) && fromObject(first)) return fromObject(first);
    }
    if (isJsonObject(payload)) {
        const direct = fromObject(payload);
        if (direct) return direct;
        if (Array.isArray(payload.deployments)) {
            const first = payload.deployments.at(0);
            if (isJsonObject(first) && fromObject(first)) return fromObject(first);
        }
    }

    throw new Error(
        `Could not find a deployment uuid in the Coolify deploy response: ${JSON.stringify(payload)}`,
    );
};

/**
 * Node's `fetch` reports every network failure as `TypeError: fetch failed` and puts
 * the real syscall error in `cause`, so the chain has to be unwrapped before any of
 * it is useful. `ENOTFOUND` means DNS, `ECONNREFUSED` means nothing is listening,
 * and a connect timeout usually means no route — three different fixes behind one
 * message.
 */
export const describeFailure = (error: unknown): string => {
    const parts: string[] = [];
    let current: unknown = error;

    for (let depth = 0; depth < 5 && current instanceof Error; depth += 1) {
        const code =
            "code" in current && typeof current.code === "string"
                ? ` [${current.code}]`
                : "";
        parts.push(`${current.message}${code}`);
        current = current.cause;
    }

    return parts.length > 0 ? parts.join(" <- ") : String(error);
};

/** What each status most often means here, so a failure names its own cause. */
const statusHint = (status: number): string => {
    if (status === 401 || status === 403) {
        return " The COOLIFY_API_TOKEN is wrong, or lacks read/write/deploy.";
    }
    if (status === 404) {
        return " No application with that UUID — check deploy-targets.json.";
    }
    if (status >= 500) return " Coolify itself errored; its own logs will say why.";
    return "";
};

const liveApi = (
    baseUrl: string,
    token: string,
    log: (message: string) => void,
): CoolifyApi => {
    const call = async (
        route: string,
        init: { method: string; body?: string },
    ): Promise<unknown> => {
        log(`-> ${init.method} ${route}`);

        let response: Response;
        try {
            response = await fetch(`${baseUrl}${route}`, {
                method: init.method,
                headers: {
                    Authorization: `Bearer ${token}`,
                    Accept: "application/json",
                    "Content-Type": "application/json",
                },
                body: init.body,
            });
        } catch (cause: unknown) {
            // Reached over the tailnet, so this is usually the network rather than
            // Coolify. Naming both saves a round of guessing.
            throw new Error(
                `Could not reach Coolify for ${init.method} ${route}: ${describeFailure(cause)}. Check COOLIFY_BASE_URL, that the tailnet node joined, and that the Coolify service is advertised on the port in that URL.`,
            );
        }

        if (!response.ok) {
            throw new Error(
                `Coolify ${init.method} ${route} returned ${response.status}: ${await response.text()}.${statusHint(response.status)}`,
            );
        }

        // A successful PATCH may return no body; that is not a failure of the call.
        const body = await response.text();
        return body === "" ? undefined : JSON.parse(body);
    };

    return {
        getApplication: async (uuid) =>
            parseApplication(
                await call(`/api/v1/applications/${uuid}`, { method: "GET" }),
            ),
        setImage: async (uuid, parts) => {
            await call(`/api/v1/applications/${uuid}`, {
                method: "PATCH",
                body: JSON.stringify({
                    docker_registry_image_name: parts.name,
                    docker_registry_image_tag: parts.tag,
                }),
            });
        },
        deploy: async (uuid) =>
            parseDeploymentUuid(
                await call(`/api/v1/deploy?uuid=${encodeURIComponent(uuid)}`, {
                    method: "POST",
                }),
            ),
        deploymentStatus: async (deploymentUuid) => {
            const payload = await call(`/api/v1/deployments/${deploymentUuid}`, {
                method: "GET",
            });
            if (!isJsonObject(payload)) {
                throw new Error("The Coolify deployment response is not an object.");
            }
            return asString(payload.status);
        },
    };
};

const requireBaseUrl = (): string => {
    const value = process.env.COOLIFY_BASE_URL;
    if (!value) {
        throw new Error(
            "COOLIFY_BASE_URL is empty. It is a repository variable (vars), not a secret, and must include the https:// scheme.",
        );
    }
    return parseBaseUrl(value);
};

const main = async (): Promise<void> => {
    const baseUrl = requireBaseUrl();
    const uuid = requireEnv("COOLIFY_APPLICATION_UUID");
    const log = (message: string) => console.log(message);

    log(`Coolify at ${new URL(baseUrl).host}, application ${uuid}.`);

    const result = await runDeploy({
        api: liveApi(baseUrl, requireEnv("COOLIFY_API_TOKEN"), log),
        uuid,
        parts: referenceParts(
            requireEnv("IMAGE_REPOSITORY"),
            requireEnv("IMAGE_DIGEST"),
        ),
        force: process.env.FORCE_DEPLOY === "true",
        log,
        wait: (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
        now: () => Date.now(),
    });

    console.log(result.deployed ? "Deployed." : `Skipped: ${result.reason}`);
};

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main().catch((error: unknown) => {
        console.error(describeFailure(error));
        process.exitCode = 1;
    });
}
