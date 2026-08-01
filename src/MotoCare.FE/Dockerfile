FROM node:22-alpine AS build
WORKDIR /src
RUN corepack enable

COPY src/MotoCare.FE/package.json src/MotoCare.FE/pnpm-lock.yaml src/MotoCare.FE/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY src/MotoCare.FE/ ./
ARG NUXT_PUBLIC_API_BASE=https://moto.luandinh.com/api/v1
ENV NUXT_PUBLIC_API_BASE=${NUXT_PUBLIC_API_BASE}
ENV CI=true
RUN pnpm generate

FROM nginx:1.27-alpine AS runtime
COPY --from=build /src/.output/public/ /usr/share/nginx/html/
COPY deploy/production/nginx/proxy_params /etc/nginx/proxy_params
COPY deploy/production/nginx/http.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
