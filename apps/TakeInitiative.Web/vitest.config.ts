import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// Unit tests for pure client logic (tests/unit). They import what they use; Nuxt's
// auto-imports are not available here.
export default defineConfig({
    resolve: {
        alias: { "~": fileURLToPath(new URL("./", import.meta.url)) },
    },
    test: {
        include: ["tests/unit/**/*.test.ts"],
        environment: "node",
    },
});
