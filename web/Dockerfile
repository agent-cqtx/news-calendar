FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci

# Path-based deploy (apps.quinterox.com/news-calendar): override in compose build args.
# Defaults: standalone at root (/).
ARG BASE_HREF=/
ARG VITE_API_BASE=/api/events
ENV BASE_HREF=$BASE_HREF
ENV VITE_API_BASE=$VITE_API_BASE

COPY . .
RUN npm run build

FROM nginx:1.27-alpine
# The nginx image renders /etc/nginx/templates/*.template with env vars at start -> conf.d/default.conf
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
ENV HA_ORIGIN=
EXPOSE 80
