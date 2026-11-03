import { describe, expect, it } from "vitest";
import { authCookies, AUTH_COOKIE_NAMES, hasAuthCookie } from "~/utils/session";

describe.each(AUTH_COOKIE_NAMES)("hasAuthCookie (%s)", (name) => {
    it("finds the auth cookie wherever it sits in the header", () => {
        expect(hasAuthCookie(`${name}=abc`)).toBe(true);
        expect(hasAuthCookie(`theme=dark; ${name}=abc; other=1`)).toBe(true);
        expect(hasAuthCookie(`theme=dark;${name}=abc`)).toBe(true);
    });

    it("is false for a cleared cookie", () => {
        expect(hasAuthCookie(`${name}=`)).toBe(false);
        expect(hasAuthCookie(`${name}=  `)).toBe(false);
    });

    it("does not match a different cookie that merely contains the name", () => {
        expect(hasAuthCookie(`not${name}=abc`)).toBe(false);
        expect(hasAuthCookie(`${name}Extra=abc`)).toBe(false);
        expect(hasAuthCookie(`decoy=${name}=abc`)).toBe(false);
    });
});

describe("hasAuthCookie", () => {
    it("is false without a header, or without an auth cookie", () => {
        expect(hasAuthCookie(undefined)).toBe(false);
        expect(hasAuthCookie(null)).toBe(false);
        expect(hasAuthCookie("")).toBe(false);
        expect(hasAuthCookie("theme=dark; other=1")).toBe(false);
    });
});

describe("authCookies", () => {
    it("keeps only the auth cookies, as sent", () => {
        expect(
            authCookies("theme=dark; __Secure-samstack.session_token=tok.sig%3D; .AspNetCore.Cookies=abc"),
        ).toEqual(["__Secure-samstack.session_token=tok.sig%3D"]);
    });

    it("no longer counts the old password sign-in's cookie", () => {
        expect(hasAuthCookie(".AspNetCore.Cookies=abc")).toBe(false);
    });
});
