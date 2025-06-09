import { $ } from "bun";

const fs = require("fs");
const path = require("path");

// const args = process.argv.slice(2);
// const type = args[0]; // patch, minor, major

// Import necessary modules from the libraries
import { Args, Command } from "@effect/cli"
import { NodeContext, NodeRuntime } from "@effect/platform-node"
import { Console, Effect } from "effect"

// Define the top-level command
type IncrementVersion = 'Patch' | 'Minor' | 'Major'
type VersionNumber = `${string}.${string}.${string}`
const sematicVersionType = Args.choice<IncrementVersion>([['Patch', 'Patch'], ['Minor', 'Minor'], ['Major', 'Major']], { name: 'Update type' })

const updatePackageJson = (incrementType: IncrementVersion) =>
    Effect.try({
        try: () => {
            // Read package.json
            const packagePath = path.resolve(__dirname, "../package.json");
            const packageJson = JSON.parse(fs.readFileSync(packagePath, "utf8"));

            let [major, minor, patch] = packageJson.version.split(".").map(Number);

            switch (incrementType) {
                case "Patch":
                    patch += 1;
                    break;
                case "Minor":
                    minor += 1;
                    patch = 0; // Reset patch
                    break;
                case "Major":
                    major += 1;
                    minor = 0; // Reset minor
                    patch = 0; // Reset patch
                    break;
            }

            const newVersion: VersionNumber = `${major}.${minor}.${patch}`;
            packageJson.version = newVersion;

            // Write updated package.json
            fs.writeFileSync(packagePath, JSON.stringify(packageJson, null, 2), "utf8");

            return newVersion;
        },
        catch: (err) => `Something went wrong while trying to update the package.json file: ${err}`
    })


const incrementVersionAndCreatePr = (newVersionNumber: VersionNumber) => Effect.tryPromise(async () => {
    const prName = `release/${newVersionNumber}`
    await $`git checkout -b '${prName}'`
    await $`git add package.json`
    await $`git commit -am 'Incremented package.json to version ${newVersionNumber}'`
    await $`git push`
    await $`gh pr create --title 'Increment to ${newVersionNumber} on dev' --body 'This pr was automatically generated.' --base dev --head ${prName} --web`
    return prName;
})

// const waitForPrTobeClosed = (prName: string) => Effect.

const release = Command.make("release", { sematicVersionType }, ({ sematicVersionType }) =>
    Effect.gen(function* () {
        yield* Effect.promise(async () => await $`git switch dev`)
        yield* Effect.promise(async () => await $`git pull`)
        const newVersionNumber = yield* updatePackageJson(sematicVersionType)
        const newVersionPrName = yield* incrementVersionAndCreatePr(newVersionNumber)
    })


// // Read package.json
// const packagePath = path.resolve(__dirname, "../package.json");
// const packageJson = JSON.parse(fs.readFileSync(packagePath, "utf8"));

// let [major, minor, patch] = packageJson.version.split(".").map(Number);

// switch (type) {
//     case "patch":
//         patch += 1;
//         break;
//     case "minor":
//         minor += 1;
//         patch = 0; // Reset patch
//         break;
//     case "major":
//         major += 1;
//         minor = 0; // Reset minor
//         patch = 0; // Reset patch
//         break;
// }

// const newVersion = `${major}.${minor}.${patch}`;
// packageJson.version = newVersion;

    // // Write updated package.json
    // fs.writeFileSync(packagePath, JSON.stringify(packageJson, null, 2), "utf8");

    // console.log(`-- Version updated to ${newVersion}`);
    // console.log("-- Creating PR to update the version.")
    // await $`git checkout -b 'release/${newVersion}'`
    // await $`git add package.json`
    // await $`git commit -am 'Incremented package.json to version ${newVersion}'`
    // await $`git push`
    // await $`gh pr create --title 'Increment to ${newVersion} on dev' --body 'This pr was automatically generated.' --base dev --head release/${newVersion} --web`




    // console.log("-- Making a new PR with the update.")
    // await $`gh pr create --title 'Push version ${newVersion} to main' --body 'This pr was automatically generated.' --base main --head dev --web`



)

// Set up the CLI application
const cli = Command.run(release, {
    name: "Hello World CLI",
    version: "v1.0.0"
})

// Prepare and run the CLI application
cli(process.argv).pipe(Effect.provide(NodeContext.layer), NodeRuntime.runMain)