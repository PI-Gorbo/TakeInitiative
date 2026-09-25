import type { CreateAxiosDefaults } from "axios";
import { defineNuxtConfig } from "nuxt/config";

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
                // From Ripple: `viewport-fit=cover` lets the shell pad itself with
                // the safe-area insets, and `interactive-widget=overlays-content`
                // keeps the layout still when the on-screen keyboard opens.
                {
                    name: "viewport",
                    content:
                        "width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover, interactive-widget=overlays-content",
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

    },

    runtimeConfig: {
        public: {
            // https://medium.com/@hackcharms/how-to-use-axios-in-nuxt3-same-as	-nuxt2-with-typescript-3f4daf524cdd
            axios: <CreateAxiosDefaults>{
                baseURL: process.env.API_URL,
            },
            webUrl: process.env.WEB_URL,
        },
    },
    build: {
        transpile: [
            "@fortawesome/free-brands-svg-icons",
            "@fortawesome/vue-fontawesome",
            "@fortawesome/fontawesome-svg-core",
        ],
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
