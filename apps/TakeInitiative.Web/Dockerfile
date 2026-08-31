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

COPY apps/TakeInitiative.Web/ apps/TakeInitiative.Web/

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
