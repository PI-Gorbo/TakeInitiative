export default defineNuxtConfig({
    extends: ["../base"],

    routeRules: {
        "/combat/**": { ssr: false },
    },

    //   typescript: {
    //       strict: true,
    //       typeCheck: true,
    //   },

    compatibilityDate: "2024-09-03",
});
