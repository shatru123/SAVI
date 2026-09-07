# SAVI System Architecture

## Overview
**SAVI (Shatru's Adaptive Virtual Intelligence)** is built with a clean, decoupled, multi-layered architecture following Domain-Driven Design (DDD) and Clean Architecture principles.

```text
                                  ┌──────────────────────────────┐
                                  │         SAVI Clients         │
                                  │  • Blazor Web App (HUD)      │
                                  │  • Desktop Companion Host    │
                                  │  • Mobile Client Architecture│
                                  └──────────────┬───────────────┘
                                                 │
                                           SignalR / HTTP
                                                 │
                                                 ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ SAVI.Api (ASP.NET Core .NET 10 Minimal APIs & SignalR Hubs)                             │
│   ├── /hub/savi (Real-time events, streaming responses, audio states, task telemetry)    │
│   └── /api/v1 (Chat, Conversations, Memory, Tasks, Providers, Tools, Settings, Audit)   │
└────────────────────────────────────────────┬────────────────────────────────────────────┘
                                             │
                                             ▼
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ SAVI.Agent (Intelligent Orchestration Engine)                                           │
│   ├── Request Normalizer & Intent Routing (Pattern, Semantic, Deterministic)            │
│   ├── Context Retrieval & Builder (Recency + Summaries + History + Long-Term Memory)    │
│   ├── Task Classifier & Capability Discovery                                            │
│   ├── Provider Ranking & Execution Planner                                              │
│   ├── Verification Engine (Cross-source comparison, confidence scoring, freshness)      │
│   └── Personality Engine (Friendly, Professional, Concise, Calm, Witty, Motivational)   │
└───────────────────────┬─────────────────────────────────────────────────┬───────────────┘
                        │                                                 │
                        ▼                                                 ▼
┌────────────────────────────────────────┐       ┌────────────────────────────────────────┐
│ SAVI.Tools (Execution & Permissions)   │       │ SAVI.Infrastructure (Providers & Data) │
│   ├── Permission Guard                 │       │   ├── Provider Registry & Health       │
│   │   (Safe, Controlled, Dangerous)    │       │   ├── Open-Meteo & wttr.in (Weather)   │
│   ├── File System Tool                 │       │   ├── Frankfurter (Currency Rates)     │
│   ├── Playwright Browser Tool          │       │   ├── Wikipedia & Wikidata (Knowledge) │
│   ├── System & Terminal Tool           │       │   ├── DuckDuckGo (Search)              │
│   ├── Document Reader Tool             │       │   ├── GitHub Public REST API           │
│   ├── Notification Tool                │       │   ├── Generic HTTP API Engine          │
│   ├── Task State Machine               │       │   ├── Optional Ollama / LLM Provider   │
│   └── Audit Log Service                │       │   ├── EF Core SQLite DB + Migrations   │
│                                        │       │   └── Speech STT / TTS Abstractions    │
└────────────────────────────────────────┘       └────────────────────────────────────────┘
```

## Layer Responsibilities

### 1. `SAVI.Core`
- Core domain entities: `Conversation`, `Message`, `MemoryItem`, `TaskItem`, `TaskStep`, `ApiProviderDefinition`, `AuditLogEntry`, `UserSettings`.
- Enums: `PermissionLevel`, `PersonalityMode`, `VoiceState`, `TaskState`, `MemoryType`, `MessageRole`, `MessageType`.
- Value Objects: `SourceReference`, `ProviderResult`, `ToolExecutionResult`, `ToolMetadata`, `ProviderMetadata`.
- Domain Interfaces: `ICapabilityProvider`, `ISearchProvider`, `ITool`, `IAgentOrchestrator`, `IContextBuilder`, `IVerificationEngine`, `IPersonalityEngine`, `IPermissionGuard`, `ITaskStateMachine`.
- Has zero dependencies on any external framework or infrastructure.

### 2. `SAVI.Application`
- Application DTOs: `ChatDto`, `ConversationDto`, `MemoryDto`, `TaskDto`, `ProviderDto`, `SettingsDto`, `SystemDiagnosticsDto`.
- Application Services: `ConversationService`, `MemoryService`, `TaskService`, `SettingsService`.
- Abstract Repositories: `IConversationRepository`, `IMemoryRepository`, `ITaskRepository`, `IProviderDefinitionRepository`, `IAuditRepository`, `ISettingsRepository`.

### 3. `SAVI.Infrastructure`
- Persistence: `SaviDbContext` with EF Core SQLite and automatic SQLite integer-ticks conversion for `DateTimeOffset`.
- Provider implementations: Open-Meteo, wttr.in, Frankfurter, Wikipedia, DuckDuckGo, GitHub Public, SystemInfo, Calculator, GenericHttpApi, OptionalOllama.
- Security: `SsrfValidator` for user-defined APIs, `SecretMasker` for redacting credentials and tokens.
- Auditing: `AuditService` writing transparent logs to SQLite.

### 4. `SAVI.Agent`
- Orchestrator: `AgentOrchestrator` implementing the 12-step execution pipeline.
- Routing: `IntentDetector` for deterministic, regex, and coreference intent recognition.
- Context: `ContextBuilder` assembling recent messages, history, and active long-term memories.
- Verification: `VerificationEngine` performing multi-source cross-checking and discrepancy detection.
- Personality: `PersonalityEngine` adapting tone to user preferences and selected personality mode.

### 5. `SAVI.Tools`
- Security Guard: `PermissionGuard` classifying tool invocations into Safe, Controlled, and Dangerous.
- Implementations: `FileSystemTool`, `TerminalTool`, `BrowserAgentTool`, `DocumentReaderTool`, `NotificationTool`.

### 6. `SAVI.Api` & `SAVI.Web`
- `SAVI.Api`: ASP.NET Core Minimal APIs + SignalR `SaviHub`.
- `SAVI.Web`: Blazor Web App with holographic JARVIS-inspired HUD, animated SVG `SaviCore`, and Web Speech API.

### 7. `SAVI.Desktop` & `SAVI.Mobile`
- `SAVI.Desktop`: Cross-platform desktop companion host with SignalR client and terminal diagnostics.
- `SAVI.Mobile`: Mobile companion client architecture with real-time sync.
