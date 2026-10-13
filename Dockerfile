# syntax=docker/dockerfile:1

# ── Build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached across source-only changes.
# The solution and every csproj must be copied before restore.
# -r linux-x64 here as well as on publish: the restore graph is RID-specific,
# and without it the assets file has no net10.0/linux-x64 target, so publish
# --no-restore below fails with NETSDK1047.
COPY LocalMediaTranslator.slnx .
COPY src/LocalMediaTranslator.Core/LocalMediaTranslator.Core.csproj src/LocalMediaTranslator.Core/
COPY src/LocalMediaTranslator.Infrastructure/LocalMediaTranslator.Infrastructure.csproj src/LocalMediaTranslator.Infrastructure/
COPY src/LocalMediaTranslator.Cli/LocalMediaTranslator.Cli.csproj src/LocalMediaTranslator.Cli/
RUN dotnet restore -r linux-x64 src/LocalMediaTranslator.Cli/LocalMediaTranslator.Cli.csproj

# -r linux-x64 is required: Whisper.net.Runtime ships native assets per RID and
# the linux-x64 libwhisper is silently absent without it.
# Framework-dependent (not self-contained) because this stage's output runs in
# the runtime image below, which already provides the .NET runtime.
COPY . .
RUN dotnet publish src/LocalMediaTranslator.Cli/LocalMediaTranslator.Cli.csproj \
    -c Release -r linux-x64 --no-restore \
    -o /app

# ── Runtime stage ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# Non-root by default. The base image already occupies uid 1000, so let
# useradd pick; the image uid is arbitrary because file ownership on bind
# mounts comes from `docker run --user $(id -u):$(id -g)` at run time. This
# user exists only so a run without --user is still non-root instead of root.
RUN useradd --create-home appuser
WORKDIR /app
COPY --from=build --chown=appuser:appuser /app .

# /config holds the config file when LMT_CONFIG_FILE points at it. An empty
# named volume mounted here inherits this directory's permissions; 777 because
# the container runs as an arbitrary --user uid that cannot chown itself.
RUN mkdir -p /config && chmod 777 /config

# Seed the config volume: an empty named volume mounted at /config inherits
# this file, so PathMappings is pre-configured on the first run. Container
# defaults only - endpoints and keys come from .env, which override this.
# 666 because `set` must be able to rewrite the file as an arbitrary uid.
COPY appsettings.container.json /config/appsettings.local.json
RUN chmod 666 /config/appsettings.local.json

USER appuser

# /media holds the mounted media library; /config holds a mounted
# appsettings.local.json (point LMT_CONFIG_FILE at it).
VOLUME ["/media", "/config"]

# Long transcriptions run for hours; the container should not be mistaken for
# hung just because a single run takes a while.
ENV DOTNET_TieredPGO=1

ENTRYPOINT ["dotnet", "LocalMediaTranslator.Cli.dll"]
