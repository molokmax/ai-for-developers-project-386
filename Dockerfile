# Мультистейдж: сборка SPA -> публикация API -> рантайм с той же статикой.
# Контекст сборки - корень репо (API тянет contracts/generated/openapi.json).

# Стадия 1: сборка фронтенда (Vite + TypeScript)
FROM node:22-alpine AS client
WORKDIR /src/client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

# Стадия 2: сборка и публикация API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
# Directory.Build.props/.editorconfig важны: EnforceCodeStyleInBuild ловится здесь же,
# как и в CI (warnings = errors)
COPY Directory.Build.props .editorconfig ./
COPY src/CallCalendar.Api/CallCalendar.Api.csproj src/CallCalendar.Api/
COPY contracts/generated/openapi.json contracts/generated/
RUN dotnet restore src/CallCalendar.Api
COPY src/CallCalendar.Api/ src/CallCalendar.Api/
RUN dotnet publish src/CallCalendar.Api --configuration Release --no-restore --output /app/publish

# Стадия 3: рантайм
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# curl для HEALTHCHECK (в базовом образе его нет); ставится до смены пользователя
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=api /app/publish .
COPY --from=client /src/client/dist ./wwwroot

# Каталог данных SQLite с владельцем app-пользователя: named volume при первом
# маунте наследует владельца содержимого каталога из образа
RUN mkdir /data && chown $APP_UID:$APP_UID /data

# PORT читается в Program.cs (UseUrls); APP_UID задаёт non-root пользователя (1654)
ENV PORT=8080 \
    RUN_MIGRATIONS=1 \
    ConnectionStrings__Default=Data\ Source=/data/callcalendar.db

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS "http://localhost:${PORT}/api/health" || exit 1

USER $APP_UID
ENTRYPOINT ["dotnet", "CallCalendar.Api.dll"]
