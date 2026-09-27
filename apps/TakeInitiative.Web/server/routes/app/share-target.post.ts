// The share target's POST when no service worker was active yet (16e), e.g. the first
// share right after installing. The files cannot be kept here, so the page says so.
export default defineEventHandler((event) => sendRedirect(event, "/app/share?error=unavailable", 303));
