#!/bin/bash
set -e
echo "Starting SAVI Web App (HUD)..."
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$HOME}"
dotnet run --project src/SAVI.Web/SAVI.Web.csproj
