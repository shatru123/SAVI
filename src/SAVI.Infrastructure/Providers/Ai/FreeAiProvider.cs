using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Ai;

public class FreeAiProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;
    private static readonly string[] FreeOvhModels = new[]
    {
        "Mistral-7B-Instruct-v0.3",
        "Mistral-Nemo-Instruct-2407"
    };

    private static readonly ConcurrentDictionary<string, (string Content, IReadOnlyList<SourceReference> Sources, DateTime ExpireAt)> Cache = new();

    private const string SystemPrompt =
        "You are SAVI (Shatru's Adaptive Virtual Intelligence), an advanced, helpful, and realistic AI assistant created and architected by Shatrughna Ambhore (Email: ambhoreshatrughna@gmail.com, Phone: +91 9604466334). " +
        "Provide direct, accurate, realistic, and thoughtful answers. If asked who created, built, or developed you, proudly state that you were created by Shatrughna Ambhore.";

    public FreeAiProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.FreeAi;
    public string Name => "SAVI Neural Engine (Free Model)";
    public IReadOnlyCollection<string> Capabilities => new[]
    {
        SaviConstants.Capabilities.Reasoning,
        SaviConstants.Capabilities.Knowledge,
        SaviConstants.Capabilities.Search
    };

    // Priority 10 ensures it is selected as the primary provider before DuckDuckGo (15) and Wikipedia (20)
    public int Priority => 10;

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Reasoning, StringComparison.OrdinalIgnoreCase) ||
               request.Capability.Equals(SaviConstants.Capabilities.Knowledge, StringComparison.OrdinalIgnoreCase) ||
               request.Capability.Equals(SaviConstants.Capabilities.Search, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = request.Parameters.GetValueOrDefault("query") ??
                     request.Parameters.GetValueOrDefault("topic") ??
                     request.Parameters.GetValueOrDefault("prompt") ??
                     request.Prompt;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return ProviderResult.Failed(Id, Name, "Prompt is empty.");
        }

        // Cache Check (sub-10ms response for repeat prompts)
        var cacheKey = prompt.Trim().ToLowerInvariant();
        if (Cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpireAt)
        {
            return ProviderResult.Succeeded(Id, Name, cached.Content, confidence: 0.98, sources: cached.Sources);
        }

        // 1. Check optional authenticated provider keys if user provided any in env vars
        var groqKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (!string.IsNullOrWhiteSpace(groqKey))
        {
            var groqResult = await TryChatCompletionAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                "llama-3.3-70b-versatile",
                groqKey,
                prompt,
                request,
                "Groq LLaMA-3.3-70B",
                cancellationToken);

            if (groqResult != null) return groqResult;
        }

        var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (!string.IsNullOrWhiteSpace(openRouterKey))
        {
            var openRouterResult = await TryChatCompletionAsync(
                "https://openrouter.ai/api/v1/chat/completions",
                "google/gemini-2.0-flash-exp:free",
                openRouterKey,
                prompt,
                request,
                "OpenRouter Gemini-2.0",
                cancellationToken);

            if (openRouterResult != null) return openRouterResult;
        }

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            var openAiResult = await TryChatCompletionAsync(
                "https://api.openai.com/v1/chat/completions",
                "gpt-4o-mini",
                openAiKey,
                prompt,
                request,
                "OpenAI GPT-4o-mini",
                cancellationToken);

            if (openAiResult != null) return openAiResult;
        }

        // 2. Query OVHcloud AI Endpoints (Zero API Key, Anonymous Free Tier)
        foreach (var model in FreeOvhModels)
        {
            try
            {
                var result = await TryChatCompletionAsync(
                    "https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/chat/completions",
                    model,
                    apiKey: null,
                    prompt,
                    request,
                    $"SAVI Neural Model ({model})",
                    cancellationToken);

                if (result != null && result.Success)
                {
                    return result;
                }
            }
            catch
            {
                // Try next model in sequence
            }
        }

        // 3. Resilient Fallback: If AI model endpoints are rate-limited on shared cloud IPs,
        // synthesize answers directly from live knowledge sources so SAVI always answers.
        var fallback = await TryKnowledgeFallbackAsync(prompt, cancellationToken);
        if (fallback != null)
        {
            return fallback;
        }

        return ProviderResult.Failed(Id, Name, "Free AI models currently unavailable or rate-limited. Falling back to knowledge retrieval.");
    }

    private async Task<ProviderResult?> TryChatCompletionAsync(
        string endpoint,
        string model,
        string? apiKey,
        string prompt,
        TaskRequest request,
        string sourceTitle,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var messages = new List<object>
            {
                new { role = "system", content = SystemPrompt }
            };

            // Include last 4 context messages for conversational continuity if available
            if (request.Context?.RecentMessages.Count > 0)
            {
                foreach (var m in request.Context.RecentMessages.TakeLast(4))
                {
                    var role = m.Role == MessageRole.User ? "user" : "assistant";
                    messages.Add(new { role, content = m.Content });
                }
            }

            // Append current prompt
            messages.Add(new { role = "user", content = prompt });

            var payload = new
            {
                model,
                messages,
                max_tokens = 600,
                temperature = 0.7
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            httpRequest.Headers.UserAgent.ParseAdd("SAVI-Agent/1.0 (https://savi-4grt.onrender.com)");

            using var response = await _httpClient.SendAsync(httpRequest, linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(linked.Token);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msgObj) &&
                    msgObj.TryGetProperty("content", out var contentProp))
                {
                    var text = contentProp.GetString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var source = new SourceReference
                        {
                            Title = sourceTitle,
                            Url = "https://savi-4grt.onrender.com",
                            SourceName = "SAVI Neural Engine",
                            Snippet = text.Length > 160 ? text[..157] + "..." : text,
                            ReliabilityScore = 0.95
                        };

                        var sources = new[] { source };
                        Cache[prompt.Trim().ToLowerInvariant()] = (text, sources, DateTime.UtcNow.AddMinutes(10));
                        return ProviderResult.Succeeded(Id, Name, text, confidence: 0.95, sources: sources);
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<ProviderResult?> TryKnowledgeFallbackAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var cleanTerm = query
                .Replace("what is", "", StringComparison.OrdinalIgnoreCase)
                .Replace("who is", "", StringComparison.OrdinalIgnoreCase)
                .Replace("explain", "", StringComparison.OrdinalIgnoreCase)
                .Replace("tell me about", "", StringComparison.OrdinalIgnoreCase)
                .Trim(' ', '?', '.', '"');

            if (string.IsNullOrWhiteSpace(cleanTerm)) cleanTerm = query;

            // 1. Wikipedia Summary REST API
            try
            {
                var wikiUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(cleanTerm)}";
                using var wikiReq = new HttpRequestMessage(HttpMethod.Get, wikiUrl);
                wikiReq.Headers.UserAgent.ParseAdd("SAVI-Agent/1.0 (https://savi-4grt.onrender.com)");
                using var wikiResp = await _httpClient.SendAsync(wikiReq, linked.Token);

                if (wikiResp.IsSuccessStatusCode)
                {
                    var wikiJson = await wikiResp.Content.ReadAsStringAsync(linked.Token);
                    using var doc = JsonDocument.Parse(wikiJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("extract", out var extractProp))
                    {
                        var extract = extractProp.GetString();
                        if (!string.IsNullOrWhiteSpace(extract))
                        {
                            var title = root.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? cleanTerm : cleanTerm;
                            var source = new SourceReference
                            {
                                Title = $"{title} — Wikipedia",
                                Url = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title)}",
                                SourceName = "Wikipedia Knowledge Engine",
                                Snippet = extract.Length > 160 ? extract[..157] + "..." : extract,
                                ReliabilityScore = 0.92
                            };
                            return ProviderResult.Succeeded(Id, Name, extract, confidence: 0.90, sources: new[] { source });
                        }
                    }
                }
            }
            catch { }

            // 2. DuckDuckGo Instant Answer API
            try
            {
                var ddgUrl = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";
                using var ddgResp = await _httpClient.GetAsync(ddgUrl, linked.Token);
                if (ddgResp.IsSuccessStatusCode)
                {
                    var ddgJson = await ddgResp.Content.ReadAsStringAsync(linked.Token);
                    using var doc = JsonDocument.Parse(ddgJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("AbstractText", out var absProp))
                    {
                        var absText = absProp.GetString();
                        if (!string.IsNullOrWhiteSpace(absText))
                        {
                            var source = new SourceReference
                            {
                                Title = query,
                                Url = "https://duckduckgo.com",
                                SourceName = "DuckDuckGo Instant Answers",
                                Snippet = absText.Length > 160 ? absText[..157] + "..." : absText,
                                ReliabilityScore = 0.88
                            };
                            return ProviderResult.Succeeded(Id, Name, absText, confidence: 0.88, sources: new[] { source });
                        }
                    }
                }
            }
            catch { }
        }
        catch { }

        return null;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.GetAsync("https://oai.endpoints.kepler.ai.cloud.ovh.net/v1/models", linked.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
