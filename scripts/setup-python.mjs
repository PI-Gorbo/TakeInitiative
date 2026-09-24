#!/usr/bin/env node
/**
 * Installs the Python interpreter that the API embeds via pythonnet, and points
 * appsettings.Development.json at it.
 *
 * Why this exists: pythonnet loads CPython in-process, so it needs a real
 * libpython shared library. macOS's system python3 (3.9.6, Xcode CLT) ships no
 * usable dylib, so we use a uv-managed standalone build, which does.
 *
 * Why 3.11: it matches CI's python-version and pythonnet 3.0.3 predates 3.13.
 *
 * This whole file is deleted in roadmap step 11, once the dice roller no longer
 * needs Python. Keep the footprint small and easy to remove.
 */

import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync, readdirSync } from "node:fs";
import { delimiter, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const PYTHON_VERSION = "3.11";
const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const VENV_DIR = join(REPO_ROOT, ".venv");
const APPSETTINGS = join(
    REPO_ROOT,
    "apps",
    "TakeInitiative.Api",
    "appsettings.Development.json",
);

function run(cmd, args) {
    return execFileSync(cmd, args, {
        cwd: REPO_ROOT,
        encoding: "utf8",
        stdio: ["ignore", "pipe", "inherit"],
    }).trim();
}

function log(message) {
    console.log(`[setup-python] ${message}`);
}

// 1. Interpreter. `uv python install` is a no-op when it is already present.
log(`installing CPython ${PYTHON_VERSION} via uv...`);
run("uv", ["python", "install", PYTHON_VERSION]);

// 2. Virtualenv. `uv venv` errors out on an existing directory unless told
//    otherwise, and this script runs on every `pnpm dev`, so allow it.
log(`creating ${VENV_DIR}...`);
run("uv", ["venv", "--allow-existing", "--python", PYTHON_VERSION, VENV_DIR]);

// 3. Packages (d20).
log("installing requirements.txt...");
run("uv", [
    "pip",
    "install",
    "--python",
    join(VENV_DIR, "bin", "python"),
    "-r",
    join(REPO_ROOT, "requirements.txt"),
]);

// 4. Resolve the interpreter prefix. `--managed-python --system` skips the
//    .venv we just created (which `uv python find` would otherwise prefer) and
//    resolves the uv-managed standalone build; `--resolve-links` turns the
//    versioned symlink into a real path. The result is <prefix>/bin/python3.11,
//    so the prefix is two levels up.
const interpreter = run("uv", [
    "python",
    "find",
    "--managed-python",
    "--system",
    "--resolve-links",
    PYTHON_VERSION,
]);
const pythonHome = dirname(dirname(interpreter));

// The shared library extension differs per platform, so glob for it rather than
// hardcoding .dylib.
const libDir = process.platform === "win32" ? pythonHome : join(pythonHome, "lib");
const libraryPrefix = `libpython${PYTHON_VERSION}`;
const libraryName = readdirSync(libDir).find(
    (name) =>
        name.startsWith(libraryPrefix) &&
        /\.(dylib|so|dll)(\.\d+)*$/.test(name),
);
if (!libraryName) {
    throw new Error(
        `Could not find ${libraryPrefix}.{dylib,so,dll} in ${libDir}. ` +
            `pythonnet cannot embed an interpreter without its shared library.`,
    );
}

// PythonEngine.PythonPath REPLACES sys.path rather than appending to it, so it
// has to carry the interpreter's own stdlib directories as well as the venv's
// site-packages. Listing only site-packages leaves CPython unable to find
// `encodings` and it dies during Initialize() with:
//   Fatal Python error: init_fs_encoding: failed to get the Python codec of the
//   filesystem encoding
const isWindows = process.platform === "win32";
const stdlibDirs = isWindows
    ? [join(pythonHome, "Lib"), join(pythonHome, "DLLs")]
    : [
          join(pythonHome, "lib", `python${PYTHON_VERSION}`),
          join(pythonHome, "lib", `python${PYTHON_VERSION}`, "lib-dynload"),
      ];
const sitePackages = isWindows
    ? join(VENV_DIR, "Lib", "site-packages")
    : join(VENV_DIR, "lib", `python${PYTHON_VERSION}`, "site-packages");

const pythonSettings = {
    PythonDLL: join(libDir, libraryName),
    PythonHome: pythonHome,
    PythonPath: [...stdlibDirs, sitePackages].join(delimiter),
};

// 5. Merge into appsettings.Development.json rather than overwriting it — the
//    file is gitignored, so anything else in there is a local customisation.
let settings = {
    Logging: {
        LogLevel: { Default: "Information", "Microsoft.AspNetCore": "Warning" },
    },
};
if (existsSync(APPSETTINGS)) {
    try {
        settings = JSON.parse(readFileSync(APPSETTINGS, "utf8"));
    } catch (error) {
        throw new Error(
            `${APPSETTINGS} is not valid JSON, refusing to overwrite it: ${error.message}`,
        );
    }
}

writeFileSync(
    APPSETTINGS,
    `${JSON.stringify({ ...settings, ...pythonSettings }, null, 4)}\n`,
);

log(`PythonDLL  = ${pythonSettings.PythonDLL}`);
log(`PythonHome = ${pythonSettings.PythonHome}`);
log(`PythonPath = ${pythonSettings.PythonPath}`);
log(`wrote ${APPSETTINGS}`);
