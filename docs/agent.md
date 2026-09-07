# SAVI Agent Pipeline & Orchestration

## The 12-Step Agent Pipeline
SAVI operates on a deterministic, model-independent pipeline:

1. **Request Normalization**: Trims, normalizes, and sanitizes incoming user input.
2. **Conversation & Context Retrieval**: Fetches conversation history and constructs a `ContextPackage`.
3. **Intent Detection**: Analyzes the query using pattern matchers, regex, keywords, and coreference resolvers.
4. **Task Classification**: Categorizes the intent into a capability (e.g. Weather, Currency, Knowledge, Filesystem, Terminal, Calculator, Time, Memory).
5. **Capability Discovery**: Queries the `IProviderRegistry` or `IToolRegistry` for registered handlers.
6. **Provider Ranking**: Ranks candidate providers based on priority, reliability score, and latency.
7. **Execution Planning**: Builds an `ExecutionPlan` containing primary provider, secondary verification provider, or tool input.
8. **Permission Verification**: If a tool is required, `PermissionGuard` ensures safe/controlled/dangerous policies are enforced.
9. **Provider / Tool Execution**: Dispatches asynchronous requests with cancellation tokens and timeout safeguards.
10. **Multi-Source Verification**: If multiple providers respond, the `VerificationEngine` compares outputs, checks for contradictions, and computes a unified confidence score.
11. **Personality Formatting**: The `PersonalityEngine` shapes the synthesis into a friendly, warm, context-aware companion response.
12. **Asynchronous Memory Extraction**: Analyzes user statements for long-term facts, projects, or preferences and commits them to memory.
