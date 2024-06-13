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
            title: "Take Initiative",
        },
    },
});
