export default defineNuxtConfig({
    extends: ["../base"],
    routeRules: {
        "/combat/**": { ssr: false },
    },
    typescript: {
        strict: true,
        typeCheck: true,
    },
});
