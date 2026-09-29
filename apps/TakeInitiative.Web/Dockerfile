FROM node:24-alpine AS base
RUN apk add --no-cache libc6-compat
RUN corepack enable
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
WORKDIR /repo

# ---
FROM base AS build

# Manifests first, so the install layer is cached until a dependency changes.
# pnpm needs every workspace member's package.json to satisfy --frozen-lockfile.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/TakeInitiative.Api/package.json apps/TakeInitiative.Api/
COPY apps/TakeInitiative.Api.Tests/package.json apps/TakeInitiative.Api.Tests/
COPY apps/TakeInitiative.Web/package.json apps/TakeInitiative.Web/
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

# nuxt.config.ts inlines these into runtimeConfig at build time.
ARG API_URL
ARG WEB_URL
ENV API_URL=$API_URL
ENV WEB_URL=$WEB_URL
ENV TAKE_INIT_ENVIRONMENT=PRODUCTION
RUN pnpm --filter @ti/web run build

# ---
FROM node:24-alpine AS production
WORKDIR /app
RUN addgroup --system --gid 1001 nodejs && adduser --system --uid 1001 nuxtjs
USER nuxtjs

# `.output` is self-contained: server bundle plus public assets.
COPY --from=build --chown=nuxtjs:nodejs /repo/apps/TakeInitiative.Web/.output /app

ENV HOST=0.0.0.0
ENV PORT=3000
ENV NODE_ENV=production
EXPOSE 3000/tcp
ENTRYPOINT ["node", "/app/server/index.mjs"]
