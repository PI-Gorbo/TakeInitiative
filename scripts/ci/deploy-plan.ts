/**
 * What `main` should deploy, decided here rather than in `if:` expressions.
 *
 * One primitive: **the release already built the artefact, so main never builds.**
 * release-please publishes an immutable `1.2.3` tag from `dev` (see
 * `.github/workflows/release.yml` and `images.yml`), and a push to `main` resolves
 * that tag to the digest it points at and deploys exactly those bytes. There is no
 * content-addressed key here and no need for one: the version tag never moves, so
 * it already answers "is this the artefact dev published?" — which is the only
 * question Ripple's `image-key.ts` exists to answer. Ripple needs the hashing
 * because it deploys every `dev` push, where no release version exists yet.
 *
 * Consequences worth stating, because they are the design:
 *   * A push to main that is not a release merge resolves the version already
 *     running, and `coolify-deploy.ts` skips it as "already running this digest
 *     and healthy". Pushing docs to main does not redeploy prod.
 *   * A release whose images never published fails HERE, naming the version,
 *     rather than at pull time inside Coolify.
 *   * A rollback is `workflow_dispatch` with `pin_tag`, and it deploys an older
 *     published tag without rebuilding it — rebuilding is what would make it a
 *     different artefact.
 *
 * There is deliberately NO migrate job. The API applies the Marten schema at boot
 * (`Marten__ApplySchemaOnStartup=true`) before the port opens, which is safe
 * because `AddAsyncDaemon(DaemonMode.Solo)` already means exactly one API
 * container. See docs/deploy/pipeline.md for when that stops being true.
 */

import { appendFileSync, readFileSync } from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

import {
    APP_IMAGES,
    type GhcrClient,
    type TiApp,
    TI_APPS,
    ghcrClient,
    isApp,
    repositoryOf,
    validateDeployableTag,
} from "./ghcr.ts";

export const REPO_ROOT = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "..",
    "..",
);

export const TARGETS_FILE = "deploy-targets.json";

export type AppTarget = {
    readonly enabled: boolean;
    readonly coolifyApplicationUuid: string;
};

export type Environment = {
    readonly branch: string;
    readonly githubEnvironment: string;
    readonly apps: Readonly<Record<TiApp, AppTarget>>;
};

export type DeployTargets = {
    readonly environments: Readonly<Record<string, Environment>>;
};

const isJsonObject = (value: unknown): value is Readonly<Record<string, unknown>> =>
    typeof value === "object" && value !== null && !Array.isArray(value);

const expectString = (value: unknown, label: string): string => {
    if (typeof value !== "string") throw new Error(`${label} is not a string.`);
    return value;
};

const expectBoolean = (value: unknown, label: string): boolean => {
    if (typeof value !== "boolean") throw new Error(`${label} is not a boolean.`);
    return value;
};

/**
 * Validated rather than trusted, because every field here either selects an
 * Environment's secrets or names the Coolify application a deploy will PATCH. A
 * typo that read as "absent" would deploy nothing and report success.
 */
export const parseDeployTargets = (raw: string): DeployTargets => {
    const parsed: unknown = JSON.parse(raw);
    if (!isJsonObject(parsed) || !isJsonObject(parsed.environments)) {
        throw new Error(`${TARGETS_FILE} has no "environments" object.`);
    }

    const environments = Object.fromEntries(
        Object.entries(parsed.environments).map(([name, value]) => {
            const label = `environments.${name}`;
            if (!isJsonObject(value)) throw new Error(`${label} is not an object.`);
            if (!isJsonObject(value.apps)) {
                throw new Error(`${label}.apps is not an object.`);
            }

            const apps = Object.fromEntries(
                TI_APPS.map((app) => {
                    const target = (value.apps as Record<string, unknown>)[app];
                    if (!isJsonObject(target)) {
                        throw new Error(`${label}.apps.${app} is missing.`);
                    }
                    return [
                        app,
                        {
                            enabled: expectBoolean(
                                target.enabled,
                                `${label}.apps.${app}.enabled`,
                            ),
                            coolifyApplicationUuid: expectString(
                                target.coolifyApplicationUuid,
                                `${label}.apps.${app}.coolifyApplicationUuid`,
                            ),
                        },
                    ];
                }),
            ) as Record<TiApp, AppTarget>;

            return [
                name,
                {
                    branch: expectString(value.branch, `${label}.branch`),
                    githubEnvironment: expectString(
                        value.githubEnvironment,
                        `${label}.githubEnvironment`,
                    ),
                    apps,
                },
            ];
        }),
    );

    return { environments };
};

export const environmentForBranch = (
    targets: DeployTargets,
    branch: string,
): readonly [string, Environment] => {
    const found = Object.entries(targets.environments).find(
        ([, environment]) => environment.branch === branch,
    );
    if (!found) {
        throw new Error(
            `No environment in ${TARGETS_FILE} deploys from "${branch}". Known branches: ${Object.values(
                targets.environments,
            )
                .map((environment) => environment.branch)
                .join(", ")}.`,
        );
    }
    return found;
};

/** A blank `targets` input means every app; otherwise a comma-separated subset. */
export const selectApps = (raw: string): readonly TiApp[] => {
    const requested = raw
        .split(",")
        .map((value) => value.trim())
        .filter((value) => value !== "");
    if (requested.length === 0) return TI_APPS;

    return requested.map((value) => {
        if (!isApp(value)) {
            throw new Error(
                `Unknown app "${value}" in targets; expected ${TI_APPS.join(" or ")}.`,
            );
        }
        return value;
    });
};

/**
 * The version release-please owns. `sync-version.mjs` propagates it into the csproj
 * on the release branch, so by the time the release merges and main moves, this
 * field and the published image tag are the same string.
 */
export const versionFromManifest = (raw: string): string => {
    const parsed: unknown = JSON.parse(raw);
    if (!isJsonObject(parsed) || typeof parsed.version !== "string") {
        throw new Error("The root package.json has no version field.");
    }
    return parsed.version;
};

export type AppPlan = {
    readonly app: TiApp;
    readonly image: string;
    readonly repository: string;
    readonly tag: string;
    readonly digest: string;
    readonly uuid: string;
    readonly skipped?: string;
};

export type DeployPlan = {
    readonly environmentName: string;
    readonly githubEnvironment: string;
    readonly branch: string;
    readonly sha: string;
    readonly tag: string;
    readonly pinned: boolean;
    readonly forceDeploy: boolean;
    readonly apps: readonly AppPlan[];
};

/**
 * An app with `enabled: false` is reported and skipped; an enabled one with no UUID
 * is an error. The asymmetry is deliberate — the first is the documented
 * off-switch, the second is a half-finished config that would otherwise PATCH
 * nothing and pass.
 */
export const planApp = ({
    app,
    tag,
    digest,
    target,
}: {
    readonly app: TiApp;
    readonly tag: string;
    readonly digest: string | undefined;
    readonly target: AppTarget;
}): AppPlan => {
    const image = APP_IMAGES[app];
    const base = {
        app,
        image,
        repository: repositoryOf(image),
        tag,
        uuid: target.coolifyApplicationUuid,
    };

    if (!target.enabled) {
        return { ...base, digest: "", skipped: "disabled in deploy-targets.json" };
    }
    if (target.coolifyApplicationUuid === "") {
        throw new Error(
            `${app} is enabled in ${TARGETS_FILE} but has no coolifyApplicationUuid. Create the Coolify application first, then commit its UUID — see docs/deploy/pipeline.md.`,
        );
    }
    if (digest === undefined) {
        throw new Error(
            `${repositoryOf(image)}:${tag} is not published, so there is nothing to deploy. A release publishes both images from release.yml; check that run finished. To deploy a different build, dispatch this workflow with pin_tag.`,
        );
    }

    return { ...base, digest };
};

export const deployable = (plan: DeployPlan): readonly AppPlan[] =>
    plan.apps.filter((app) => app.skipped === undefined);

/** `include:` so the matrix carries one object per app rather than a cross product. */
export const deployMatrix = (plan: DeployPlan): string =>
    JSON.stringify({
        include: deployable(plan).map((app) => ({
            app: app.app,
            image: app.image,
            repository: app.repository,
            digest: app.digest,
            uuid: app.uuid,
        })),
    });

export const formatOutputs = (
    plan: DeployPlan,
): readonly (readonly [string, string])[] => [
    ["environment-name", plan.environmentName],
    ["github-environment", plan.githubEnvironment],
    ["tag", plan.tag],
    ["deploy-matrix", deployMatrix(plan)],
    ["deploy-count", String(deployable(plan).length)],
    ["force-deploy", String(plan.forceDeploy)],
];

export const summaryMarkdown = (
    plan: DeployPlan,
    recentTags: Readonly<Record<string, readonly string[]>>,
): string => {
    const rows = plan.apps.map((app) =>
        app.skipped === undefined
            ? `| \`${app.app}\` | \`${app.tag}\` | \`${app.digest}\` | deploy |`
            : `| \`${app.app}\` | \`${app.tag}\` | — | skipped: ${app.skipped} |`,
    );

    const published = Object.entries(recentTags).map(
        ([image, tags]) =>
            `- \`${image}\`: ${
                tags.length === 0
                    ? "no tags listed"
                    : tags
                          .filter((tag) => tag !== "buildcache")
                          .slice(-8)
                          .map((tag) => `\`${tag}\``)
                          .join(", ")
            }`,
    );

    return [
        `## Deploy plan — ${plan.environmentName}`,
        "",
        `Branch \`${plan.branch}\` at \`${plan.sha.slice(0, 7)}\`, deploying tag \`${plan.tag}\`${
            plan.pinned ? " (**pinned by dispatch** — this is a rollback)" : ""
        }.`,
        plan.forceDeploy ? "\nForced: Coolify is redeployed even if it already runs this digest." : "",
        "",
        "| app | tag | digest | action |",
        "| --- | --- | --- | --- |",
        ...rows,
        "",
        "Nothing is built here. Every digest above was published by the release that",
        "created the tag, so these are the bytes `dev` ran.",
        "",
        "<details><summary>Recently published tags</summary>",
        "",
        ...published,
        "",
        "</details>",
    ]
        .filter((line) => line !== "")
        .join("\n");
};

const requireEnv = (name: string): string => {
    const value = process.env[name];
    if (!value) throw new Error(`${name} is not set.`);
    return value;
};

const writeOutputs = (entries: readonly (readonly [string, string])[]): void => {
    const rendered = entries.map(([key, value]) => `${key}=${value}`).join("\n");
    const file = process.env.GITHUB_OUTPUT;
    if (file) appendFileSync(file, `${rendered}\n`);
    else console.log(rendered);
};

const writeSummary = (markdown: string): void => {
    const file = process.env.GITHUB_STEP_SUMMARY;
    if (file) appendFileSync(file, `${markdown}\n`);
    else console.log(markdown);
};

export const buildPlan = async ({
    branch,
    sha,
    pinTag,
    forceDeploy,
    targets,
    targetsRaw,
    manifestRaw,
    client,
}: {
    readonly branch: string;
    readonly sha: string;
    readonly pinTag: string;
    readonly forceDeploy: boolean;
    readonly targets: string;
    readonly targetsRaw: string;
    readonly manifestRaw: string;
    readonly client: GhcrClient;
}): Promise<DeployPlan> => {
    const [environmentName, environment] = environmentForBranch(
        parseDeployTargets(targetsRaw),
        branch,
    );

    const pinned = pinTag.trim() !== "";
    const tag = pinned
        ? validateDeployableTag(pinTag)
        : validateDeployableTag(versionFromManifest(manifestRaw));

    const apps: AppPlan[] = [];
    for (const app of selectApps(targets)) {
        const digest = environment.apps[app].enabled
            ? await client.digestForTag(APP_IMAGES[app], tag)
            : undefined;
        apps.push({
            ...planApp({ app, tag, digest, target: environment.apps[app] }),
        });
    }

    return {
        environmentName,
        githubEnvironment: environment.githubEnvironment,
        branch,
        sha,
        tag,
        pinned,
        forceDeploy,
        apps,
    };
};

const runPlan = async (): Promise<void> => {
    const client = ghcrClient(fetch);
    const plan = await buildPlan({
        branch: requireEnv("GITHUB_REF_NAME"),
        sha: requireEnv("GITHUB_SHA"),
        pinTag: process.env.PIN_TAG ?? "",
        forceDeploy: process.env.FORCE_DEPLOY === "true",
        targets: process.env.TARGETS ?? "",
        targetsRaw: readFileSync(path.join(REPO_ROOT, TARGETS_FILE), "utf8"),
        manifestRaw: readFileSync(path.join(REPO_ROOT, "package.json"), "utf8"),
        client,
    });

    const recentTags: Record<string, readonly string[]> = {};
    for (const app of TI_APPS) {
        recentTags[APP_IMAGES[app]] = await client.tagsOf(APP_IMAGES[app]);
    }

    writeOutputs(formatOutputs(plan));
    writeSummary(summaryMarkdown(plan, recentTags));
};

/** Prints what the current checkout would deploy, without needing a GitHub run. */
const runExplain = async (): Promise<void> => {
    const plan = await buildPlan({
        branch: process.env.GITHUB_REF_NAME ?? "main",
        sha: process.env.GITHUB_SHA ?? "0000000",
        pinTag: process.env.PIN_TAG ?? "",
        forceDeploy: false,
        targets: process.env.TARGETS ?? "",
        targetsRaw: readFileSync(path.join(REPO_ROOT, TARGETS_FILE), "utf8"),
        manifestRaw: readFileSync(path.join(REPO_ROOT, "package.json"), "utf8"),
        client: ghcrClient(fetch),
    });
    console.log(summaryMarkdown(plan, {}));
};

const main = async (): Promise<void> => {
    const [command] = process.argv.slice(2);
    if (command === "plan") return runPlan();
    if (command === "explain") return runExplain();
    throw new Error("Usage: deploy-plan.ts <plan|explain>");
};

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main().catch((error: unknown) => {
        console.error(error instanceof Error ? error.message : error);
        process.exitCode = 1;
    });
}
