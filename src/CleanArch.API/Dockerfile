# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy csproj files first for better layer caching
COPY ["src/CleanArch.Domain/CleanArch.Domain.csproj", "src/CleanArch.Domain/"]
COPY ["src/CleanArch.Application/CleanArch.Application.csproj", "src/CleanArch.Application/"]
COPY ["src/CleanArch.Infrastructure/CleanArch.Infrastructure.csproj", "src/CleanArch.Infrastructure/"]
COPY ["src/CleanArch.API/CleanArch.API.csproj", "src/CleanArch.API/"]
RUN dotnet restore "src/CleanArch.API/CleanArch.API.csproj"

# Copy the rest and publish
COPY . .
WORKDIR /src/src/CleanArch.API
RUN dotnet publish "CleanArch.API.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Security hardening: run as a non-root, unprivileged user (matches K8s runAsNonRoot: true)
RUN addgroup -g 1654 dotnetgroup && adduser -u 1654 -G dotnetgroup -s /bin/sh -D dotnetuser
RUN apk add --no-cache icu-libs curl
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .
RUN chown -R dotnetuser:dotnetgroup /app
USER dotnetuser

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "CleanArch.API.dll"]
