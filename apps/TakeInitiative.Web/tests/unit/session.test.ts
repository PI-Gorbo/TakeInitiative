import { describe, expect, it } from "vitest";
import { AUTH_COOKIE_NAME, hasAuthCookie } from "~/utils/session";

describe("hasAuthCookie", () => {
    it("finds the auth cookie wherever it sits in the header", () => {
        expect(hasAuthCookie(`${AUTH_COOKIE_NAME}=abc`)).toBe(true);
        expect(hasAuthCookie(`theme=dark; ${AUTH_COOKIE_NAME}=abc; other=1`)).toBe(true);
        expect(hasAuthCookie(`theme=dark;${AUTH_COOKIE_NAME}=abc`)).toBe(true);
    });

    it("is false without a header, or without the cookie", () => {
        expect(hasAuthCookie(undefined)).toBe(false);
        expect(hasAuthCookie(null)).toBe(false);
        expect(hasAuthCookie("")).toBe(false);
        expect(hasAuthCookie("theme=dark; other=1")).toBe(false);
    });

    it("is false for a cleared cookie", () => {
        expect(hasAuthCookie(`${AUTH_COOKIE_NAME}=`)).toBe(false);
        expect(hasAuthCookie(`${AUTH_COOKIE_NAME}=  `)).toBe(false);
    });

    it("does not match a different cookie that merely contains the name", () => {
        expect(hasAuthCookie(`not${AUTH_COOKIE_NAME}=abc`)).toBe(false);
        expect(hasAuthCookie(`${AUTH_COOKIE_NAME}Extra=abc`)).toBe(false);
        expect(hasAuthCookie(`decoy=${AUTH_COOKIE_NAME}=abc`)).toBe(false);
    });
});
