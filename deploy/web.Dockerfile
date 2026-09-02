# Builds the frontend with a same-origin API base (empty string -> requests
# go to "/api/..." on whatever host served the page), then serves it from
# nginx alongside a reverse-proxy rule for that same "/api/" path. Same-origin
# means the browser never needs a cross-origin request to the API at all, and
# nginx is the only container with a published port - see docker-compose.yml.
FROM node:20-alpine AS build
WORKDIR /src
COPY app/package.json app/package-lock.json ./
RUN npm ci
COPY app/ ./
ENV VITE_API_BASE_URL=""
RUN npm run build

FROM nginx:1.27-alpine
COPY --from=build /src/dist /usr/share/nginx/html
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
