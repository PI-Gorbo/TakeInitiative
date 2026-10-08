#!/usr/bin/env node
/**
 * Bootstraps a Garage cluster: layout, access key, bucket, grants. Idempotent.
 *
 * Over the admin API rather than the `garage` CLI, because the official image contains the
 * `/garage` binary and nothing else — no shell at all. Coolify's container terminal cannot
 * attach to it, and `docker exec` needs SSH onto the host. The admin API needs neither, so the
 * same script bootstraps the dev container and a Coolify-managed one from a laptop.
 *
 * Dev needs no arguments. For Coolify, take the Admin URL and Admin Token from the service's
 * configuration page — it generates both — and pass the key pair the API will use:
 *
 *   GARAGE_ADMIN_URL=https://admin-xxxx.example.org \
 *   GARAGE_ADMIN_TOKEN=... \
 *   BLOBS_ACCESS_KEY=GK... BLOBS_SECRET_KEY=... \
 *   node scripts/setup-garage.mjs
 */

const ADMIN_URL = (process.env.GARAGE_ADMIN_URL ?? "http://localhost:7405").replace(/\/+$/, "");
const ADMIN_TOKEN = process.env.GARAGE_ADMIN_TOKEN ?? "dev-admin-token";
const ACCESS_KEY = process.env.BLOBS_ACCESS_KEY ?? "GK1d4f8b2c9e07a35614bd8f29";
const SECRET_KEY =
    process.env.BLOBS_SECRET_KEY ??
    "c7e1b94a26f8d350179ace4b62d80f5731ea9c6d84b05f2a3e7196cd48b2a0f6";
const BUCKET = process.env.GARAGE_BUCKET ?? "takeinitiative";
const ZONE = process.env.GARAGE_ZONE ?? "dc1";
const CAPACITY = Number(process.env.GARAGE_CAPACITY ?? 1_000_000_000);

if (!/^GK[0-9a-f]{24}$/.test(ACCESS_KEY)) {
    throw new Error(
        `BLOBS_ACCESS_KEY must be "GK" followed by 24 hex characters; Garage rejects anything else. Got "${ACCESS_KEY}".`,
    );
}

/** `tolerate` is the status a second run is expected to produce, and is not a failure. */
const call = async (op, { method = "POST", body, query = "", tolerate } = {}) => {
    const response = await fetch(`${ADMIN_URL}/v2/${op}${query}`, {
        method,
        headers: {
            Authorization: `Bearer ${ADMIN_TOKEN}`,
            ...(body ? { "Content-Type": "application/json" } : {}),
        },
        body: body ? JSON.stringify(body) : undefined,
    });

    if (response.status === tolerate) return undefined;
    if (!response.ok) {
        const detail = await response.text();
        throw new Error(`${op} returned ${response.status}: ${detail}`);
    }
    const text = await response.text();
    return text === "" ? undefined : JSON.parse(text);
};

const status = await call("GetClusterStatus", { method: "GET" });
const node = status.nodes[0];
if (!node) throw new Error("Garage reports no nodes.");

// Re-applying a layout version is an error rather than a no-op, so this is guarded by whether
// the node already has a role rather than by tolerating a status.
if (node.role === null) {
    await call("UpdateClusterLayout", {
        body: { roles: [{ id: node.id, zone: ZONE, capacity: CAPACITY, tags: [] }] },
    });
    await call("ApplyClusterLayout", { body: { version: status.layoutVersion + 1 } });
    console.log(`Layout applied: ${node.hostname} in ${ZONE}, ${CAPACITY} bytes.`);
}

await call("ImportKey", {
    body: { accessKeyId: ACCESS_KEY, secretAccessKey: SECRET_KEY, name: "takeinitiative" },
    tolerate: 409,
});
// The API creates its own bucket when Blobs:CreateBucket is on, which is S3 CreateBucket, and
// an imported key may not do that by default.
await call("UpdateKey", { query: `?id=${ACCESS_KEY}`, body: { allow: { createBucket: true } } });

await call("CreateBucket", { body: { globalAlias: BUCKET }, tolerate: 409 });
const bucket = await call("GetBucketInfo", {
    method: "GET",
    query: `?globalAlias=${encodeURIComponent(BUCKET)}`,
});
await call("AllowBucketKey", {
    body: {
        bucketId: bucket.id,
        accessKeyId: ACCESS_KEY,
        permissions: { read: true, write: true, owner: true },
    },
});

const key = await call("GetKeyInfo", { method: "GET", query: `?id=${ACCESS_KEY}` });
const granted = key.buckets.find((b) => b.globalAliases.includes(BUCKET));
if (!granted?.permissions.write) {
    throw new Error(`${ACCESS_KEY} ended up without write access to ${BUCKET}.`);
}

console.log(`Garage ready at ${ADMIN_URL}: bucket ${BUCKET}, key ${ACCESS_KEY}`);
