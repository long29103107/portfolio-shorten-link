FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
ARG VITE_SHORTENLINK_API_BASE_URL
ENV VITE_SHORTENLINK_API_BASE_URL=$VITE_SHORTENLINK_API_BASE_URL
RUN npm run build

FROM nginx:1.27-alpine AS final
ENV NGINX_ENVSUBST_OUTPUT_DIR=/usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
COPY runtime-config.js.template /etc/nginx/templates/runtime-config.js.template

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
