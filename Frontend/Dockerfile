FROM node:22-alpine AS build
WORKDIR /app
COPY Frontend/package.json Frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY Frontend/ ./
RUN npm run build

FROM nginx:alpine AS runtime
COPY Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
RUN chmod -R a+r /usr/share/nginx/html
EXPOSE 80
