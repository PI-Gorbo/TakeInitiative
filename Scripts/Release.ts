import { $ } from "bun";

const fs = require("fs");
const path = require("path");

// const args = process.argv.slice(2);
// const type = args[0]; // patch, minor, major

// Import necessary modules from the libraries
import { Args, Command } from "@effect/cli"
import { NodeContext, NodeRuntime } from "@effect/platform-node"
import { Console, Effect, pipe, Schedule, Schema } from "effect"

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
    return 'Increment to 1.0.11 on dev';
})

const PrResponse = Schema.Array(Schema.Struct({
    state: Schema.Literal('OPEN', "CLOSED"),
    title: Schema.String
}))
const waitForClosedPrWithTitle = (prName: string) =>
    Effect.retry(
        pipe(
            Effect.tryPromise(async () => {
                debugger;
                const openPrsResp = (await $`gh pr list --state open --search '${prName}' --json title,state'`);
                const closedPrsResp = (await $`gh pr list --state closed --search '${prName}' --json title,state'`);
                return {
                    openPrs: openPrsResp.json(),
                    closedPrs: closedPrsResp.json()
                }
            }),
            Effect.flatMap(({ openPrs, closedPrs }) => {
                const schema = Schema.decodeUnknown(PrResponse)
                return pipe(
                    Effect.all([schema(openPrs), schema(closedPrs)]),
                    Effect.map(([parsedOpenPrs, parsedClosedPrs]) => ({ openPrs: parsedOpenPrs, closedPrs: parsedClosedPrs }))
                )
            }),
            Effect.flatMap(({ openPrs, closedPrs }) => {
                debugger;
                if (closedPrs.length != 1) {
                    if (openPrs.length != 1) {
                        return Effect.fail('No PRs')
                    }

                    return Effect.fail('PR has not been closed yet.')
                }

                return Effect.succeed(`Found one closed pr with the expected name ${prName}`)
            }),
            Effect.tapError((err) => {
                console.log(`Trying to fetch closed pr with the name ${prName} : ${err}. Retyring again in one second`);
                return Effect.Do;
            })
        ), Schedule.fixed(1000))

const release = Command.make("release", { sematicVersionType }, ({ sematicVersionType }) =>
    Effect.gen(function* () {
        yield* Effect.promise(async () => await $`git switch dev`)
        yield* Effect.promise(async () => await $`git pull`)
        const newVersionNumber = yield* updatePackageJson(sematicVersionType)
        const newVersionPrTitle = yield* incrementVersionAndCreatePr(newVersionNumber)
        yield* waitForClosedPrWithTitle(newVersionPrTitle)
        console.log("Detected closed PR.")
        yield* Effect.promise(async () => await $`gh pr create --title 'Push version ${newVersionNumber} to main' --body 'This pr was automatically generated.' --base main --head dev --web`)
    })
)

// Set up the CLI application
const cli = Command.run(release, {
    name: "Hello World CLI",
    version: "v1.0.0"
})

// Prepare and run the CLI application
cli(process.argv).pipe(Effect.provide(NodeContext.layer), NodeRuntime.runMain)