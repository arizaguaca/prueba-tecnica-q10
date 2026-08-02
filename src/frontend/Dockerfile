# Etapa 1: Compilación de la aplicación React (Vite)
FROM node:20-alpine AS build
WORKDIR /app

# Copiar archivos de dependencias e instalarlas
COPY package*.json ./
RUN npm ci

# Copiar el código fuente y construir los archivos estáticos de producción
COPY . .
ARG VITE_API_URL=http://localhost:5051
ENV VITE_API_URL=${VITE_API_URL}
RUN npm run build

# Etapa 2: Servidor Web Nginx para producción
FROM nginx:alpine AS final

# Copiar los archivos construidos desde la etapa anterior
COPY --from=build /app/dist /usr/share/nginx/html

# Copiar la configuración personalizada de Nginx
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
