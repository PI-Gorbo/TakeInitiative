/**
 * Liveness probe for the container's HEALTHCHECK and for Coolify (29d).
 *
 * Deliberately trivial: it answers as soon as Nitro is listening and checks nothing
 * downstream. `/` is a real page, and probing it every 30s would render the landing
 * page forever; this costs nothing. It is not a readiness probe — the web app has no
 * dependency whose blip should make an orchestrator restart it.
 */
export default defineEventHandler(() => ({ ok: true }));
