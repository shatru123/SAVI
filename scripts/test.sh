#!/bin/bash
set -e
echo "Running SAVI Test Suite..."
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$HOME}"
dotnet test tests/SAVI.Core.Tests/SAVI.Core.Tests.csproj
dotnet test tests/SAVI.Application.Tests/SAVI.Application.Tests.csproj
dotnet test tests/SAVI.Agent.Tests/SAVI.Agent.Tests.csproj
dotnet test tests/SAVI.Infrastructure.Tests/SAVI.Infrastructure.Tests.csproj
dotnet test tests/SAVI.Api.Tests/SAVI.Api.Tests.csproj
echo "All tests passed successfully."
