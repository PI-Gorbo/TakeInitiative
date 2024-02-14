import type { CreateAxiosDefaults } from "axios";

// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
    // devtools: { enabled: true, timeline: {enabled: true} },
    typescript: {},
    runtimeConfig: {
        public: {
            // https://medium.com/@hackcharms/how-to-use-axios-in-nuxt3-same-as	-nuxt2-with-typescript-3f4daf524cdd
            axios: <CreateAxiosDefaults>{
                baseURL: process.env.apiUrl,
            },
        },
    },
    css: ["~/assets/main.css", "@fortawesome/fontawesome-svg-core/styles.css"],
    postcss: {
        plugins: {
            tailwindcss: {},
            autoprefixer: {},
        },
    },
    modules: ["@pinia/nuxt"],
});
