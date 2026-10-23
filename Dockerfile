# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0.

# ---------------------------------------------------------------------------
# Stage 1 – build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy repo-level files first so NuGet restore is cached independently.
COPY global.json Directory.Build.props Directory.Packages.props nuget.config ./

# Copy project files for restore layer caching.
COPY src/MudDocs.ServiceDefaults/MudDocs.ServiceDefaults.csproj \
     src/MudDocs.ServiceDefaults/
COPY src/MudDocs/MudDocs.csproj src/MudDocs/

RUN dotnet restore src/MudDocs/MudDocs.csproj

# Copy the actual source after restore to preserve the cache layer above.
COPY src/MudDocs.ServiceDefaults/ src/MudDocs.ServiceDefaults/
COPY src/MudDocs/ src/MudDocs/

RUN dotnet publish src/MudDocs/MudDocs.csproj \
    -c Release -o /app/publish --no-restore

# ---------------------------------------------------------------------------
# Stage 2 – runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl is used by the Docker / compose health-check probe.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# The app stores per-version git clones, serialized index files, and the
# versions.json manifest under /app/data/v{Version}/.
# Mount a named volume at /app/data so all cached data persists across restarts.
RUN mkdir -p /app/data
VOLUME ["/app/data"]

# Use port 8080 inside the container (12-factor / Fly.io / Azure Container Apps
# convention). The host-side mapping is defined in docker-compose.yml.
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV MUDBLAZOR_VERSION=9.0.0

EXPOSE 8080

ENTRYPOINT exec dotnet MudDocs.dll --version ${MUDBLAZOR_VERSION}
