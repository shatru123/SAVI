#!/bin/bash
set -e
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$HOME}"

echo "================================================="
echo "  Starting SAVI (Shatru's Adaptive Virtual Intelligence)"
echo "  Web HUD:   http://localhost:5212"
echo "  REST API:  http://localhost:5210"
echo "================================================="

# Trap SIGINT/SIGTERM to kill all background child processes on exit
trap 'kill $(jobs -p) 2>/dev/null || true' EXIT SIGINT SIGTERM

dotnet run --project src/SAVI.Api/SAVI.Api.csproj --urls "http://127.0.0.1:5210" &
API_PID=$!

dotnet run --project src/SAVI.Web/SAVI.Web.csproj --urls "http://127.0.0.1:5212"
