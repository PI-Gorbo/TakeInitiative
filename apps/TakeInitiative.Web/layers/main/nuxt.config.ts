export default defineNuxtConfig({
    extends: ["../base"],

    routeRules: {
        "/combat/**": { ssr: false },
    },

    compatibilityDate: "2024-09-03",
});
