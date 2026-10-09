// Step 23b: where the suggestion model's files come from, from `runtimeConfig.public.suggestions`
// (nuxt.config.ts, which reads `suggestion-model.json`). Pure.
//
// Default: self-hosted, `/models/{id}/{revision}/…`, which `pnpm models:fetch` fills.
// `NUXT_PUBLIC_SUGGESTIONS_BASE_URL` points it anywhere else, e.g. a Hugging Face repo's
// `…/resolve/{revision}`. Every file is checked against its pinned sha256 wherever it came from.

export interface ModelFile {
    path: string;
    bytes: number;
    sha256: string;
}

/** `runtimeConfig.public.suggestions`. */
export interface SuggestionsConfig {
    id: string;
    name: string;
    version: string;
    revision: string;
    upstream: string;
    licence: string;
    attribution: string;
    threshold: number;
    maxWidth: number;
    maxWords: number;
    maxTokens: number;
    files: ModelFile[];
    /** Empty for self-hosted. */
    baseUrl: string;
}

export interface ResolvedFile extends ModelFile {
    url: string;
}

/** What the worker needs to load the model: absolute URLs and the inference settings. */
export interface ModelSource {
    id: string;
    name: string;
    version: string;
    revision: string;
    selfHosted: boolean;
    /** The host the files come from, for the Me page ("this app" when self-hosted). */
    host: string;
    files: ResolvedFile[];
    bytes: number;
    threshold: number;
    maxWidth: number;
    maxWords: number;
    maxTokens: number;
}

/** The model's files as absolute URLs against `origin` (the page's). */
export function resolveModelSource(config: SuggestionsConfig, origin: string): ModelSource {
    const selfHosted = !config.baseUrl;
    const base = selfHosted ? `/models/${config.id}/${config.revision}` : config.baseUrl;
    const root = new URL(base.replace(/\/?$/, "/"), origin);
    const files = config.files.map((f) => ({ ...f, url: new URL(f.path, root).href }));
    return {
        id: config.id,
        name: config.name,
        version: config.version,
        revision: config.revision,
        selfHosted,
        host: selfHosted ? "this app" : root.host,
        files,
        bytes: config.files.reduce((n, f) => n + f.bytes, 0),
        threshold: config.threshold,
        maxWidth: config.maxWidth,
        maxWords: config.maxWords,
        maxTokens: config.maxTokens,
    };
}

/** "183 MB", in decimal megabytes as downloads are usually shown. */
export function formatMegabytes(bytes: number): string {
    return `${Math.round(bytes / 1e6)} MB`;
}
