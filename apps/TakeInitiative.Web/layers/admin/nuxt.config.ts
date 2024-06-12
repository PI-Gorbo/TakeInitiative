export default defineNuxtConfig({
    extends: ["../.."],
    devServer: {
        port: 3001,
    },
    runtimeConfig: {
        public: {
            adminUrl: process.env.ADMIN_URL,
        },
    },
});
