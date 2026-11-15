# syntax=docker/dockerfile:1.7
# Web image: the Vue SPA and the ASP.NET Core backend in one container; Kestrel serves the SPA from wwwroot.
# Build from the repository root:  docker build -f app/Dockerfile -t oregon-web .

ARG DOTNET_VERSION=10.0
ARG NODE_VERSION=24

# 1) Build the SPA (type-check + Vite production build).
FROM node:${NODE_VERSION}-bookworm-slim AS frontend
WORKDIR /src/app/frontend
COPY app/frontend/package.json app/frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY app/frontend/ ./
RUN npm run build

# 2) Publish the backend with the SPA baked into wwwroot. Restore runs on project files alone so the layer
#    is cached until dependencies change.
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS backend
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY shared/Oregon.Contracts/Oregon.Contracts.csproj shared/Oregon.Contracts/
COPY app/backend/Oregon.Backend.csproj app/backend/
# Publish for the target architecture only: a portable publish would ship the Temporal SDK's native core for
# all nine platforms (~40 MB each).
ARG TARGETARCH
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo linux-arm64 || echo linux-x64) && \
    dotnet restore app/backend/Oregon.Backend.csproj -r "$RID"
COPY shared/ shared/
COPY app/backend/ app/backend/
COPY --from=frontend /src/app/frontend/dist/ app/backend/wwwroot/
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo linux-arm64 || echo linux-x64) && \
    dotnet publish app/backend/Oregon.Backend.csproj -c Release -r "$RID" --self-contained false -o /out --no-restore -p:UseAppHost=false

# 3) Chiseled runtime: no shell, no package manager, non-root by default (smallest attack surface).
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled AS runtime
WORKDIR /app
COPY --from=backend /out ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Oregon.Backend.dll"]
