import type { CreateAxiosDefaults } from "axios";
import { defineNuxtConfig } from "nuxt/config";

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
    compatibilityDate: "2024-11-01",
    devtools: { enabled: true },

    devServer: {
        port: 3002,
    },

    app: {
        head: {
            link: [
                {
                    rel: "icon",
                    type: "image/png",
                    href: "img/yellowDice.png",
                },
            ],
            title: "Take Initiative",
        },
        pageTransition: { name: "fade", mode: "out-in" },
        layoutTransition: { name: "fade", mode: "out-in" },
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
    ],

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
        strict: true,
    },
});
