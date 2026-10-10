import type { CreateAxiosDefaults } from "axios";
import { readFileSync } from "node:fs";
import { defineNuxtConfig } from "nuxt/config";

// Step 23b: the suggestion model, pinned (id, revision, files and their sha256s). The files are
// served from `public/models/` (git-ignored, filled by `pnpm models:fetch`) unless
// NUXT_PUBLIC_SUGGESTIONS_BASE_URL points somewhere else.
const suggestionModel = JSON.parse(readFileSync(new URL("./suggestion-model.json", import.meta.url), "utf8"));

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
    compatibilityDate: "2024-11-01",
    devtools: { enabled: true, timeline: { enabled: true } },

    app: {
        head: {
            link: [
                {
                    rel: "icon",
                    type: "image/png",
                    href: "/yellowDice.png",
                },
                {
                    rel: "apple-touch-icon",
                    sizes: "180x180",
                    href: "/icons/apple-touch-icon.png",
                },
            ],
            meta: [
                // `viewport-fit=cover` lets the shell pad itself with the safe-area insets.
                // `interactive-widget=resizes-content` is the whole app's default: the keyboard
                // shrinks the layout viewport. iOS ignores it, which `useKeyboardInset` carries.
                {
                    name: "viewport",
                    content:
                        "width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover, interactive-widget=resizes-content",
                },
                { name: "theme-color", content: "#030712" },
                { name: "apple-mobile-web-app-capable", content: "yes" },
                {
                    name: "apple-mobile-web-app-status-bar-style",
                    content: "black-translucent",
                },
            ],
            title: "Take Initiative",
        },
        pageTransition: { name: "fade", mode: "out-in" },
        layoutTransition: { name: "fade", mode: "out-in" },
    },

    routeRules
        : {

        // everything in the main app only on client-side
        '/app/**': {
            ssr
                : false
        },

        // The suggestion model's files (23b): the path holds the revision, so they never change.
        '/models/**': {
            headers: { 'cache-control': 'public, max-age=31536000, immutable' },
        },

    },

    runtimeConfig: {
        public: {
            // https://medium.com/@hackcharms/how-to-use-axios-in-nuxt3-same-as	-nuxt2-with-typescript-3f4daf524cdd
            // The `?? ""` is load-bearing, not tidiness (29d). nuxt.config.ts runs during
            // `nuxt build`, so these are inlined into the bundle — but Nitro re-applies the
            // environment to runtimeConfig on every request (`applyEnv`), which means a
            // container can override them with NUXT_PUBLIC_AXIOS_BASE_URL and
            // NUXT_PUBLIC_WEB_URL (the NUXT_ prefix plus scule's snake_case of the config
            // path). `applyEnv` only walks keys that are already in the built config, and an
            // `undefined` does not survive inlining, so an empty-string default is what keeps
            // the key present and therefore overridable. One image, any domain.
            //
            // `/app/**` being `ssr: false` does not break this: the SPA shell is still
            // rendered per request and writes `config.public` into `window.__NUXT__.config`.
            axios: <CreateAxiosDefaults>{
                baseURL: process.env.API_URL ?? "",
            },
            webUrl: process.env.WEB_URL ?? "",
            // `baseUrl` empty = self-hosted (`/models/{id}/{revision}/`).
            suggestions: {
                id: suggestionModel.id as string,
                name: suggestionModel.name as string,
                version: suggestionModel.version as string,
                revision: suggestionModel.revision as string,
                upstream: suggestionModel.upstream as string,
                licence: suggestionModel.licence as string,
                attribution: suggestionModel.attribution as string,
                threshold: suggestionModel.threshold as number,
                maxWidth: suggestionModel.maxWidth as number,
                maxWords: suggestionModel.maxWords as number,
                maxTokens: suggestionModel.maxTokens as number,
                files: suggestionModel.files as { path: string; bytes: number; sha256: string }[],
                baseUrl: "",
            },
        },
    },
    build: {
        transpile: [
            "@fortawesome/free-brands-svg-icons",
            "@fortawesome/vue-fontawesome",
            "@fortawesome/fontawesome-svg-core",
        ],
    },
    vite: {
        // The extractor worker (23b) is an ES module worker; the runtime is never pre-bundled
        // (its WASM is imported by URL in the worker).
        worker: { format: "es" },
        optimizeDeps: { exclude: ["onnxruntime-web"] },
    },
    css: ["~/assets/index.css", "@fortawesome/fontawesome-svg-core/styles.css"],
    modules: [
        "shadcn-nuxt",
        "@nuxtjs/tailwindcss",
        "@pinia/nuxt",
        "nuxt-typed-router",
        "v-wave/nuxt",
        "@nuxtjs/device",
        "@vite-pwa/nuxt",
    ],

    // Ripple's PWA config (apps/pwa-nuxt): installable, with a hand-written
    // service worker and no precaching or offline support (design §3a, §12).
    pwa: {
        strategies: "injectManifest",
        // Ripple has "../public" because its Nuxt srcDir is `app/`; here it is the root.
        srcDir: "public",
        filename: "sw.js",
        registerType: "autoUpdate",
        injectManifest: {
            injectionPoint: undefined,
        },
        manifest: {
            id: "take-initiative",
            name: "Take Initiative",
            short_name: "Take Initiative",
            description:
                "Run your campaign: session notes, a wiki built from them, and combat.",
            start_url: "/app",
            scope: "/",
            display: "standalone",
            orientation: "portrait",
            // `--background` in assets/index.css (hsl 224 71.4% 4.1%).
            theme_color: "#030712",
            background_color: "#030712",
            icons: [
                {
                    src: "/icons/icon-192.png",
                    sizes: "192x192",
                    type: "image/png",
                },
                {
                    src: "/icons/icon-512.png",
                    sizes: "512x512",
                    type: "image/png",
                    purpose: "any",
                },
                {
                    src: "/icons/icon-maskable-512.png",
                    sizes: "512x512",
                    type: "image/png",
                    purpose: "maskable",
                },
            ],
            // The Web Share Target (16e): images shared from the phone's share sheet.
            // `public/sw.js` receives the POST; `server/routes/app/share-target.post.ts`
            // answers it when no service worker was active yet.
            share_target: {
                action: "/app/share-target",
                method: "POST",
                enctype: "multipart/form-data",
                params: {
                    title: "title",
                    text: "text",
                    url: "url",
                    files: [{ name: "images", accept: ["image/*"] }],
                },
            },
            launch_handler: {
                client_mode: "focus-existing",
            },
            prefer_related_applications: false,
        },
        devOptions: {
            enabled: true,
            type: "classic",
        },
    },

    shadcn: {
        /**
         * Prefix for all the imported component
         */
        prefix: "",
        /**
         * Directory that the component lives in.
         * @default "./components/ui"
         */
        componentDir: "./components/ui",
    },

    typescript: {
        strict: process.env.TAKE_INIT_ENVIRONMENT === 'DEVELOPMENT',
        typeCheck: process.env.TAKE_INIT_ENVIRONMENT === 'DEVELOPMENT',
        tsConfig: {
            compilerOptions: {
                strictNullChecks: true
            }
        }
    },
});
