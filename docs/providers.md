# Capability Provider System

## Model-Independence & Zero-Cost Philosophy
SAVI does not depend on any paid LLM, paid search engine, or paid database.

### Built-in Free Providers
1. **Open-Meteo Weather**: Completely free weather forecasts and geocoding without API keys.
2. **wttr.in Weather**: Fallback weather engine used for cross-verifying weather data.
3. **Frankfurter Exchange Rates**: Free currency conversion driven by European Central Bank reference rates.
4. **Wikipedia Open Knowledge**: REST API integration for factual knowledge summaries and article search.
5. **DuckDuckGo Search**: Instant Answers and web search integration without paid search subscriptions.
6. **GitHub Public API**: Real-time inspection of public GitHub repositories, releases, and issue counts.
7. **Local System Diagnostics**: Live machine telemetry, CPU cores, process memory, and OS information.
8. **Local Deterministic Calculator**: Math evaluation and unit conversions (km to miles, Celsius to Fahrenheit).
9. **Generic HTTP API Engine**: Users can register arbitrary JSON REST endpoints with custom parameter mappings and JSONPath extraction.
10. **Optional Ollama Integration**: Pluggable local LLM provider when Ollama is running locally (`http://localhost:11434`).
