# ============================================
# Build stage
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

WORKDIR /src

# Copy project files first for Docker layer caching
COPY ["src/CleanArch.Domain/CleanArch.Domain.csproj", "src/CleanArch.Domain/"]
COPY ["src/CleanArch.Application/CleanArch.Application.csproj", "src/CleanArch.Application/"]
COPY ["src/CleanArch.Infrastructure/CleanArch.Infrastructure.csproj", "src/CleanArch.Infrastructure/"]
COPY ["src/CleanArch.API/CleanArch.API.csproj", "src/CleanArch.API/"]

# Restore dependencies
RUN dotnet restore "src/CleanArch.API/CleanArch.API.csproj"

# Copy source code
COPY . .

# Publish API
WORKDIR /src/src/CleanArch.API

RUN dotnet publish "CleanArch.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false


# ============================================
# Runtime stage
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

WORKDIR /app

# Required for .NET globalization support
RUN apk add --no-cache icu-libs curl

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

# Copy published application
COPY --from=build /app/publish .

# Run as built-in non-root .NET user
USER app

EXPOSE 8080

# Container health check
HEALTHCHECK --interval=30s \
            --timeout=5s \
            --start-period=20s \
            --retries=3 \
            CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "CleanArch.API.dll"]