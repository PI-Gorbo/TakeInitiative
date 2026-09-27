import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import vm from "node:vm";
import { describe, expect, it } from "vitest";
import type { CampaignSummary } from "~/utils/api/types";
import { NOTE_IMAGES_MAX } from "~/utils/images";
import {
    LAST_CAMPAIGN_KEY,
    SHARED_AT_HEADER,
    SHARE_CACHE,
    SHARE_MAX_AGE_MS,
    SHARE_MESSAGES,
    SHARE_TARGET_PATH,
    captionAfterShare,
    chooseCampaign,
    deleteShare,
    pruneShares,
    readShare,
    rememberCampaign,
    rememberedCampaign,
    shareCaption,
    shareIsEmpty,
    shareKey,
    shareWarnings,
    sharedItemFrom,
    validShareId,
} from "~/utils/shareTarget";

// ── A fake Cache Storage, shared by the service worker and the page ─────────

type Key = string | Request;
const keyOf = (key: Key) => (typeof key === "string" ? key : new URL(key.url).pathname);

class FakeCache {
    entries = new Map<string, Response>();
    async put(key: Key, response: Response) {
        this.entries.set(keyOf(key), response);
    }
    async match(key: Key) {
        return this.entries.get(keyOf(key))?.clone();
    }
    async delete(key: Key) {
        return this.entries.delete(keyOf(key));
    }
    async keys() {
        return [...this.entries.keys()].map((k) => new Request(`https://ti.test${k}`));
    }
}

class FakeCaches {
    caches = new Map<string, FakeCache>();
    async open(name: string) {
        if (!this.caches.has(name)) this.caches.set(name, new FakeCache());
        return this.caches.get(name)! as unknown as Cache;
    }
    async has(name: string) {
        return this.caches.has(name);
    }
    entryCount() {
        return this.caches.get(SHARE_CACHE)?.entries.size ?? 0;
    }
}

// ── The real service worker, run in Node ─────────────────────────────────────

const swSource = readFileSync(fileURLToPath(new URL("../../public/sw.js", import.meta.url)), "utf8");

type Listener = (event: unknown) => void;
function loadServiceWorker(caches: FakeCaches) {
    const listeners = new Map<string, Listener>();
    const self = {
        addEventListener: (type: string, listener: Listener) => listeners.set(type, listener),
        skipWaiting: () => undefined,
        clients: { claim: async () => undefined },
    };
    vm.runInNewContext(swSource, {
        self,
        caches,
        crypto,
        Response,
        FormData,
        URL,
        String,
    });
    /** Dispatches a fetch; resolves the response when the worker answers, or undefined. */
    async function fetch(request: Request): Promise<Response | undefined> {
        let answer: Promise<Response> | undefined;
        listeners.get("fetch")!({ request, respondWith: (r: Promise<Response>) => (answer = r) });
        return answer ? await answer : undefined;
    }
    return { listeners, fetch };
}

const image = (name: string, type = "image/jpeg") => new File([new Uint8Array([1, 2, 3])], name, { type });

function sharePost(form: FormData, url = "https://ti.test/app/share-target") {
    return new Request(url, { method: "POST", body: form });
}

function shareForm(files: File[], fields: Record<string, string> = {}) {
    const form = new FormData();
    for (const file of files) form.append("images", file, file.name);
    for (const [name, value] of Object.entries(fields)) form.append(name, value);
    return form;
}

async function share(files: File[], fields: Record<string, string> = {}) {
    const caches = new FakeCaches();
    const sw = loadServiceWorker(caches);
    const response = await sw.fetch(sharePost(shareForm(files, fields)));
    const location = new URL(response!.headers.get("location")!);
    const id = location.searchParams.get("id")!;
    return { caches, sw, response: response!, location, id };
}

describe("the service worker's share handler", () => {
    it("uses the same names as utils/shareTarget.ts", () => {
        expect(swSource).toContain(`const SHARE_TARGET_PATH = "${SHARE_TARGET_PATH}";`);
        expect(swSource).toContain(`const SHARE_CACHE = "${SHARE_CACHE}";`);
        expect(swSource).toContain(`const SHARED_AT_HEADER = "${SHARED_AT_HEADER}";`);
        expect(swSource).toContain(`const SHARE_MAX_FILES = ${NOTE_IMAGES_MAX};`);
    });

    it("keeps the images and text and answers 303 to /app/share?id=", async () => {
        const { caches, response, location, id } = await share([image("map.jpg"), image("b.png", "image/png")], {
            title: "Cragmaw",
            text: "The hideout",
            url: "https://example.com/map",
        });
        expect(response.status).toBe(303);
        expect(location.pathname).toBe("/app/share");
        expect(validShareId(id)).toBe(id);
        expect(caches.entryCount()).toBe(1);

        const item = await readShare(caches, id);
        expect(item?.files.map((f) => [f.name, f.type])).toEqual([
            ["map.jpg", "image/jpeg"],
            ["b.png", "image/png"],
        ]);
        expect(item?.files[0].size).toBe(3);
        expect(item).toMatchObject({
            title: "Cragmaw",
            text: "The hideout",
            url: "https://example.com/map",
            notImages: 0,
            overLimit: 0,
        });
    });

    it("drops files that are not images", async () => {
        const { caches, id } = await share([image("notes.pdf", "application/pdf"), image("map.webp", "image/webp")]);
        const item = await readShare(caches, id);
        expect(item?.files.map((f) => f.name)).toEqual(["map.webp"]);
        expect(item?.notImages).toBe(1);
        expect(shareWarnings(item!)).toEqual([SHARE_MESSAGES.someNotImages]);
    });

    it("keeps a typeless photo by its extension", async () => {
        const { caches, id } = await share([image("IMG_1.HEIC", ""), image("readme", "")]);
        const item = await readShare(caches, id);
        expect(item?.files.map((f) => f.name)).toEqual(["IMG_1.HEIC"]);
        expect(item?.notImages).toBe(1);
    });

    it("keeps at most 10 images", async () => {
        const files = Array.from({ length: 12 }, (_, i) => image(`p${i}.jpg`));
        const { caches, id } = await share(files);
        const item = await readShare(caches, id);
        expect(item?.files).toHaveLength(10);
        expect(item?.files.at(-1)?.name).toBe("p9.jpg");
        expect(item?.overLimit).toBe(2);
        expect(shareWarnings(item!)).toHaveLength(1);
    });

    it("keeps a share of only non-images as an empty one", async () => {
        const { caches, id } = await share([image("a.pdf", "application/pdf")]);
        const item = await readShare(caches, id);
        expect(shareIsEmpty(item!)).toBe(true);
        expect(item?.notImages).toBe(1);
    });

    it("leaves every other request alone", async () => {
        const caches = new FakeCaches();
        const sw = loadServiceWorker(caches);
        expect(await sw.fetch(new Request("https://ti.test/app/share-target"))).toBeUndefined();
        expect(await sw.fetch(new Request("https://ti.test/api/notes", { method: "POST", body: "{}" }))).toBeUndefined();
        expect(await sw.fetch(new Request("https://ti.test/app/campaigns"))).toBeUndefined();
        expect(caches.entryCount()).toBe(0);
        expect([...sw.listeners.keys()].sort()).toEqual(["activate", "fetch", "install"]);
    });

    it("answers a body it cannot read with /app/share?error=failed", async () => {
        const caches = new FakeCaches();
        const sw = loadServiceWorker(caches);
        const response = await sw.fetch(
            new Request("https://ti.test/app/share-target", {
                method: "POST",
                body: "not a form",
                headers: { "content-type": "multipart/form-data; boundary=x" },
            })
        );
        expect(response?.status).toBe(303);
        expect(response?.headers.get("location")).toBe("https://ti.test/app/share?error=failed");
        expect(caches.entryCount()).toBe(0);
    });
});

describe("reading a share back", () => {
    it("is undefined when missing, and deleteShare removes it", async () => {
        const { caches, id } = await share([image("a.jpg")]);
        expect(await readShare(caches, "00000000-0000-0000-0000-000000000000")).toBeUndefined();
        expect(await readShare(new FakeCaches(), id)).toBeUndefined();
        expect(await readShare(undefined, id)).toBeUndefined();
        await deleteShare(caches, id);
        expect(await readShare(caches, id)).toBeUndefined();
    });

    it("expires after an hour, and pruneShares deletes the old ones only", async () => {
        const caches = new FakeCaches();
        const cache = await caches.open(SHARE_CACHE);
        const now = 10 * SHARE_MAX_AGE_MS;
        const stored = (at: number) =>
            new Response(shareForm([image("a.jpg")]), { headers: { [SHARED_AT_HEADER]: String(at) } });
        const fresh = "11111111-1111-1111-1111-111111111111";
        const old = "22222222-2222-2222-2222-222222222222";
        const undated = "33333333-3333-3333-3333-333333333333";
        await cache.put(shareKey(fresh), stored(now - SHARE_MAX_AGE_MS + 1000));
        await cache.put(shareKey(old), stored(now - SHARE_MAX_AGE_MS - 1000));
        await cache.put(shareKey(undated), new Response(shareForm([])));

        expect(await readShare(caches, old, now)).toBeUndefined();
        expect(caches.entryCount()).toBe(2);
        expect(await pruneShares(caches, now)).toBe(1);
        expect(caches.entryCount()).toBe(1);
        expect((await readShare(caches, fresh, now))?.files).toHaveLength(1);
    });
});

describe("share ids", () => {
    it("takes a UUID only", () => {
        expect(validShareId("0b6f7c1e-9a2d-4c1e-8f3a-2b4c6d8e0f12")).toBe("0b6f7c1e-9a2d-4c1e-8f3a-2b4c6d8e0f12");
        expect(validShareId("../../api")).toBeUndefined();
        expect(validShareId(["x"])).toBeUndefined();
        expect(validShareId(undefined)).toBeUndefined();
    });
});

describe("the caption", () => {
    it("joins title, text and url, leaving repeats out", () => {
        expect(shareCaption({ title: "Map", text: "Cragmaw", url: "https://x.test" })).toBe("Map\nCragmaw\nhttps://x.test");
        expect(shareCaption({ text: "Look at https://x.test", url: "https://x.test" })).toBe("Look at https://x.test");
        expect(shareCaption({})).toBe("");
    });

    it("fills an empty caption and keeps an existing one", () => {
        expect(captionAfterShare("", { text: "Cragmaw" })).toBe("Cragmaw");
        expect(captionAfterShare("  ", { text: "Cragmaw" })).toBe("Cragmaw");
        expect(captionAfterShare("Already @here", { text: "Cragmaw" })).toBe("Already @here");
    });

    it("reads a payload with blank and odd fields", () => {
        const form = shareForm([image("a.jpg")], { title: "  ", notImages: "x", overLimit: "-2" });
        expect(sharedItemFrom(form)).toMatchObject({ title: undefined, notImages: 0, overLimit: 0 });
        expect(shareIsEmpty(sharedItemFrom(shareForm([], { url: "https://x.test" })))).toBe(false);
    });
});

describe("the campaign", () => {
    const campaign = (id: string): CampaignSummary => ({
        id,
        name: id,
        role: "Player",
        isOwner: false,
        memberCount: 2,
    });
    const a = campaign("a");
    const b = campaign("b");

    it("has none to go to", () => {
        expect(chooseCampaign([], "a")).toEqual({ kind: "none" });
    });
    it("goes straight to the only one", () => {
        expect(chooseCampaign([a], undefined)).toEqual({ kind: "go", campaignId: "a" });
        expect(chooseCampaign([a], "gone")).toEqual({ kind: "go", campaignId: "a" });
    });
    it("goes straight to a remembered one the user is still in", () => {
        expect(chooseCampaign([a, b], "b")).toEqual({ kind: "go", campaignId: "b" });
    });
    it("lists them for a stale or missing remembered one", () => {
        expect(chooseCampaign([a, b], "gone")).toEqual({ kind: "pick", campaigns: [a, b] });
        expect(chooseCampaign([a, b], undefined)).toEqual({ kind: "pick", campaigns: [a, b] });
    });

    it("remembers the last campaign, and survives blocked storage", () => {
        const values = new Map<string, string>();
        const storage = {
            getItem: (k: string) => values.get(k) ?? null,
            setItem: (k: string, v: string) => void values.set(k, v),
        } as unknown as Storage;
        expect(rememberedCampaign(storage)).toBeUndefined();
        rememberCampaign(storage, "b");
        expect(values.get(LAST_CAMPAIGN_KEY)).toBe("b");
        expect(rememberedCampaign(storage)).toBe("b");

        const blocked = {
            getItem: () => {
                throw new Error("blocked");
            },
            setItem: () => {
                throw new Error("blocked");
            },
        } as unknown as Storage;
        expect(() => rememberCampaign(blocked, "b")).not.toThrow();
        expect(rememberedCampaign(blocked)).toBeUndefined();
    });
});
