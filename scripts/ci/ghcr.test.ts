import assert from "node:assert/strict";
import { test } from "node:test";

import {
    type FetchLike,
    NAMESPACE,
    ghcrClient,
    isApp,
    repositoryOf,
    validateDeployableTag,
    validateDigest,
} from "./ghcr.ts";

const DIGEST = `sha256:${"a".repeat(64)}`;

/** A stub registry that records what it was asked for. */
const stubFetch = (
    routes: Record<string, { status: number; headers?: Record<string, string>; body?: string }>,
): { fetch: FetchLike; calls: string[] } => {
    const calls: string[] = [];
    const fetchLike: FetchLike = async (url, init) => {
        calls.push(`${init?.method ?? "GET"} ${url}`);
        const match = Object.entries(routes).find(([fragment]) => url.includes(fragment));
        const route = match?.[1] ?? { status: 500 };
        return {
            ok: route.status >= 200 && route.status < 300,
            status: route.status,
            headers: { get: (name) => route.headers?.[name.toLowerCase()] ?? null },
            text: async () => route.body ?? "",
        };
    };
    return { fetch: fetchLike, calls };
};

const TOKEN_ROUTE = { status: 200, body: JSON.stringify({ token: "pull-token" }) };

test("repositoryOf lowercases nothing but joins the published namespace", () => {
    assert.equal(
        repositoryOf("takeinitiative-api"),
        "ghcr.io/pi-gorbo/takeinitiative-api",
    );
    // The namespace must stay lowercase: `PI-Gorbo` is not a valid OCI repository name.
    assert.equal(NAMESPACE, NAMESPACE.toLowerCase());
});

test("isApp accepts only the two published apps", () => {
    assert.ok(isApp("api"));
    assert.ok(isApp("web"));
    assert.ok(!isApp("minio"));
    assert.ok(!isApp("queue-worker"));
});

test("validateDigest rejects anything that is not a sha256 digest", () => {
    assert.equal(validateDigest(DIGEST, "api"), DIGEST);
    assert.throws(() => validateDigest("sha256:short", "api"), /not a sha256 digest/);
    assert.throws(() => validateDigest(DIGEST.toUpperCase(), "api"), /not a sha256 digest/);
});

test("validateDeployableTag takes versions and build tags, and refuses moving ones", () => {
    assert.equal(validateDeployableTag("1.2.3"), "1.2.3");
    assert.equal(validateDeployableTag(" 1.0.15 "), "1.0.15");
    assert.equal(validateDeployableTag("sha-cca7c15"), "sha-cca7c15");

    // The point of the refusal: resolving a moving tag now does not stop it moving
    // before Coolify pulls, so the digest deployed would not be the one approved.
    for (const moving of ["latest", "edge", "buildcache"]) {
        assert.throws(() => validateDeployableTag(moving), /not a deployable tag/);
    }
    assert.throws(() => validateDeployableTag("1.2"), /not a deployable tag/);
    assert.throws(() => validateDeployableTag("v1.2.3"), /not a deployable tag/);
});

test("digestForTag reads the digest from the manifest HEAD", async () => {
    const { fetch, calls } = stubFetch({
        "/token": TOKEN_ROUTE,
        "/manifests/1.2.3": {
            status: 200,
            headers: { "docker-content-digest": DIGEST },
        },
    });

    const digest = await ghcrClient(fetch).digestForTag("takeinitiative-api", "1.2.3");

    assert.equal(digest, DIGEST);
    // HEAD, not GET: the digest is a header and an index body can be large.
    assert.ok(calls.some((call) => call.startsWith("HEAD ")));
});

test("digestForTag returns undefined for a tag that was never published", async () => {
    const { fetch } = stubFetch({
        "/token": TOKEN_ROUTE,
        "/manifests/9.9.9": { status: 404 },
    });

    // Not a throw: "that release published no image" is a real answer the caller
    // turns into a message naming the version.
    assert.equal(
        await ghcrClient(fetch).digestForTag("takeinitiative-api", "9.9.9"),
        undefined,
    );
});

test("digestForTag fails loudly when the registry answers without a digest header", async () => {
    const { fetch } = stubFetch({
        "/token": TOKEN_ROUTE,
        "/manifests/1.2.3": { status: 200 },
    });

    await assert.rejects(
        ghcrClient(fetch).digestForTag("takeinitiative-api", "1.2.3"),
        /without a docker-content-digest header/,
    );
});

test("a refused pull token names the private-package cause", async () => {
    const { fetch } = stubFetch({ "/token": { status: 403 } });

    await assert.rejects(
        ghcrClient(fetch).digestForTag("takeinitiative-api", "1.2.3"),
        /probably private/,
    );
});

test("the pull token is fetched once per image, not once per call", async () => {
    const { fetch, calls } = stubFetch({
        "/token": TOKEN_ROUTE,
        "/manifests/": { status: 200, headers: { "docker-content-digest": DIGEST } },
    });
    const client = ghcrClient(fetch);

    await client.digestForTag("takeinitiative-api", "1.2.3");
    await client.digestForTag("takeinitiative-api", "1.2.4");

    assert.equal(calls.filter((call) => call.includes("/token")).length, 1);
});

test("tagsOf returns the tag list, and [] rather than throwing on failure", async () => {
    const listed = stubFetch({
        "/token": TOKEN_ROUTE,
        "/tags/list": { status: 200, body: JSON.stringify({ tags: ["edge", "1.2.3"] }) },
    });
    assert.deepEqual(await ghcrClient(listed.fetch).tagsOf("takeinitiative-api"), [
        "edge",
        "1.2.3",
    ]);

    // Only used for the run summary, so a failure to list must not fail a deploy.
    const broken = stubFetch({ "/token": TOKEN_ROUTE, "/tags/list": { status: 500 } });
    assert.deepEqual(await ghcrClient(broken.fetch).tagsOf("takeinitiative-api"), []);
});
