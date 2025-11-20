# Multi-stage Dockerfile for SocketGhost Core

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY socketghost-core/*.csproj ./socketghost-core/
RUN dotnet restore socketghost-core/socketghost-core.csproj

# Copy source and build
COPY socketghost-core/ ./socketghost-core/
RUN dotnet publish socketghost-core/socketghost-core.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime
WORKDIR /app

# Copy published app from build stage
COPY --from=build /app/publish .

# Create config directory
RUN mkdir -p /app/storage

# Default configuration
ENV ENGINE=titanium \
    FLOW_RETENTION_DAYS=30 \
    FLOW_STORE_MAX_TOTAL_BYTES=524288000 \
    FLOW_STORE_MAX_INLINE_BYTES=131072 \
    AUTO_PRUNE_ON_START=true

# Expose ports
# 8080: Proxy server
# 9000: WebSocket server
# 9100: Process API
# 9200: Script API
# 9300: Flow API
EXPOSE 8080 9000 9100 9200 9300

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:9100/processes || exit 1

# Entry point
ENTRYPOINT ["dotnet", "socketghost-core.dll"]
