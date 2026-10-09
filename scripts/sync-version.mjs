#!/usr/bin/env node
/**
 * Propagates the root package.json version into the things that actually ship.
 *
 * release-please owns the version: it bumps root package.json on the release
 * PR it opens. Everything downstream of that is this script's job.
 *
 * Targets:
 *   - <Version> in apps/TakeInitiative.Api/TakeInitiative.Api.csproj, so the
 *     published assembly reports the release version.
 *   - TAKEINITIATIVE_VERSION in the root .env, which compose.dev.yml
 *     interpolates into the api/web image tags. .env is gitignored, so this
 *     only affects local builds; CI image builds should set the variable in
 *     the environment instead.
 *
 * Run by .github/workflows/release.yml on the release-please branch, and
 * available locally as `pnpm sync:version`.
 */

import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const CSPROJ = join(REPO_ROOT, "apps", "TakeInitiative.Api", "TakeInitiative.Api.csproj");
const ENV_FILE = join(REPO_ROOT, ".env");
const ENV_KEY = "TAKEINITIATIVE_VERSION";

const { version } = JSON.parse(readFileSync(join(REPO_ROOT, "package.json"), "utf8"));
if (!version) {
    console.error("[sync-version] root package.json has no version");
    process.exit(1);
}

// --- csproj -----------------------------------------------------------------
const csproj = readFileSync(CSPROJ, "utf8");
let updated;

if (/<Version>[^<]*<\/Version>/.test(csproj)) {
    updated = csproj.replace(/<Version>[^<]*<\/Version>/, `<Version>${version}</Version>`);
} else {
    // Insert as the first entry of the first PropertyGroup.
    updated = csproj.replace(
        /(<PropertyGroup>\s*\r?\n)/,
        `$1        <Version>${version}</Version>\n`
    );
    if (updated === csproj) {
        console.error("[sync-version] could not find a <PropertyGroup> to add <Version> to");
        process.exit(1);
    }
}

if (updated === csproj) {
    console.log(`[sync-version] csproj already at ${version}`);
} else {
    writeFileSync(CSPROJ, updated);
    console.log(`[sync-version] set <Version>${version}</Version> in TakeInitiative.Api.csproj`);
}

// --- .env -------------------------------------------------------------------
const line = `${ENV_KEY}=${version}`;
const existing = existsSync(ENV_FILE) ? readFileSync(ENV_FILE, "utf8") : "";
const keyPattern = new RegExp(`^${ENV_KEY}=.*$`, "m");

let envContents;
if (keyPattern.test(existing)) {
    envContents = existing.replace(keyPattern, line);
} else {
    envContents = existing === "" || existing.endsWith("\n") ? `${existing}${line}\n` : `${existing}\n${line}\n`;
}

if (envContents === existing) {
    console.log(`[sync-version] .env already at ${version}`);
} else {
    writeFileSync(ENV_FILE, envContents);
    console.log(`[sync-version] set ${line} in .env`);
}
