import type { CreateAxiosDefaults } from "axios";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

// https://nuxt.com/docs/api/configuration/nuxt-config
const executionDirectory = dirname(fileURLToPath(import.meta.url));
export default defineNuxtConfig({
    app: {
        head: {
            link: [
                {
                    rel: "icon",
                    type: "image/png",
                    href: "~/public/img/yellowDice.png",
                },
            ],
            title: "Take Initiative",
        },
        pageTransition: { name: "fade", mode: "out-in" },
    },
    devtools: { enabled: true },
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
            "vue-toastification",
        ],
    },
    css: [
        join(executionDirectory, "./assets/main.css"),
        "@fortawesome/fontawesome-svg-core/styles.css",
    ],
    postcss: {
        plugins: {
            tailwindcss: {},
            autoprefixer: {},
        },
    },
    modules: ["@pinia/nuxt", "@nuxtjs/device"],
    plugins: [],
    alias: {
        base: executionDirectory,
    },
    typescript: {
        strict: true,
        typeCheck: true,
    },
});
