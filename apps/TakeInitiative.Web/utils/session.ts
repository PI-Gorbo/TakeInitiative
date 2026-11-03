// The API's auth cookies: SamStack's session (set on `.samstack.org`, `__Secure-` in front on
// https) and, until it is removed, the password sign-in's. HttpOnly, so client-side JavaScript can
// never read them — only the server render sees them, in the request's `Cookie` header.
export const AUTH_COOKIE_NAMES = [
    "__Secure-samstack.session_token",
    "samstack.session_token",
    ".AspNetCore.Cookies",
];

/** The `name=value` pairs of a `Cookie` header that are non-empty auth cookies. */
export function authCookies(header: string | null | undefined): string[] {
    if (!header) {
        return [];
    }

    return header
        .split(";")
        .map((pair) => pair.trim())
        .filter((pair) => {
            const equals = pair.indexOf("=");
            // A cleared cookie is sent as `name=`, which is not a session.
            return (
                equals > 0 &&
                AUTH_COOKIE_NAMES.includes(pair.slice(0, equals).trim()) &&
                pair.slice(equals + 1).trim().length > 0
            );
        });
}

/** True when a `Cookie` header carries a non-empty auth cookie. */
export function hasAuthCookie(header: string | null | undefined): boolean {
    return authCookies(header).length > 0;
}
