# Stage 1: Build frontend
FROM node:22-alpine AS frontend-build
WORKDIR /app
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ .
RUN npm run build

# Stage 2: Build .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet-build
WORKDIR /src

# Copy solution and project files for restore
COPY Directory.Build.props .
COPY LogJammer.slnx .
COPY LogJammer.Core/LogJammer.Core.csproj LogJammer.Core/
COPY LogJammer.Infrastructure/LogJammer.Infrastructure.csproj LogJammer.Infrastructure/
COPY LogJammer.Api/LogJammer.Api.csproj LogJammer.Api/

# Restore
RUN dotnet restore LogJammer.Api/LogJammer.Api.csproj

# Copy all source
COPY . .

# Build and publish
RUN dotnet publish LogJammer.Api/LogJammer.Api.csproj -c Release -o /app/publish --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=dotnet-build /app/publish .
COPY --from=frontend-build /app/dist ./wwwroot

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "LogJammer.Api.dll"]
