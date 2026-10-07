/**
 * Resolving a GHCR tag to the digest it currently points at.
 *
 * The OCI registry API, not GitHub's Packages REST API. Two reasons, and the
 * first is the load-bearing one:
 *
 *   * `PI-Gorbo` is a USER, not an organisation. Ripple's pipeline calls
 *     `/orgs/{org}/packages/container/{image}/versions`, which has no working
 *     equivalent here: the user-scoped route needs a `read:packages` PAT, and the
 *     run's `GITHUB_TOKEN` is not one. Porting that call would have meant adding
 *     the first stored registry credential this repo has managed to avoid.
 *   * The packages are public (docs/roadmap/29-deploy.md, "Visibility"), so
 *     ghcr.io hands out a pull token to anybody who asks. Verified 2026-10-07:
 *     an anonymous token reads both tag lists and manifests.
 *
 * So this module needs no secret at all. If a package is ever flipped back to
 * private, the token request below starts returning 403 and the fix is to make it
 * public again rather than to add a credential.
 */

export const REGISTRY = "ghcr.io";

/** Lowercase because an OCI repository name must be; `PI-Gorbo` is not a valid one. */
export const NAMESPACE = "pi-gorbo";

export const TI_APPS = ["api", "web"] as const;

export type TiApp = (typeof TI_APPS)[number];

/** The GHCR package name per app. These are what `images.yml`'s matrix publishes. */
export const APP_IMAGES = {
    api: "takeinitiative-api",
    web: "takeinitiative-web",
} as const satisfies Record<TiApp, string>;

export const isApp = (value: string): value is TiApp =>
    TI_APPS.some((app) => app === value);

/** `ghcr.io/pi-gorbo/takeinitiative-api`, with no tag or digest. */
export const repositoryOf = (image: string): string =>
    `${REGISTRY}/${NAMESPACE}/${image}`;

/**
 * Every manifest media type a `docker buildx build --push` can land on. Without
 * these the registry answers with a v1 manifest and a DIFFERENT digest from the
 * one the build reported, so the pin would reference something that was never
 * published. `images.yml` builds `linux/arm64` alone, which means a single
 * manifest rather than an index — but both are accepted here, because adding a
 * second platform must not silently change what this resolves to.
 */
const MANIFEST_ACCEPT = [
    "application/vnd.oci.image.index.v1+json",
    "application/vnd.docker.distribution.manifest.list.v2+json",
    "application/vnd.oci.image.manifest.v1+json",
    "application/vnd.docker.distribution.manifest.v2+json",
].join(", ");

/** Narrowed to what this module uses, so a test can supply a stub. */
export type FetchLike = (
    url: string,
    init?: { method?: string; headers?: Record<string, string> },
) => Promise<{
    readonly ok: boolean;
    readonly status: number;
    readonly headers: { readonly get: (name: string) => string | null };
    readonly text: () => Promise<string>;
}>;

const DIGEST_PATTERN = /^sha256:[0-9a-f]{64}$/;

/**
 * Validated rather than trusted. A malformed value here would be PATCHed into
 * Coolify and fail at pull time, which is a much later and vaguer error than
 * this one.
 */
export const validateDigest = (value: string, label: string): string => {
    if (!DIGEST_PATTERN.test(value)) {
        throw new Error(
            `${label} is not a sha256 digest: "${value}". Expected sha256: followed by 64 lowercase hex characters.`,
        );
    }
    return value;
};

/**
 * A tag this pipeline is willing to deploy: a release version from
 * release-please, or a `sha-<short>` build tag for a rollback to one specific
 * build. Deliberately NOT `latest`, `edge` or `buildcache` — those move, and a
 * moving tag resolved now can mean different bytes by the time Coolify pulls.
 */
const VERSION_TAG = /^\d+\.\d+\.\d+$/;
const SHA_TAG = /^sha-[0-9a-f]{7,40}$/;

export const validateDeployableTag = (value: string): string => {
    const tag = value.trim();
    if (VERSION_TAG.test(tag) || SHA_TAG.test(tag)) return tag;
    throw new Error(
        `"${tag}" is not a deployable tag. Expected a release version like 1.2.3, or a build tag like sha-cca7c15. Moving tags (latest, edge) are refused on purpose: resolving one now does not stop it moving before Coolify pulls.`,
    );
};

export type GhcrClient = {
    readonly digestForTag: (image: string, tag: string) => Promise<string | undefined>;
    readonly tagsOf: (image: string) => Promise<readonly string[]>;
};

const describeFailure = (error: unknown): string => {
    const parts: string[] = [];
    let current: unknown = error;
    for (let depth = 0; depth < 5 && current instanceof Error; depth += 1) {
        const code =
            "code" in current && typeof current.code === "string"
                ? ` [${current.code}]`
                : "";
        parts.push(`${current.message}${code}`);
        current = current.cause;
    }
    return parts.length > 0 ? parts.join(" <- ") : String(error);
};

/**
 * One pull token per repository, cached for the process. The token is scoped to a
 * single repository, so resolving both images takes two — but each is good for
 * every call against its own image.
 */
export const ghcrClient = (fetchLike: FetchLike): GhcrClient => {
    const tokens = new Map<string, Promise<string>>();

    const call = async (
        url: string,
        init: { method: string; headers: Record<string, string> },
    ) => {
        try {
            return await fetchLike(url, init);
        } catch (cause: unknown) {
            throw new Error(
                `Could not reach ${REGISTRY}: ${describeFailure(cause)}.`,
            );
        }
    };

    const tokenFor = (image: string): Promise<string> => {
        const cached = tokens.get(image);
        if (cached) return cached;

        const pending = (async (): Promise<string> => {
            const scope = `repository:${NAMESPACE}/${image}:pull`;
            const response = await call(
                `https://${REGISTRY}/token?service=${REGISTRY}&scope=${encodeURIComponent(scope)}`,
                { method: "GET", headers: {} },
            );
            if (!response.ok) {
                throw new Error(
                    `${REGISTRY} refused a pull token for ${NAMESPACE}/${image} (${response.status}). The package is probably private; these resolve anonymously only while it is public.`,
                );
            }
            const parsed: unknown = JSON.parse(await response.text());
            const token =
                typeof parsed === "object" &&
                parsed !== null &&
                "token" in parsed &&
                typeof parsed.token === "string"
                    ? parsed.token
                    : "";
            if (token === "") {
                throw new Error(
                    `${REGISTRY} returned no token for ${NAMESPACE}/${image}.`,
                );
            }
            return token;
        })();

        tokens.set(image, pending);
        return pending;
    };

    return {
        /**
         * `undefined` for a tag that does not exist, which is a real answer rather
         * than a failure: it is what "this release never published an image" looks
         * like, and the caller turns it into a message naming the version.
         */
        digestForTag: async (image, tag) => {
            const token = await tokenFor(image);
            const response = await call(
                `https://${REGISTRY}/v2/${NAMESPACE}/${image}/manifests/${encodeURIComponent(tag)}`,
                {
                    // HEAD: the digest is a response header, so the manifest body is
                    // never needed and an index can be large.
                    method: "HEAD",
                    headers: {
                        Authorization: `Bearer ${token}`,
                        Accept: MANIFEST_ACCEPT,
                    },
                },
            );

            if (response.status === 404) return undefined;
            if (!response.ok) {
                throw new Error(
                    `${REGISTRY} returned ${response.status} for ${NAMESPACE}/${image}:${tag}.`,
                );
            }

            const digest = response.headers.get("docker-content-digest");
            if (!digest) {
                throw new Error(
                    `${REGISTRY} answered for ${NAMESPACE}/${image}:${tag} without a docker-content-digest header, so there is nothing to pin.`,
                );
            }
            return validateDigest(digest.trim(), `${image}:${tag}`);
        },

        /** Only for the run summary, so a failure to list is not worth failing on. */
        tagsOf: async (image) => {
            const token = await tokenFor(image);
            const response = await call(
                `https://${REGISTRY}/v2/${NAMESPACE}/${image}/tags/list`,
                { method: "GET", headers: { Authorization: `Bearer ${token}` } },
            );
            if (!response.ok) return [];

            const parsed: unknown = JSON.parse(await response.text());
            if (
                typeof parsed !== "object" ||
                parsed === null ||
                !("tags" in parsed) ||
                !Array.isArray(parsed.tags)
            ) {
                return [];
            }
            return parsed.tags.filter(
                (tag: unknown): tag is string => typeof tag === "string",
            );
        },
    };
};
