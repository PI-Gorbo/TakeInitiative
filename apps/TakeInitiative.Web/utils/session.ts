// The API's auth cookie. HttpOnly, so client-side JavaScript can never read it — only the
// server render sees it, in the request's `Cookie` header.
export const AUTH_COOKIE_NAME = ".AspNetCore.Cookies";

/** True when a `Cookie` header carries a non-empty auth cookie. */
export function hasAuthCookie(header: string | null | undefined): boolean {
    if (!header) {
        return false;
    }

    return header.split(";").some((pair) => {
        const equals = pair.indexOf("=");
        if (equals < 0) {
            return false;
        }

        // A cleared cookie is sent as `name=`, which is not a session.
        return (
            pair.slice(0, equals).trim() === AUTH_COOKIE_NAME &&
            pair.slice(equals + 1).trim().length > 0
        );
    });
}
