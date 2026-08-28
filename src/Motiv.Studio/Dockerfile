# syntax=docker/dockerfile:1
# Build context: repo root (see docker-compose.yml)

# ---- UI build stage -----------------------------------------------------
FROM node:20-slim AS ui-build
WORKDIR /src
RUN corepack enable && corepack prepare pnpm@9.12.0 --activate
COPY ui/ ui/
COPY src/Motiv.Studio/ src/Motiv.Studio/
RUN pnpm -C ui install --frozen-lockfile
RUN pnpm -C ui/apps/studio build
# Vite build outputs into src/Motiv.Studio/wwwroot (outside ui/)

# ---- Publish stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /src
COPY . .
COPY --from=ui-build /src/src/Motiv.Studio/wwwroot ./src/Motiv.Studio/wwwroot
RUN dotnet publish src/Motiv.Studio -c Release -o /app

# ---- Runtime stage ----------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app .
ENV ASPNETCORE_URLS=http://+:5100
EXPOSE 5100
ENTRYPOINT ["dotnet", "Motiv.Studio.dll"]
