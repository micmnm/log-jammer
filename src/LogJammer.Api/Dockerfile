FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for restore
COPY Directory.Build.props .
COPY LogJammer.sln .
COPY LogJammer.Core/LogJammer.Core.csproj LogJammer.Core/
COPY LogJammer.Infrastructure/LogJammer.Infrastructure.csproj LogJammer.Infrastructure/
COPY LogJammer.Api/LogJammer.Api.csproj LogJammer.Api/

# Restore
RUN dotnet restore LogJammer.Api/LogJammer.Api.csproj

# Copy all source
COPY . .

# Build and publish
RUN dotnet publish LogJammer.Api/LogJammer.Api.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "LogJammer.Api.dll"]
