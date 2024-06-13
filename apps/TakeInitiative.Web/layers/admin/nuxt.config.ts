export default defineNuxtConfig({
    extends: ["../base"],
    app: {
        head: {
            link: [
                {
                    rel: "icon",
                    type: "image/png",
                    href: "yellowDice.png",
                },
            ],
            title: "Admin - Take Initiative",
        },
    },
    devServer: {
        port: 3001,
    },
    runtimeConfig: {
        public: {
            adminUrl: process.env.ADMIN_URL,
        },
    },
});
