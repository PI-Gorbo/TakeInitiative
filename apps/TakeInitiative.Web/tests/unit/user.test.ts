import { describe, expect, it } from "vitest";
import { userFromGetUserError } from "~/utils/user";

// `isAxiosError` tests the flag, so this is enough to stand in for a real one.
const axiosError = (status: number) => ({
    isAxiosError: true,
    response: { status },
});

describe("a failed GET /api/user", () => {
    it("reads a 401 as nobody being signed in", () => {
        expect(userFromGetUserError(axiosError(401))).toBeNull();
    });

    it("rethrows a server error, so it is not cached as signed out", () => {
        const error = axiosError(500);
        expect(() => userFromGetUserError(error)).toThrow(error);
    });

    it("rethrows a 403, which means signed in but not allowed", () => {
        const error = axiosError(403);
        expect(() => userFromGetUserError(error)).toThrow(error);
    });

    it("rethrows anything that is not an axios error", () => {
        const error = new Error("the network went away");
        expect(() => userFromGetUserError(error)).toThrow(error);
    });
});
