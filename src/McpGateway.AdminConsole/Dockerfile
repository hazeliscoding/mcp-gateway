# Build the Angular app, then serve the static bundle from nginx.
FROM node:22-alpine AS build
WORKDIR /source

# Install with only the manifests first so the dependency layer caches across code changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /source/dist/admin-console/browser /usr/share/nginx/html
EXPOSE 80
