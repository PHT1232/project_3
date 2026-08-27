# Frontend-only image (independent-container deployment). Self-contained build context:
#   docker build -t stationeryms-frontend ./frontend

FROM node:20-alpine AS build
WORKDIR /src

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final

# BACKEND_HOST/BACKEND_PORT are substituted into nginx.conf.template at container start
# (nginx's official image runs envsubst over /etc/nginx/templates/*.template automatically).
# Defaults match the docker-compose/Jenkins service name; override with `docker run -e`.
ENV BACKEND_HOST=backend
ENV BACKEND_PORT=8080

COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf.template /etc/nginx/templates/default.conf.template

EXPOSE 80
