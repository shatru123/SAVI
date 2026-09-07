#!/bin/bash
set -e
echo "Building SAVI .NET 10 Solution..."
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$HOME}"
dotnet build /m:1 SAVI.sln
echo "Build complete."
