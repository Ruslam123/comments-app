# === STAGE 1: Build Backend ===
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

# Копіювання solution та проектів
COPY backend/CommentsApp.sln ./
COPY backend/CommentsApp.Core/CommentsApp.Core/*.csproj ./CommentsApp.Core/CommentsApp.Core/
COPY backend/CommentsApp.Infrastructure/CommentsApp.Infrastructure/*.csproj ./CommentsApp.Infrastructure/CommentsApp.Infrastructure/
COPY backend/CommentsApp.API/CommentsApp.API/*.csproj ./CommentsApp.API/CommentsApp.API/

# Restore
RUN dotnet restore CommentsApp.sln

# Копіювання всього коду
COPY backend/ ./

# Build та Publish
RUN dotnet publish CommentsApp.API/CommentsApp.API/CommentsApp.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# === STAGE 2: Build Frontend ===
FROM node:18-alpine AS frontend-build
WORKDIR /app

# Копіювання package.json
COPY frontend/package*.json ./
RUN npm install

# Копіювання коду frontend
COPY frontend/ ./

# Build React з правильним API_URL
ARG REACT_APP_API_URL
ENV REACT_APP_API_URL=${REACT_APP_API_URL}

RUN npm run build

# === STAGE 3: Runtime ===
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Копіювання backend
COPY --from=backend-build /app/publish .

# Копіювання frontend у wwwroot
COPY --from=frontend-build /app/build ./wwwroot

# Створення папки для uploads
RUN mkdir -p ./wwwroot/uploads && chmod 777 ./wwwroot/uploads

# Environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "CommentsApp.API.dll"]
