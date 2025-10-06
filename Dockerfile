FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копіюємо тільки backend папку
COPY backend/ .

# Тепер CommentsApp.sln буде в поточній директорії
RUN dotnet restore CommentsApp.sln
RUN dotnet build CommentsApp.sln -c Release
RUN dotnet publish CommentsApp.API/CommentsApp.API/CommentsApp.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Створюємо папку для завантажених файлів
RUN mkdir -p /app/wwwroot/uploads && chmod 777 /app/wwwroot/uploads

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CommentsApp.API.dll"]