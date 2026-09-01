FROM node:24-alpine AS build
WORKDIR /src
RUN corepack enable && corepack prepare pnpm@11.13.1 --activate

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/admin/package.json apps/admin/
RUN pnpm install --frozen-lockfile --filter @veriscan/admin...

COPY apps/admin/ apps/admin/
ARG VITE_API_BASE_URL=/api/admin/v1
ARG VITE_OIDC_AUTHORITY
ARG VITE_OIDC_CLIENT_ID=veriscan-admin
ARG VITE_OIDC_REDIRECT_URI
ENV VITE_API_MODE=real \
    VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_OIDC_AUTHORITY=$VITE_OIDC_AUTHORITY \
    VITE_OIDC_CLIENT_ID=$VITE_OIDC_CLIENT_ID \
    VITE_OIDC_REDIRECT_URI=$VITE_OIDC_REDIRECT_URI
RUN pnpm --dir apps/admin build

FROM nginxinc/nginx-unprivileged:1.29-alpine AS runtime
COPY apps/admin/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/apps/admin/dist/ /usr/share/nginx/html/
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/ || exit 1
