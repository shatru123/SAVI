# Deploying SAVI to Render (Free Plan)

This guide walks you through deploying **SAVI (Shatru's Adaptive Virtual Intelligence)** to **Render's Free Cloud Hosting Plan** in under 2 minutes.

---

## Why Render Free Plan?
- **Zero Cost**: Completely free web service hosting ($0/month).
- **Automated Deployments**: Connects to your GitHub repository and automatically deploys whenever you push changes to `main`.
- **Free SSL / TLS**: Automatic HTTPS certificate for `https://<your-service-name>.onrender.com`.
- **Built-in Health Checks**: Render queries `/healthz` to guarantee zero-downtime and monitor service health.

---

## Method 1: Deploy with Render Blueprint (Recommended)

1. **Push your code to GitHub**:
   Ensure your changes are committed and pushed to your GitHub repository:
   ```bash
   git push origin main
   ```

2. **Open Render Dashboard**:
   - Go to [dashboard.render.com](https://dashboard.render.com).
   - Sign in with your GitHub account.

3. **Deploy with Blueprint**:
   - Click **New +** in the top navigation bar.
   - Select **Blueprint**.
   - Choose your repository (`shatru123/SAVI`).
   - Render will automatically detect [`render.yaml`](../render.yaml) and configure:
     - **Service Name**: `savi`
     - **Runtime**: `Docker`
     - **Plan**: `Free`
     - **Health Check**: `/healthz`
   - Click **Apply**. Render will build and launch your SAVI instance!

---

## Method 2: Manual Web Service Setup

If you prefer setting it up manually without blueprints:

1. In Render Dashboard, click **New +** -> **Web Service**.
2. Select **Build and deploy from a Git repository**.
3. Choose your repository: `shatru123/SAVI`.
4. Configure the settings:
   - **Name**: `savi`
   - **Region**: Oregon (US West) or Frankfurt (EU Central)
   - **Branch**: `main`
   - **Runtime**: `Docker`
   - **Dockerfile Path**: `./Dockerfile`
   - **Instance Type**: `Free`
5. Expand **Advanced**:
   - **Health Check Path**: `/healthz`
   - **Environment Variables**:
     - `ASPNETCORE_ENVIRONMENT` = `Production`
     - `DOTNET_RUNNING_IN_CONTAINER` = `true`
6. Click **Create Web Service**.

---

## Verification & Health Check Endpoints

Once deployed, your live URL will be:
`https://<your-subdomain>.onrender.com`

You can verify service health by visiting:
- **Render Health Check**: `https://<your-subdomain>.onrender.com/healthz` (Returns `Healthy` / HTTP 200)
- **Detailed JSON Status**: `https://<your-subdomain>.onrender.com/api/health`
  ```json
  {
    "status": "Healthy",
    "system": "SAVI (Shatru's Adaptive Virtual Intelligence)",
    "version": "1.0.0",
    "creator": "Shatrughna Ambhore",
    "timestamp": "2026-09-07T08:45:00Z",
    "runtime": ".NET 10.0.0"
  }
  ```

---

## Free Tier Notes & Best Practices

1. **Spin-Down Behavior**:
   Render's free tier web services spin down after 15 minutes of inactivity. When a new request arrives, Render automatically spins up the container (takes ~30–50 seconds).
2. **Ephemeral Database Storage**:
   Render free instances have ephemeral container disks. When restarted, SAVI automatically executes `InitializeSaviDatabaseAsync()` to recreate tables and initialize default settings without any manual intervention.
3. **No Paid API Keys Needed**:
   SAVI adheres to the Zero-Cost Principle out-of-the-box. Live weather, currency rates, Wikipedia knowledge, and web searches all work on Render immediately with no third-party configuration!
