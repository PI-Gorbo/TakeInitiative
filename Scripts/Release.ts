const fs = require("fs");
const path = require("path");

const args = process.argv.slice(2);
const type = args[0]; // patch, minor, major

if (!["patch", "minor", "major"].includes(type)) {
    console.error("Usage: bun run update-version [patch|minor|major]");
    process.exit(1);
}

// Read package.json
const packagePath = path.resolve(__dirname, "../package.json");
const packageJson = JSON.parse(fs.readFileSync(packagePath, "utf8"));

let [major, minor, patch] = packageJson.version.split(".").map(Number);

switch (type) {
    case "patch":
        patch += 1;
        break;
    case "minor":
        minor += 1;
        patch = 0; // Reset patch
        break;
    case "major":
        major += 1;
        minor = 0; // Reset minor
        patch = 0; // Reset patch
        break;
}

const newVersion = `${major}.${minor}.${patch}`;
packageJson.version = newVersion;

// Write updated package.json
fs.writeFileSync(packagePath, JSON.stringify(packageJson, null, 2), "utf8");

console.log(`Version updated to ${newVersion}`);