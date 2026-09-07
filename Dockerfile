# =========================================================
# SAVI — Shatru's Adaptive Virtual Intelligence
# Production Container Image for Render / Cloud Hosting
# =========================================================

# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and configuration files
COPY Directory.Build.props nuget.config SAVI.sln ./

# Copy all projects
COPY src/SAVI.Core/ src/SAVI.Core/
COPY src/SAVI.Application/ src/SAVI.Application/
COPY src/SAVI.Infrastructure/ src/SAVI.Infrastructure/
COPY src/SAVI.Agent/ src/SAVI.Agent/
COPY src/SAVI.Tools/ src/SAVI.Tools/
COPY src/SAVI.Api/ src/SAVI.Api/
COPY src/SAVI.Web/ src/SAVI.Web/
COPY src/SAVI.Desktop/ src/SAVI.Desktop/
COPY src/SAVI.Mobile/ src/SAVI.Mobile/

# Restore dependencies
RUN dotnet restore /m:1 src/SAVI.Web/SAVI.Web.csproj

# Build and publish release output
RUN dotnet publish -c Release -o /app/publish src/SAVI.Web/SAVI.Web.csproj

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Default port configuration (overridden dynamically if Render sets $PORT)
ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"
ENV DOTNET_RUNNING_IN_CONTAINER="true"

EXPOSE 8080
EXPOSE 10000

# Start SAVI Virtual Intelligence Web Engine
ENTRYPOINT ["dotnet", "SAVI.Web.dll"]
