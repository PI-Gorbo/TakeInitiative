import type { LocationQueryValue } from "vue-router";

/**
 * Sends the user to SamStack's sign-in (auth.samstack.org), which brings them back to `path`.
 * Only a path on this site is honoured, so a crafted `?redirectTo=` cannot point anywhere else.
 */
export function goToSignIn(path: LocationQueryValue | LocationQueryValue[] | undefined) {
    const target = typeof path === "string" && path.startsWith("/") && !path.startsWith("//") ? path : "/app";
    return navigateTo(signInUrl(new URL(target, useRequestURL().origin).href), { external: true });
}
