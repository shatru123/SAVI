# Development Guide

## Build
```bash
dotnet build SAVI.sln
```

## Test
```bash
dotnet test SAVI.sln
```

## Adding a New Capability Provider
1. Create a class implementing `ICapabilityProvider` in `SAVI.Infrastructure/Providers/`.
2. Define `Id`, `Name`, `Capabilities`, `Priority`, and implement `ExecuteAsync` and `HealthCheckAsync`.
3. Register the provider in `SAVI.Infrastructure/DependencyInjection.cs`.
4. The `AgentOrchestrator` will automatically discover, rank, and cross-verify with your new provider.
