# SAVI — Shatru's Adaptive Virtual Intelligence

<div align="center">

<p align="center">
  <img src="docs/images/savi-banner.svg" alt="SAVI — Shatru's Adaptive Virtual Intelligence Banner" width="100%" />
</p>

[![Live Deployment](https://img.shields.io/badge/Render-Live%20App-00f0ff?style=for-the-badge&logo=render)](https://savi-4grt.onrender.com)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?style=flat&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![SignalR](https://img.shields.io/badge/Real--Time-SignalR-blue?style=flat)](https://dotnet.microsoft.com/apps/aspnet/signalr)
[![Blazor](https://img.shields.io/badge/Web-Blazor%20Server-purple?style=flat&logo=blazor)](https://blazor.net/)
[![EF Core](https://img.shields.io/badge/ORM-EF%20Core%20SQLite-green?style=flat)](https://learn.microsoft.com/en-us/ef/core/)
[![Zero-Cost](https://img.shields.io/badge/Dependencies-Zero%20Paid%20APIs-brightgreen?style=flat)](#zero-cost-principle)
[![Free Neural Model](https://img.shields.io/badge/AI%20Engine-Mistral%20%7C%20Llama%20(Free)-cyan?style=flat)](#5-built-in-capability-providers)

### 🚀 **Live Hosted App**: [https://savi-4grt.onrender.com](https://savi-4grt.onrender.com)

**Architect & Developer**: **Shatrughna Ambhore**  
📧 **Email**: [ambhoreshatrughna@gmail.com](mailto:ambhoreshatrughna@gmail.com) | 📞 **Phone**: +91 9604466334

</div>

---

## 1. Product Vision

**SAVI (Shatru's Adaptive Virtual Intelligence)** is a sovereign personal AI companion and intelligent task-execution platform built on modern **.NET 10**.

SAVI is **NOT** a simple chatbot or generic API wrapper. SAVI behaves like a personal digital friend and sovereign companion that can:
* **Connect to Free AI Models**: Automatically queries serverless OpenAI-compatible inference endpoints (Mistral 7B Instruct, Mistral Nemo 2407, GPT-OSS 20B) for realistic, intelligent, and accurate responses with zero API keys and zero cost.
* **Auto-Speaking Welcome Voice**: Automatically begins speaking aloud ("Hello Shatru! I am SAVI...") when you open the URL, featuring browser autoplay security unlockers.
* **Maintain Deep Context & Long-Term Memory**: Remembers user preferences, past conversations, and facts in persistent SQLite memory.
* **Search & Verify with Multi-Source Consensus**: Discovers public knowledge bases, live weather, currency rates, GitHub repositories, and system diagnostics, verifying consensus before responding.
* **Execute Protected Local Tasks**: Guided by a strict 3-tier permission guard (Safe / Controlled / Dangerous) with interactive user confirmation modals for destructive actions.
* **Rich Cybernetic HUD Interface**: Futuristic JARVIS-inspired HUD with real-time audio wave telemetry, animated hexagonal core, responsive mobile drawer, search, and conversation management.

> *"SAVI feels like a personal digital friend who can actually get things done."*

---

## 2. Core Principles

### ✦ Zero-Cost Principle
SAVI is 100% free to develop and run with zero paid subscriptions:
- **Zero Mandatory Paid LLMs** (built-in free neural model endpoints with zero API keys required).
- **Zero Mandatory Paid Search APIs** (free DuckDuckGo instant answers & Wikipedia OpenSearch).
- **Zero Mandatory Paid Databases** (lightweight, zero-config EF Core SQLite).
- **Zero Mandatory Paid Speech Services** (native W3C Web Speech API for both STT and TTS).

### ✦ Model Independence & Fallback Cascade
SAVI dynamically ranks available capability providers. If an AI endpoint is temporarily busy, it seamlessly cascades through alternate models (Mistral 7B → Mistral Nemo → GPT-OSS → Wikipedia / DuckDuckGo), guaranteeing a responsive answer at all times.

---

## 3. System Architecture

SAVI adheres strictly to Clean Architecture and Domain-Driven Design:

```mermaid
graph TD
    User([User: Browser / Mobile / Desktop]) --> UI[SAVI.Web: Blazor HUD + Web Speech API]
    UI --> API[SAVI.Api / SaviHub SignalR]
    API --> Agent[SAVI.Agent Orchestration Pipeline]
    
    subgraph SAVI.Agent
        Agent --> Intent[Intent Detection & Entity Extraction]
        Intent --> Context[Context Builder: History + Summary + Memories]
        Context --> Plan[Execution Planner & Provider Ranking]
        Plan --> Verifier[Multi-Source Verification Engine]
        Verifier --> Personality[Personality Engine]
    end
    
    subgraph Capability Providers
        Plan --> Providers[Provider Registry]
        Providers --> FreeAI[SAVI Neural Engine: Mistral 7B / Nemo / GPT-OSS]
        Providers --> OpenMeteo[Open-Meteo Weather]
        Providers --> Frankfurter[Frankfurter Currency Rates]
        Providers --> Wikipedia[Wikipedia Knowledge]
        Providers --> DDG[DuckDuckGo Search]
        Providers --> GitHub[GitHub Public API]
        Providers --> SysInfo[Local System Diagnostics]
        Providers --> Calc[Deterministic Calculator]
    end
    
    subgraph Safety & Tools
        Plan --> Tools[Tool Framework]
        Tools --> PermGuard[Permission Guard: Safe / Controlled / Dangerous]
        PermGuard --> FileTool[File System Tool]
        PermGuard --> TermTool[Terminal Tool]
    end
    
    subgraph Persistence
        Context --> DB[(SQLite Database)]
        Agent --> DB
    end
```

---

## 4. Repository Structure

```text
SAVI/
├── src/
│   ├── SAVI.Core/            # Entities, Enums, Models, ValueObjects, Domain Interfaces
│   ├── SAVI.Application/     # Application Services, Repositories Interfaces, DTOs
│   ├── SAVI.Infrastructure/  # EF Core SQLite, Free AI Provider, Security, Audit
│   ├── SAVI.Agent/           # Orchestrator, Intent Routing, Planning, Verification, Personality
│   ├── SAVI.Tools/           # Permission Guard, FileSystem, Terminal Tools
│   ├── SAVI.Api/             # ASP.NET Core Minimal APIs & SignalR SaviHub
│   ├── SAVI.Web/             # Futuristic JARVIS-Inspired Blazor Server Web App
│   ├── SAVI.Desktop/         # Desktop Companion Client Host
│   └── SAVI.Mobile/          # Mobile Companion Client Architecture
│
├── tests/
│   ├── SAVI.Core.Tests/            # Entity & Value Object Unit Tests
│   ├── SAVI.Application.Tests/     # Service & Memory Tests
│   ├── SAVI.Agent.Tests/           # Intent, Verification & Personality Tests
│   ├── SAVI.Infrastructure.Tests/  # EF Core SQLite & Security Tests
│   └── SAVI.Api.Tests/             # API Contract & DTO Tests
│
├── docs/                     # Architectural guides, images & deployment docs
│   └── images/               # SAVI SVG Banner & Logo assets
├── scripts/                  # build.sh, test.sh, run.sh
├── Dockerfile                # Root Multi-stage Docker container for Cloud Hosting
├── render.yaml               # Render Infrastructure Blueprint
└── SAVI.sln                  # Master Solution File
```

---

## 5. Built-in Capability Providers

| Provider | Capability | Auth Required | Cost | Description |
| :--- | :--- | :---: | :---: | :--- |
| **SAVI Neural Engine** | `reasoning`, `knowledge`, `search` | **None** | **Free** | Serverless OpenAI-compatible inference with Mistral-7B, Mistral-Nemo, and GPT-OSS models |
| **Open-Meteo** | `weather` | None | Free | Geocoding + current/forecast weather |
| **wttr.in** | `weather` | None | Free | Secondary weather source for cross-verification |
| **Frankfurter** | `currency` | None | Free | Live European Central Bank exchange rates |
| **Wikipedia** | `knowledge` | None | Free | Encyclopedia article summaries & searches |
| **DuckDuckGo** | `search` | None | Free | Instant answers and web search extraction |
| **GitHub Public** | `github` | None | Free | Repository stats, releases, and issue counts |
| **Local System** | `system`, `time` | None | Free | Host OS diagnostics, CPU, RAM, clock |
| **Calculator** | `calculator` | None | Free | Deterministic math & unit conversions |
| **Optional LLM Keys** | `reasoning` | Optional | Free | Optional Groq (`GROQ_API_KEY`) or OpenRouter (`OPENROUTER_API_KEY`) |

---

## 6. Safety & Tool Permissions

Every tool execution is guarded by the `IPermissionGuard`:
* **SAFE**: Reading files, inspecting system telemetry, math evaluation, searching public knowledge.
* **CONTROLLED**: Creating/writing files, automated web navigation.
* **DANGEROUS**: Deleting files, recursive directory removal, executing terminal shell commands.
  * *Dangerous actions pause execution and render an interactive approval modal requesting explicit user consent.*

---

## 7. Quick Start & Deployment

### Run Locally
```bash
# Build the entire solution
dotnet build /m:1 SAVI.sln

# Launch the Web HUD
dotnet run --project src/SAVI.Web/SAVI.Web.csproj --urls "http://localhost:5212"
```
Open **`http://localhost:5212`** in your browser to experience the holographic HUD!

### Run with Docker
```bash
docker build -t savi:latest .
docker run -p 5212:5212 savi:latest
```

### Deploy to Render
SAVI is deployed live on Render's Cloud platform:
* **Live Web App**: [https://savi-4grt.onrender.com](https://savi-4grt.onrender.com)
* **Healthcheck Endpoints**: `/healthz`, `/health`, `/api/health`
* Automatic dynamic `$PORT` environment variable binding.

---

## 8. Creator Information

SAVI was designed, architected, and built by:

* **Name**: **Shatrughna Ambhore**
* **Email**: [ambhoreshatrughna@gmail.com](mailto:ambhoreshatrughna@gmail.com)
* **Phone**: +91 9604466334
* **GitHub**: [@shatru123](https://github.com/shatru123)

---

## 9. License

This project is licensed under the [MIT License](LICENSE).
