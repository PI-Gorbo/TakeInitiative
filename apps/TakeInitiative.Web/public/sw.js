// The service worker: it makes the app installable as a PWA, and it receives the
// Web Share Target's POST (16e). There is no precaching, and every other request
// goes to the network as usual (offline is out of scope, design §12).
// `@vite-pwa/nuxt` registers this file with `injectManifest` and
// `injectionPoint: undefined`. `tests/unit/shareTarget.test.ts` runs it in Node.

// Keep these in step with `utils/shareTarget.ts`.
const SHARE_TARGET_PATH = "/app/share-target";
const SHARE_CACHE = "ti-share";
const SHARE_MAX_FILES = 10;
const SHARED_AT_HEADER = "x-shared-at";

self.addEventListener("install", () => {
    // Activate a new version straight away instead of waiting for old tabs to close.
    self.skipWaiting();
});

self.addEventListener("activate", (event) => {
    // Take control of pages that are already open.
    event.waitUntil(self.clients.claim());
});

// Acts on the share POST only; every other request is left alone.
self.addEventListener("fetch", (event) => {
    const url = new URL(event.request.url);
    if (event.request.method !== "POST" || url.pathname !== SHARE_TARGET_PATH) return;
    event.respondWith(receiveShare(event.request));
});

/**
 * Keeps the shared images and text in Cache Storage and sends the page to
 * `/app/share?id=`, which picks the campaign and hands them to the composer. The
 * files cannot go straight to a page: this runs before any page exists.
 */
async function receiveShare(request) {
    try {
        const payload = sharePayload(await request.formData());
        const id = crypto.randomUUID();
        const cache = await caches.open(SHARE_CACHE);
        await cache.put(
            shareKey(id),
            new Response(payload, { headers: { [SHARED_AT_HEADER]: String(Date.now()) } })
        );
        return Response.redirect(new URL(`/app/share?id=${id}`, request.url).href, 303);
    } catch {
        return Response.redirect(new URL("/app/share?error=failed", request.url).href, 303);
    }
}

/** The part of the share the app keeps: at most 10 images, the text, and what was left out. */
function sharePayload(form) {
    const files = form.getAll("images").filter((value) => typeof value !== "string");
    const images = files.filter(isImageFile);
    const kept = images.slice(0, SHARE_MAX_FILES);
    const payload = new FormData();
    for (const file of kept) payload.append("images", file, file.name);
    for (const name of ["title", "text", "url"]) {
        const value = form.get(name);
        if (typeof value === "string" && value.trim() !== "") payload.append(name, value);
    }
    payload.append("notImages", String(files.length - images.length));
    payload.append("overLimit", String(images.length - kept.length));
    return payload;
}

// A photo can arrive with no type (some HEIC), which multipart turns into
// `application/octet-stream`, so its extension decides then.
function isImageFile(file) {
    const type = (file.type || "").toLowerCase();
    if (type && type !== "application/octet-stream") return type.startsWith("image/");
    return /\.(jpe?g|png|webp|gif|heic|heif|avif)$/i.test(file.name || "");
}

function shareKey(id) {
    return `${SHARE_TARGET_PATH}/${id}`;
}
