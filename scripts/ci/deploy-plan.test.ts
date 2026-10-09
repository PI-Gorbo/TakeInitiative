import assert from "node:assert/strict";
import { test } from "node:test";

import type { GhcrClient } from "./ghcr.ts";
import {
    type DeployPlan,
    assertDistinctUuids,
    buildPlan,
    deployingBranch,
    deployMatrix,
    deployable,
    environmentForBranch,
    formatOutputs,
    parseDeployTargets,
    planApp,
    selectApps,
    summaryMarkdown,
    versionFromManifest,
} from "./deploy-plan.ts";

const API_DIGEST = `sha256:${"a".repeat(64)}`;
const WEB_DIGEST = `sha256:${"b".repeat(64)}`;

const targets = (overrides: {
    api?: { enabled?: boolean; uuid?: string };
    web?: { enabled?: boolean; uuid?: string };
} = {}): string =>
    JSON.stringify({
        environments: {
            prod: {
                branch: "main",
                githubEnvironment: "prod",
                apps: {
                    api: {
                        enabled: overrides.api?.enabled ?? true,
                        coolifyApplicationUuid: overrides.api?.uuid ?? "api-uuid",
                    },
                    web: {
                        enabled: overrides.web?.enabled ?? true,
                        coolifyApplicationUuid: overrides.web?.uuid ?? "web-uuid",
                    },
                },
            },
        },
    });

const manifest = (version: string): string => JSON.stringify({ name: "ti", version });

const stubClient = (
    digests: Record<string, string | undefined> = {},
): GhcrClient => ({
    digestForTag: async (image) =>
        image in digests
            ? digests[image]
            : image === "takeinitiative-api"
              ? API_DIGEST
              : WEB_DIGEST,
    tagsOf: async () => ["edge", "1.2.3"],
});

const plan = (overrides: Partial<Parameters<typeof buildPlan>[0]> = {}) =>
    buildPlan({
        branch: "main",
        sha: "abcdef1234567890",
        pinTag: "",
        forceDeploy: false,
        targets: "",
        targetsRaw: targets(),
        manifestRaw: manifest("1.2.3"),
        client: stubClient(),
        ...overrides,
    });

test("parseDeployTargets rejects a config missing a field rather than reading it as absent", () => {
    assert.throws(() => parseDeployTargets("{}"), /no "environments" object/);
    assert.throws(
        () =>
            parseDeployTargets(
                JSON.stringify({
                    environments: { prod: { branch: "main", githubEnvironment: "prod" } },
                }),
            ),
        /apps is not an object/,
    );
    assert.throws(
        () =>
            parseDeployTargets(
                JSON.stringify({
                    environments: {
                        prod: {
                            branch: "main",
                            githubEnvironment: "prod",
                            apps: { api: { enabled: "yes", coolifyApplicationUuid: "x" }, web: {} },
                        },
                    },
                }),
            ),
        /enabled is not a boolean/,
    );
});

test("environmentForBranch names the known branches when none matches", () => {
    const parsed = parseDeployTargets(targets());
    assert.equal(environmentForBranch(parsed, "main")[0], "prod");
    assert.throws(() => environmentForBranch(parsed, "dev"), /Known branches: main/);
});

test("deployingBranch asks the config, so explain works off a PR merge ref", () => {
    // The regression this exists for: `explain` used to read GITHUB_REF_NAME, which on a
    // pull_request run is `281/merge`. That matches no environment, so the one CI job meant to
    // prove deploy-targets.json parses was the job that failed.
    assert.equal(deployingBranch(parseDeployTargets(targets())), "main");
    assert.throws(
        () => deployingBranch(parseDeployTargets(JSON.stringify({ environments: {} }))),
        /defines no environments/,
    );
});

test("selectApps defaults to every app and rejects an unknown one", () => {
    assert.deepEqual(selectApps(""), ["api", "web"]);
    assert.deepEqual(selectApps(" web "), ["web"]);
    assert.deepEqual(selectApps("api,web"), ["api", "web"]);
    assert.throws(() => selectApps("minio"), /Unknown app "minio"/);
});

test("versionFromManifest reads the version release-please owns", () => {
    assert.equal(versionFromManifest(manifest("1.0.15")), "1.0.15");
    assert.throws(() => versionFromManifest("{}"), /no version field/);
});

test("a disabled app is skipped rather than deployed", () => {
    const result = planApp({
        app: "web",
        tag: "1.2.3",
        digest: undefined,
        target: { enabled: false, coolifyApplicationUuid: "" },
    });

    assert.equal(result.skipped, "disabled in deploy-targets.json");
    assert.equal(result.digest, "");
});

test("an enabled app with no UUID is an error, not a silent no-op", () => {
    // The asymmetry with the test above is the point: `enabled: false` is the
    // documented off-switch, a missing UUID is a half-finished config that would
    // otherwise PATCH nothing and report success.
    assert.throws(
        () =>
            planApp({
                app: "api",
                tag: "1.2.3",
                digest: API_DIGEST,
                target: { enabled: true, coolifyApplicationUuid: "" },
            }),
        /has no coolifyApplicationUuid/,
    );
});

test("two apps may not point at the same Coolify application", async () => {
    // The UUIDs are pasted by hand from the dashboard as opaque 24-character strings,
    // and the applications were created undesignated — so duplicating one is plausible.
    // Nothing downstream would notice: both deploys succeed against the same
    // application, the second overwriting the first.
    await assert.rejects(
        plan({ targetsRaw: targets({ api: { uuid: "same" }, web: { uuid: "same" } }) }),
        /both point at Coolify application same/,
    );
});

test("a shared UUID is only an error for apps actually being deployed", async () => {
    // Two DISABLED apps share the empty-string placeholder, which means nothing.
    const result = await plan({
        targetsRaw: targets({
            api: { enabled: false, uuid: "" },
            web: { enabled: false, uuid: "" },
        }),
    });

    assert.equal(deployable(result).length, 0);
});

test("assertDistinctUuids passes distinct ones through", () => {
    assert.doesNotThrow(() =>
        assertDistinctUuids([
            { app: "api", image: "i", repository: "r", tag: "1.0.0", digest: "d", uuid: "a" },
            { app: "web", image: "i", repository: "r", tag: "1.0.0", digest: "d", uuid: "b" },
        ]),
    );
});

test("an unpublished tag fails naming the release rather than at pull time", async () => {
    await assert.rejects(
        plan({ client: stubClient({ "takeinitiative-web": undefined }) }),
        /is not published, so there is nothing to deploy/,
    );
});

test("the plan resolves the manifest version to both digests", async () => {
    const result = await plan();

    assert.equal(result.environmentName, "prod");
    assert.equal(result.githubEnvironment, "prod");
    assert.equal(result.tag, "1.2.3");
    assert.equal(result.pinned, false);
    assert.deepEqual(
        result.apps.map((app) => [app.app, app.digest]),
        [
            ["api", API_DIGEST],
            ["web", WEB_DIGEST],
        ],
    );
});

test("pin_tag overrides the manifest version and marks the plan a rollback", async () => {
    const result = await plan({ pinTag: "sha-cca7c15" });

    assert.equal(result.tag, "sha-cca7c15");
    assert.equal(result.pinned, true);
});

test("pin_tag is validated, so a moving tag cannot be dispatched", async () => {
    await assert.rejects(plan({ pinTag: "latest" }), /not a deployable tag/);
});

test("targets narrows the plan to one app", async () => {
    const result = await plan({ targets: "api" });

    assert.deepEqual(
        result.apps.map((app) => app.app),
        ["api"],
    );
});

test("the matrix carries one include entry per deployable app", async () => {
    const result = await plan({ targetsRaw: targets({ web: { enabled: false } }) });

    assert.equal(deployable(result).length, 1);
    const matrix = JSON.parse(deployMatrix(result));
    assert.deepEqual(matrix, {
        include: [
            {
                app: "api",
                image: "takeinitiative-api",
                repository: "ghcr.io/pi-gorbo/takeinitiative-api",
                digest: API_DIGEST,
                uuid: "api-uuid",
            },
        ],
    });
});

test("deploy-count is a string the workflow can compare against '0'", async () => {
    const everythingOff = await plan({
        targetsRaw: targets({ api: { enabled: false }, web: { enabled: false } }),
    });
    const outputs = new Map(formatOutputs(everythingOff));

    // A string, not a number: the workflow gates the deploy job on `!= '0'`, and GitHub
    // compares job outputs as strings.
    assert.equal(outputs.get("deploy-count"), "0");
    assert.equal(outputs.get("github-environment"), "prod");
    assert.equal(JSON.parse(outputs.get("deploy-matrix") ?? "").include.length, 0);
});

test("the summary states the digests and flags a pinned rollback", async () => {
    const pinned = await plan({ pinTag: "sha-cca7c15" });
    const markdown = summaryMarkdown(pinned, {
        "takeinitiative-api": ["buildcache", "edge", "1.2.3"],
    });

    assert.match(markdown, /this is a rollback/);
    assert.match(markdown, /sha-cca7c15/);
    assert.match(markdown, new RegExp(API_DIGEST));
    // buildcache is noise on the package page, not a deployable artefact.
    assert.ok(!markdown.includes("buildcache"));

    const normal: DeployPlan = await plan();
    assert.ok(!summaryMarkdown(normal, {}).includes("this is a rollback"));
});
