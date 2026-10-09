import assert from "node:assert/strict";
import { test } from "node:test";

import {
    type ApplicationState,
    type CoolifyApi,
    type ImageReferenceParts,
    decideDeploy,
    describeFailure,
    isHealthy,
    isTerminal,
    parseApplication,
    parseBaseUrl,
    parseDeploymentUuid,
    referenceOf,
    referenceParts,
    runDeploy,
} from "./coolify-deploy.ts";

const HEX = "c".repeat(64);
const DIGEST = `sha256:${HEX}`;
const REPOSITORY = "ghcr.io/pi-gorbo/takeinitiative-api";

const parts = referenceParts(REPOSITORY, DIGEST);
const REFERENCE = `${REPOSITORY}@sha256:${HEX}`;

/** A scripted Coolify. `statuses` is consumed one poll at a time. */
const stubApi = ({
    state,
    statuses,
    failPolls = 0,
}: {
    state: ApplicationState;
    statuses: string[];
    failPolls?: number;
}): {
    api: CoolifyApi;
    setCalls: ImageReferenceParts[];
    deployCalls: string[];
} => {
    const setCalls: ImageReferenceParts[] = [];
    const deployCalls: string[] = [];
    let remainingFailures = failPolls;
    const queue = [...statuses];

    return {
        setCalls,
        deployCalls,
        api: {
            getApplication: async () => state,
            setImage: async (_uuid, next) => {
                setCalls.push(next);
            },
            deploy: async (uuid) => {
                deployCalls.push(uuid);
                return "deployment-1";
            },
            deploymentStatus: async () => {
                if (remainingFailures > 0) {
                    remainingFailures -= 1;
                    throw new Error("connection reset");
                }
                return queue.shift() ?? "finished";
            },
        },
    };
};

const silent = () => {
    /* the deploy logs for a human; tests do not need it */
};
const immediately = async () => {
    /* no real waiting in tests */
};

test("referenceParts splits a digest the way Coolify stores one", () => {
    // Coolify has no digest field: `@sha256` stays on the NAME and the bare hex
    // goes in the TAG, so joining them with ':' composes image@sha256:<hex>.
    assert.deepEqual(parts, { name: `${REPOSITORY}@sha256`, tag: HEX });
    assert.equal(referenceOf(parts), REFERENCE);
});

test("referenceParts accepts a bare hex digest and refuses anything else", () => {
    assert.equal(referenceParts(REPOSITORY, HEX).tag, HEX);
    assert.throws(() => referenceParts(REPOSITORY, "sha256:nope"), /Expected a sha256 digest/);
    assert.throws(() => referenceParts(REPOSITORY, ""), /Expected a sha256 digest/);
});

test("referenceOf leaves no trailing colon when the tag is empty", () => {
    assert.equal(referenceOf({ name: REPOSITORY, tag: "" }), REPOSITORY);
});

test("isHealthy requires running AND healthy", () => {
    assert.ok(isHealthy("running:healthy"));
    assert.ok(isHealthy("running"));
    assert.ok(!isHealthy("running:unhealthy"));
    assert.ok(!isHealthy("exited:unhealthy"));
    assert.ok(!isHealthy("restarting"));
});

test("a push that resolves the running digest deploys nothing", async () => {
    // This is what a non-release push to main does, and it is why such a push is free.
    const { api, deployCalls } = stubApi({
        state: { configuredReference: REFERENCE, status: "running:healthy" },
        statuses: [],
    });

    const result = await runDeploy({
        api,
        uuid: "app-1",
        parts,
        force: false,
        log: silent,
        wait: immediately,
        now: () => 0,
    });

    assert.equal(result.deployed, false);
    assert.match(result.reason, /already running this digest and healthy/);
    assert.deepEqual(deployCalls, []);
});

test("an already-pinned but unhealthy application is redeployed", () => {
    // Matching the configured reference is not evidence the artefact is live: a
    // crash-looped container would otherwise be skipped forever.
    const decision = decideDeploy({
        state: { configuredReference: REFERENCE, status: "running:unhealthy" },
        desiredReference: REFERENCE,
        force: false,
    });

    assert.ok(decision.deploy);
    assert.match(decision.reason, /already pinned but running:unhealthy/);
});

test("force redeploys a healthy application on the right digest", () => {
    const decision = decideDeploy({
        state: { configuredReference: REFERENCE, status: "running:healthy" },
        desiredReference: REFERENCE,
        force: true,
    });

    assert.ok(decision.deploy);
    assert.equal(decision.reason, "forced");
});

test("a never-configured application reports '(nothing)' rather than an empty string", () => {
    const decision = decideDeploy({
        state: { configuredReference: "", status: "exited" },
        desiredReference: REFERENCE,
        force: false,
    });

    assert.match(decision.reason, /configured for \(nothing\)/);
});

test("a deploy sets the image, then deploys, then waits for 'finished'", async () => {
    const { api, setCalls, deployCalls } = stubApi({
        state: { configuredReference: "old:tag", status: "running:healthy" },
        statuses: ["queued", "in_progress", "finished"],
    });

    const result = await runDeploy({
        api,
        uuid: "app-1",
        parts,
        force: false,
        log: silent,
        wait: immediately,
        now: () => 0,
    });

    // Two calls, in this order: deploying without setting the reference first would
    // redeploy whatever Coolify was already configured with.
    assert.deepEqual(setCalls, [parts]);
    assert.deepEqual(deployCalls, ["app-1"]);
    assert.equal(result.deployed, true);
    assert.equal(result.outcome, "finished");
});

test("a failed Coolify deployment fails the job and points at its logs", async () => {
    const { api } = stubApi({
        state: { configuredReference: "old:tag", status: "running:healthy" },
        statuses: ["in_progress", "failed"],
    });

    await assert.rejects(
        runDeploy({
            api,
            uuid: "app-1",
            parts,
            force: false,
            log: silent,
            wait: immediately,
            now: () => 0,
        }),
        /deployment-1 failed\. Check its logs/,
    );
});

test("a single dropped poll is tolerated; three in a row are not", async () => {
    // Coolify is reached over the tailnet, so one lost request must not fail a
    // deploy that is still progressing.
    const tolerated = stubApi({
        state: { configuredReference: "old:tag", status: "running:healthy" },
        statuses: ["finished"],
        failPolls: 2,
    });
    const result = await runDeploy({
        api: tolerated.api,
        uuid: "app-1",
        parts,
        force: false,
        log: silent,
        wait: immediately,
        now: () => 0,
    });
    assert.equal(result.deployed, true);

    const fatal = stubApi({
        state: { configuredReference: "old:tag", status: "running:healthy" },
        statuses: ["finished"],
        failPolls: 3,
    });
    await assert.rejects(
        runDeploy({
            api: fatal.api,
            uuid: "app-1",
            parts,
            force: false,
            log: silent,
            wait: immediately,
            now: () => 0,
        }),
        /3 times in a row/,
    );
});

test("a deployment that never terminates times out", async () => {
    const { api } = stubApi({
        state: { configuredReference: "old:tag", status: "running:healthy" },
        statuses: Array(500).fill("in_progress"),
    });
    let clock = 0;

    await assert.rejects(
        runDeploy({
            api,
            uuid: "app-1",
            parts,
            force: false,
            log: silent,
            wait: immediately,
            now: () => {
                clock += 60_000;
                return clock;
            },
        }),
        /still "in_progress" after 20 minutes/,
    );
});

test("isTerminal covers exactly Coolify's end states", () => {
    assert.ok(isTerminal("finished"));
    assert.ok(isTerminal("failed"));
    assert.ok(isTerminal("cancelled"));
    assert.ok(!isTerminal("in_progress"));
    assert.ok(!isTerminal("queued"));
});

test("parseApplication composes the configured reference from Coolify's two fields", () => {
    const state = parseApplication({
        docker_registry_image_name: `${REPOSITORY}@sha256`,
        docker_registry_image_tag: HEX,
        status: "running:healthy",
    });

    assert.equal(state.configuredReference, REFERENCE);
    assert.equal(state.status, "running:healthy");
    assert.throws(() => parseApplication("nope"), /not an object/);
});

test("parseDeploymentUuid handles the shapes Coolify has returned across versions", () => {
    assert.equal(parseDeploymentUuid({ deployment_uuid: "d-1" }), "d-1");
    assert.equal(parseDeploymentUuid({ uuid: "d-2" }), "d-2");
    assert.equal(parseDeploymentUuid([{ deployment_uuid: "d-3" }]), "d-3");
    assert.equal(parseDeploymentUuid({ deployments: [{ uuid: "d-4" }] }), "d-4");
    assert.throws(() => parseDeploymentUuid({ nope: true }), /Could not find a deployment uuid/);
});

test("parseBaseUrl insists on a scheme and strips a trailing slash", () => {
    assert.equal(parseBaseUrl("https://coolify.example.ts.net/"), "https://coolify.example.ts.net");
    assert.equal(parseBaseUrl("  http://host:8000  "), "http://host:8000");
    // A bare hostname otherwise fails deep inside fetch as "Failed to parse URL".
    assert.throws(() => parseBaseUrl("coolify.example.ts.net"), /must include the scheme/);
});

test("describeFailure unwraps the cause chain fetch hides the real error in", () => {
    const syscall = Object.assign(new Error("getaddrinfo ENOTFOUND host"), {
        code: "ENOTFOUND",
    });
    const wrapped = new Error("fetch failed", { cause: syscall });

    const described = describeFailure(wrapped);

    assert.match(described, /fetch failed/);
    assert.match(described, /ENOTFOUND/);
});
