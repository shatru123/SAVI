# Deployment Guide

## Running Locally
### Prerequisites
- .NET 10 SDK (`10.0.400` or higher)

### Run Blazor Web App (HUD)
```bash
dotnet run --project src/SAVI.Web/SAVI.Web.csproj
```
Access the application at `http://localhost:5000` or the reported port.

### Run Headless API Service
```bash
dotnet run --project src/SAVI.Api/SAVI.Api.csproj --urls "http://127.0.0.1:5210"
```

## Running with Docker
```bash
docker build -t savi-web -f docker/Dockerfile .
docker run -p 5000:8080 -v savi_data:/app/data savi-web
```
