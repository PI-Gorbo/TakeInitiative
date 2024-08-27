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
                "take-teal": {
                    DEFAULT: "#019D89",
                },
                "take-charcoal": {
                    DEFAULT: "#264652",
                },
                "take-purple": {
                    light: "#36374f",
                    DEFAULT: "#2a2635",
                    dark: "#171322",
                    "very-dark": "#040307",
                },
                "take-red": {
                    DEFAULT: "#FF652E",
                },
                "take-yellow": {
                    light: "#fdc465",
                    DEFAULT: "#FBB426",
                    dark: "#da9c20",
                },
                "take-grey": {
                    light: "#E7E7E7",
                    DEFAULT: "#D1D1D1",
                    dark: "#686868",
                },
                "take-cream": {
                    DEFAULT: "#FDF2C1",
                    medium: "#F2CC8F",
                },
            },
        },
        fontFamily: {
            NovaCut: ["NovaCut"],
            Overpass: ["Overpass"],
        },
    },
    safelist: [
        {
            // Automatically imports all the colours since we want them for changing colours based on variables.
            pattern:
                /bg-take-(navy|navy-light|navy-medium|navy-dark|red|yellow|yellow-light|yellow-dark|purple|purple-dark|purple-very-dark|purple-light|cream)/,
            variants: ["hover", "group-hover"],
        },
        {
            pattern:
                /text-take-(navy|navy-light|navy-medium|navy-dark|red|yellow|yellow-light|yellow-dark|purple|purple-dark|purple-very-dark|purple-light|cream)/,
            variants: ["hover", "group-hover"],
        },
        {
            pattern:
                /border-take-(navy|navy-light|navy-medium|navy-dark|red|yellow|yellow-light|yellow-dark|purple|purple-dark|purple-very-dark|purple-light|cream)/,
            variants: ["hover", "group-hover"],
        },
    ],
    plugins: [require("daisyui")],
};
