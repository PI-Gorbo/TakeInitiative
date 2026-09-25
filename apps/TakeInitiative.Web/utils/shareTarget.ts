// The Web Share Target (16e). The service worker (`public/sw.js`) keeps a share in
// Cache Storage and sends the page to `/app/share?id=`. This file reads it back,
// chooses the campaign, and turns the shared text into a caption. The rules are pure
// and unit tested; the cache functions take the `CacheStorage` so tests can fake it.
import type { CampaignSummary } from "./api/types";

// Keep these in step with `public/sw.js`.
export const SHARE_TARGET_PATH = "/app/share-target";
export const SHARE_CACHE = "ti-share";
export const SHARED_AT_HEADER = "x-shared-at";
/** A share left unused for this long is deleted when `/app/share` loads. */
export const SHARE_MAX_AGE_MS = 60 * 60 * 1000;

/** The composer takes a share from `?share={id}`, once. */
export const SHARE_PARAM = "share";
/** The campaign layout remembers the last opened campaign here. */
export const LAST_CAMPAIGN_KEY = "ti:lastCampaignId";

export const SHARE_MESSAGES = {
    missing: "Nothing to share. Try sharing again.",
    unavailable:
        "Take Initiative was still starting, so the share did not arrive. Share again.",
    failed: "The share could not be read. Share again.",
    notImages: "Only images can be shared to Take Initiative.",
    someNotImages: "Only images were attached. The other files were left out.",
    noCampaign: "Join or create a campaign first, then share again.",
    pick: "Share to…",
} as const;

export type SharedItem = {
    files: File[];
    title?: string;
    text?: string;
    url?: string;
    /** Shared files that were not images, left out by the service worker. */
    notImages: number;
    /** Images past the 10 a note can have, left out by the service worker. */
    overLimit: number;
};

/** A share id is the service worker's `crypto.randomUUID()`. Anything else is ignored. */
export function validShareId(value: unknown): string | undefined {
    return typeof value === "string" && /^[0-9a-f-]{36}$/i.test(value) ? value : undefined;
}

export const shareKey = (id: string) => `${SHARE_TARGET_PATH}/${id}`;

/** Reads the service worker's payload (the multipart form it stored). */
export function sharedItemFrom(form: FormData): SharedItem {
    const text = (name: string) => {
        const value = form.get(name);
        return typeof value === "string" && value.trim() !== "" ? value.trim() : undefined;
    };
    const count = (name: string) => {
        const value = Number(form.get(name));
        return Number.isFinite(value) && value > 0 ? Math.floor(value) : 0;
    };
    return {
        files: form.getAll("images").filter((v): v is File => typeof v !== "string"),
        title: text("title"),
        text: text("text"),
        url: text("url"),
        notImages: count("notImages"),
        overLimit: count("overLimit"),
    };
}

/** Whether a share carries anything the composer can use. */
export const shareIsEmpty = (item: SharedItem) =>
    item.files.length === 0 && !item.title && !item.text && !item.url;

/**
 * The caption a share suggests: its title, text and URL, one per line, with any
 * repeat left out (Android often sends the URL inside the text as well).
 */
export function shareCaption(item: Pick<SharedItem, "title" | "text" | "url">): string {
    const parts: string[] = [];
    for (const part of [item.title, item.text, item.url]) {
        const value = part?.trim();
        if (!value || parts.some((p) => p.includes(value))) continue;
        parts.push(value);
    }
    return parts.join("\n");
}

/** The shared text fills an empty caption only; one already written is kept. */
export const captionAfterShare = (current: string, item: Pick<SharedItem, "title" | "text" | "url">) =>
    current.trim() === "" ? shareCaption(item) : current;

/** What the toasts say about what the service worker left out. */
export function shareWarnings(item: SharedItem): string[] {
    const warnings: string[] = [];
    if (item.notImages > 0 && !shareIsEmpty(item)) warnings.push(SHARE_MESSAGES.someNotImages);
    if (item.overLimit > 0) warnings.push(`A note can have at most 10 images. The first 10 were attached.`);
    return warnings;
}

// ── The campaign ─────────────────────────────────────────────────────────────

export type CampaignChoice =
    | { kind: "none" }
    | { kind: "go"; campaignId: string }
    | { kind: "pick"; campaigns: CampaignSummary[] };

/**
 * One campaign, or a remembered one the user is still in: straight there. Otherwise
 * the list ("Share to…"). A remembered campaign is never in that list, since it goes
 * straight there, so the list keeps the campaigns' own order.
 */
export function chooseCampaign(
    campaigns: readonly CampaignSummary[],
    remembered: string | null | undefined
): CampaignChoice {
    if (campaigns.length === 0) return { kind: "none" };
    if (campaigns.length === 1) return { kind: "go", campaignId: campaigns[0].id };
    const last = campaigns.find((c) => c.id === remembered);
    if (last) return { kind: "go", campaignId: last.id };
    return { kind: "pick", campaigns: [...campaigns] };
}

export function rememberCampaign(storage: Storage | undefined, campaignId: string) {
    try {
        if (campaignId) storage?.setItem(LAST_CAMPAIGN_KEY, campaignId);
    } catch {
        // Storage is full or blocked: the share page lists the campaigns instead.
    }
}

export function rememberedCampaign(storage: Storage | undefined): string | undefined {
    try {
        return storage?.getItem(LAST_CAMPAIGN_KEY) ?? undefined;
    } catch {
        return undefined;
    }
}

// ── Cache Storage ────────────────────────────────────────────────────────────

type ShareCaches = Pick<CacheStorage, "open" | "has">;

/** The share, or undefined when it is missing, expired or unreadable. */
export async function readShare(
    caches: ShareCaches | undefined,
    id: string,
    now: number = Date.now()
): Promise<SharedItem | undefined> {
    if (!caches || !(await caches.has(SHARE_CACHE))) return undefined;
    const cache = await caches.open(SHARE_CACHE);
    const response = await cache.match(shareKey(id));
    if (!response) return undefined;
    if (isExpired(response, now)) {
        await cache.delete(shareKey(id));
        return undefined;
    }
    try {
        return sharedItemFrom(await response.formData());
    } catch {
        return undefined;
    }
}

export async function deleteShare(caches: ShareCaches | undefined, id: string): Promise<void> {
    if (!caches || !(await caches.has(SHARE_CACHE))) return;
    await (await caches.open(SHARE_CACHE)).delete(shareKey(id));
}

/** Deletes every share older than an hour. */
export async function pruneShares(
    caches: ShareCaches | undefined,
    now: number = Date.now()
): Promise<number> {
    if (!caches || !(await caches.has(SHARE_CACHE))) return 0;
    const cache = await caches.open(SHARE_CACHE);
    let deleted = 0;
    for (const request of await cache.keys()) {
        const response = await cache.match(request);
        if (!response || isExpired(response, now)) {
            await cache.delete(request);
            deleted++;
        }
    }
    return deleted;
}

export function isExpired(response: Pick<Response, "headers">, now: number): boolean {
    const at = Number(response.headers.get(SHARED_AT_HEADER));
    return !Number.isFinite(at) || at <= 0 || now - at > SHARE_MAX_AGE_MS;
}

/** The browser's Cache Storage, when this page may use it (a secure context). */
export function browserCaches(): CacheStorage | undefined {
    return typeof caches === "undefined" ? undefined : caches;
}
