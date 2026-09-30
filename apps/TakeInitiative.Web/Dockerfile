# The web image. Built from the REPO ROOT (pnpm needs the workspace lockfile), with
# apps/TakeInitiative.Web/Dockerfile.dockerignore next door trimming the context.
#
# Nothing environment-specific is baked in: the API and web URLs are read at request
# time from NUXT_PUBLIC_AXIOS_BASE_URL / NUXT_PUBLIC_WEB_URL, so one image serves any
# domain. See docs/roadmap/29-deploy.md, "29d".
#
# `.output` is architecture-independent (no native modules: onnxruntime-web is WASM,
# @huggingface/tokenizers is pure JS, there is no sharp), so the build stage is pinned
# to the BUILDER's platform and only the tiny runtime stage varies per target. That
# makes a multi-arch manifest cheap and keeps QEMU out of it entirely.

FROM --platform=$BUILDPLATFORM node:24-alpine AS build
RUN apk add --no-cache libc6-compat
RUN corepack enable
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
WORKDIR /repo

# Manifests first, so the install layer is cached until a dependency changes.
# pnpm wants every workspace member's package.json to satisfy --frozen-lockfile;
# the .NET-only members are empty importers, but copying them keeps the install
# exact instead of relying on pnpm tolerating a missing importer.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/TakeInitiative.Api/package.json apps/TakeInitiative.Api/
COPY apps/TakeInitiative.Api.Tests/package.json apps/TakeInitiative.Api.Tests/
COPY apps/TakeInitiative.Web/package.json apps/TakeInitiative.Web/
COPY packages/TakeInitiative.Dice/package.json packages/TakeInitiative.Dice/
COPY packages/TakeInitiative.Dice.Tests/package.json packages/TakeInitiative.Dice.Tests/
COPY packages/TakeInitiative.KnowledgeBase/package.json packages/TakeInitiative.KnowledgeBase/
COPY packages/TakeInitiative.KnowledgeBase.Tests/package.json packages/TakeInitiative.KnowledgeBase.Tests/
# --ignore-scripts skips the web package's `nuxt prepare` postinstall, which
# needs source that has not been copied yet. `nuxt build` re-runs it anyway.
RUN pnpm install --frozen-lockfile --ignore-scripts

# The suggestion model's weights (23b), fetched at their pinned sha256s into their own layer,
# which stays cached until the manifest changes. `--build-arg SUGGESTIONS_MODEL=skip` leaves
# them out (the app still builds; suggestions then need NUXT_PUBLIC_SUGGESTIONS_BASE_URL).
ARG SUGGESTIONS_MODEL=fetch
ARG SUGGESTIONS_MODEL_FROM
COPY apps/TakeInitiative.Web/suggestion-model.json apps/TakeInitiative.Web/
COPY scripts/models/ scripts/models/
RUN if [ "$SUGGESTIONS_MODEL" != "skip" ]; then \
        SUGGESTIONS_MODEL_FROM="$SUGGESTIONS_MODEL_FROM" node scripts/models/fetch-model.mjs --out /models; \
    else mkdir -p /models; fi

COPY apps/TakeInitiative.Web/ apps/TakeInitiative.Web/
RUN mkdir -p apps/TakeInitiative.Web/public/models && cp -R /models/. apps/TakeInitiative.Web/public/models/

# Build-time defaults only, kept so compose.dev.yml's `args:` still work unchanged.
# In production leave them unset and set NUXT_PUBLIC_AXIOS_BASE_URL / NUXT_PUBLIC_WEB_URL
# on the container instead: nuxt.config.ts defaults both to "" so the keys survive into
# the built config, and Nitro replaces them per request.
ARG API_URL
ARG WEB_URL
ENV API_URL=$API_URL
ENV WEB_URL=$WEB_URL
ENV TAKE_INIT_ENVIRONMENT=PRODUCTION
RUN pnpm --filter @ti/web run build

# ---
# Runtime. Per target platform, and deliberately nothing but node plus `.output`:
# no pnpm store, no node_modules, no dev dependencies, no source.
FROM node:24-alpine AS production
WORKDIR /app
RUN addgroup --system --gid 1001 nodejs && adduser --system --uid 1001 --ingroup nodejs nuxtjs

# `.output` is self-contained: server bundle plus public assets. Left owned by root so
# the unprivileged runtime user can read it and cannot modify its own code.
COPY --from=build --chown=root:root /repo/apps/TakeInitiative.Web/.output /app

ENV HOST=0.0.0.0
ENV PORT=3000
ENV NODE_ENV=production
EXPOSE 3000/tcp

# busybox wget, because node:24-alpine has no curl and adding one is not worth a layer.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=5 \
    CMD wget -q --spider "http://127.0.0.1:${PORT}/healthz" || exit 1

USER nuxtjs
ENTRYPOINT ["node", "/app/server/index.mjs"]
