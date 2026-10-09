#!/usr/bin/env node
/**
 * Seeds the web app's .env from TEMPLATE.env if it does not already exist.
 *
 * Nuxt reads .env from its own package root, and nuxt.config.ts pulls API_URL
 * and WEB_URL out of it at build time. Two variables is not enough to justify
 * a declarative env manager; revisit if that count grows.
 *
 * An existing .env is never overwritten.
 */

import { copyFileSync, existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const WEB_DIR = join(REPO_ROOT, "apps", "TakeInitiative.Web");
const TEMPLATE = join(WEB_DIR, "TEMPLATE.env");
const TARGET = join(WEB_DIR, ".env");

if (existsSync(TARGET)) {
    console.log(`[setup-env] ${TARGET} already exists, leaving it alone`);
} else {
    copyFileSync(TEMPLATE, TARGET);
    console.log(`[setup-env] created ${TARGET} from TEMPLATE.env`);
}
