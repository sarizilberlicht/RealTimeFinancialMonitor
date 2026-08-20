# Stage 1 - Build React application
FROM node:22-alpine AS build

WORKDIR /app

COPY package*.json ./

RUN npm ci

COPY . .

ARG VITE_API_BASE_URL=http://localhost:5032

ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build


# Stage 2 - Serve static files
FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html

COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80