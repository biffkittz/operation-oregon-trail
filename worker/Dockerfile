# syntax=docker/dockerfile:1.7
# Temporal game worker image. Built and released independently of the web app.
# Build from the repository root:  docker build -f worker/Dockerfile -t oregon-worker .

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY shared/Oregon.Contracts/Oregon.Contracts.csproj shared/Oregon.Contracts/
COPY worker/Oregon.Worker/Oregon.Worker.csproj worker/Oregon.Worker/
# Publish for the target architecture only: a portable publish would ship the Temporal SDK's native core for
# all nine platforms (~40 MB each).
ARG TARGETARCH
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo linux-arm64 || echo linux-x64) && \
    dotnet restore worker/Oregon.Worker/Oregon.Worker.csproj -r "$RID"
COPY shared/ shared/
COPY worker/Oregon.Worker/ worker/Oregon.Worker/
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo linux-arm64 || echo linux-x64) && \
    dotnet publish worker/Oregon.Worker/Oregon.Worker.csproj -c Release -r "$RID" --self-contained false -o /out --no-restore -p:UseAppHost=false

# The worker needs no ASP.NET: the plain .NET runtime image is enough. The Temporal SDK's native core
# (runtimes/linux-x64/native) only needs glibc and libgcc, both present in the chiseled image.
FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION}-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /out ./
USER $APP_UID
ENTRYPOINT ["dotnet", "Oregon.Worker.dll"]
