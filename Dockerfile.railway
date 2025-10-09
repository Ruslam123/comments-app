FROM mcr.microsoft.com/dotnet/sdk:8.0

# Встановлюємо Node.js
RUN apt-get update && \
    curl -fsSL https://deb.nodesource.com/setup_18.x | bash - && \
    apt-get install -y nodejs && \
    node --version && npm --version

WORKDIR /build

# === BACKEND ===
COPY backend/ ./backend/
WORKDIR /build/backend
RUN dotnet restore CommentsApp.sln
RUN dotnet publish CommentsApp.API/CommentsApp.API/CommentsApp.API.csproj -c Release -o /app

# === FRONTEND ===
WORKDIR /build/frontend
COPY frontend/ ./

# Перевірка package.json
RUN echo "=== package.json ===" && cat package.json

# Встановлення залежностей
RUN npm install

# Перевірка scripts
RUN echo "=== Check if build script exists ===" && \
    npm run --list

# Білд React
RUN echo "=== Starting React build ===" && \
    npm run build || (echo "❌ Build failed!" && exit 1)

# Перевірка результату білду
RUN echo "=== Build folder contents ===" && \
    ls -lah ./build/ && \
    echo "=== index.html check ===" && \
    cat ./build/index.html | head -n 5

# Копіювання в wwwroot
RUN echo "=== Creating wwwroot ===" && \
    mkdir -p /app/wwwroot && \
    echo "=== Copying files ===" && \
    cp -v ./build/* /app/wwwroot/ 2>&1 || echo "Copy individual files failed, trying recursive..." && \
    cp -rv ./build/* /app/wwwroot/

# Фінальна перевірка
RUN echo "=== Final wwwroot check ===" && \
    ls -lah /app/wwwroot/ && \
    if [ -f "/app/wwwroot/index.html" ]; then \
        echo "✅ index.html found!"; \
    else \
        echo "❌ index.html missing!" && \
        echo "=== Searching for index.html ===" && \
        find /build -name "index.html" && \
        exit 1; \
    fi

# Uploads folder
RUN mkdir -p /app/wwwroot/uploads && chmod 777 /app/wwwroot/uploads

WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "CommentsApp.API.dll"]