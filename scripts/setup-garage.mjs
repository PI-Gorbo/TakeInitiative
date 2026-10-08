#!/usr/bin/env node
/**
 * Bootstraps the dev Garage container: cluster layout, access key, bucket.
 *
 * MinIO took its root credentials from environment variables and the API created its own
 * bucket on start. Garage can do neither — a fresh node has no layout and so refuses every
 * request, and an access key only exists once its CLI has made one. This script is that
 * bootstrap, and it is idempotent, so `pnpm dev` can run it on every start.
 *
 * The credentials are fixed, and match appsettings.development.json. A Garage key ID must be
 * `GK` followed by 24 hex characters; it rejects anything else.
 */

import { execFileSync } from "node:child_process";

const CONTAINER = "takegarage";
const ACCESS_KEY = "GK1d4f8b2c9e07a35614bd8f29";
const SECRET_KEY = "c7e1b94a26f8d350179ace4b62d80f5731ea9c6d84b05f2a3e7196cd48b2a0f6";
const BUCKET = "takeinitiative";

const garage = (...args) => {
    try {
        return execFileSync("docker", ["exec", CONTAINER, "/garage", ...args], {
            encoding: "utf8",
            stdio: ["ignore", "pipe", "pipe"],
        });
    } catch (error) {
        const detail = `${error.stdout ?? ""}${error.stderr ?? ""}`.trim();
        throw new Error(`garage ${args.join(" ")} failed: ${detail || error.message}`);
    }
};

/** Garage answers "already exists" in several wordings; none of them is a failure here. */
const tolerate = (...args) => {
    try {
        return garage(...args);
    } catch (error) {
        if (/already (exists|present)|Duplicate|same name/i.test(error.message)) return "";
        throw error;
    }
};

const layoutIsAssigned = () => !garage("status").includes("NO ROLE ASSIGNED");

const assignLayout = () => {
    const line = garage("status")
        .split("\n")
        .find((l) => l.includes("NO ROLE ASSIGNED"));
    if (!line) return;

    const nodeId = line.trim().split(/\s+/)[0];
    garage("layout", "assign", "-z", "dc1", "-c", "1G", nodeId);

    // `layout apply` needs the version it is producing, which is whatever is staged plus one.
    const version = Number(
        /Current cluster layout version:\s*(\d+)/.exec(garage("layout", "show"))?.[1] ?? 0,
    );
    garage("layout", "apply", "--version", String(version + 1));
};

if (!layoutIsAssigned()) assignLayout();

tolerate("key", "import", ACCESS_KEY, SECRET_KEY, "-n", "takeinitiative", "--yes");
// The API creates its own bucket when Blobs:CreateBucket is on, which is S3 CreateBucket, and
// an imported key may not do that by default.
garage("key", "allow", "--create-bucket", ACCESS_KEY);
tolerate("bucket", "create", BUCKET);
garage("bucket", "allow", "--read", "--write", "--owner", BUCKET, "--key", ACCESS_KEY);

console.log(`Garage ready: bucket ${BUCKET}, key ${ACCESS_KEY}`);
