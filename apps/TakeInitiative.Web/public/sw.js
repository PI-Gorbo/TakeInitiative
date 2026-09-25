// Minimal service worker: it exists so the app is installable as a PWA.
// There is no precaching and no fetch handler, so every request goes to the
// network as usual (offline is out of scope, design §12). `@vite-pwa/nuxt`
// registers this file with `injectManifest` and `injectionPoint: undefined`.

self.addEventListener("install", () => {
    // Activate a new version straight away instead of waiting for old tabs to close.
    self.skipWaiting();
});

self.addEventListener("activate", (event) => {
    // Take control of pages that are already open.
    event.waitUntil(self.clients.claim());
});
