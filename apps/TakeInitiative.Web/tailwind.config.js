/** @type {import('tailwindcss').Config} */
export default {
    content: ["./layers/**/*.{js,vue,ts}"],
    theme: {
        extend: {
            colors: {
                "take-navy": {
                    light: "#4A6082",
                    medium: "#152846",
                    dark: "#1f2b43",
                    DEFAULT: "#020617",
                },
                "take-red": {
                    DEFAULT: "#EA5A36",
                },
                "take-yellow": {
                    light: "#fce9cc",
                    DEFAULT: "#FFD28F",
                    dark: "#fcc385",
                },
                "take-grey": {
                    light: "#E7E7E7",
                    DEFAULT: "#D1D1D1",
                    dark: "#A7A7A7",
                },
                "ti-orange": {
                    DEFAULT: "#d85a33",
                },
            },
        },
        fontFamily: {
            NovaCut: ["NovaCut"],
        },
    },
    safelist: [
        {
            // Automatically imports all the colours since we want them for changing colours based on variables.
            pattern:
                /bg-take-(navy|navy-light|navy-medium|navy-dark|red|yellow|yellow-light|yellow-dark)/,
            variants: ["hover", "group-hover"],
        },
        {
            pattern:
                /text-take-(navy|navy-light|navy-medium|navy-dark|red|yellow|yellow-light|yellow-dark)/,
            variants: ["hover", "group-hover"],
        },
        {
            pattern:
                /border-take-(navy|navy-light|navy-medium|navy-dark|red|yellow|yellow-light|yellow-dark)/,
            variants: ["hover", "group-hover"],
        },
    ],
    plugins: [require("daisyui")],
};
