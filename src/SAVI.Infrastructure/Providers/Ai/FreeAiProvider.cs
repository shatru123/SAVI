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
    private static DateTime _rateLimitResetTime = DateTime.MinValue;

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
        var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(geminiKey))
        {
            var geminiResult = await TryChatCompletionAsync(
                "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
                "gemini-1.5-flash",
                geminiKey,
                prompt,
                request,
                "Google Gemini-1.5-Flash",
                cancellationToken);

            if (geminiResult != null) return geminiResult;
        }

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

        var ghToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? Environment.GetEnvironmentVariable("GH_TOKEN");
        if (!string.IsNullOrWhiteSpace(ghToken))
        {
            var ghResult = await TryChatCompletionAsync(
                "https://models.inference.ai.azure.com/chat/completions",
                "gpt-4o-mini",
                ghToken,
                prompt,
                request,
                "GitHub Models GPT-4o-mini",
                cancellationToken);

            if (ghResult != null) return ghResult;
        }

        // 2. Query OVHcloud AI Endpoints (Zero API Key, Anonymous Free Tier)
        if (DateTime.UtcNow > _rateLimitResetTime)
        {
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

                    // If rate-limited, break immediately rather than trying other models on same IP
                    if (DateTime.UtcNow < _rateLimitResetTime)
                    {
                        break;
                    }
                }
                catch
                {
                    // Try next model or fallback
                }
            }
        }

        // 3. Technical & Algorithmic Synthesizer (Instant high-confidence code solutions)
        var techResult = TryTechnicalSynthesizer(prompt);
        if (techResult != null)
        {
            return techResult;
        }

        // 4. Resilient Fallback: If AI model endpoints are rate-limited on shared cloud IPs,
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
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3.5));
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
                if (response.StatusCode == global::System.Net.HttpStatusCode.TooManyRequests)
                {
                    _rateLimitResetTime = DateTime.UtcNow.AddSeconds(40);
                }
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

    private static ProviderResult? TryTechnicalSynthesizer(string query)
    {
        var lower = query.ToLowerInvariant();
        if (!lower.Contains("code") && !lower.Contains("function") && !lower.Contains("program") &&
            !lower.Contains("write") && !lower.Contains("implement") && !lower.Contains("reverse") &&
            !lower.Contains("sort") && !lower.Contains("search") && !lower.Contains("fibonacci") &&
            !lower.Contains("palindrome") && !lower.Contains("factorial") && !lower.Contains("linked list"))
        {
            return null;
        }

        string language = "C#";
        if (lower.Contains("python") || lower.Contains("py")) language = "Python";
        else if (lower.Contains("javascript") || lower.Contains("js") || lower.Contains("node")) language = "JavaScript";
        else if (lower.Contains("typescript") || lower.Contains("ts")) language = "TypeScript";
        else if (lower.Contains("java\b")) language = "Java";
        else if (lower.Contains("c++") || lower.Contains("cpp")) language = "C++";

        string code = "";
        string explanation = "";

        if (lower.Contains("reverse") && (lower.Contains("string") || lower.Contains("text") || lower.Contains("word")))
        {
            if (language == "Python")
            {
                code = "def reverse_string(text: str) -> str:\n    # Using slice notation with step -1\n    return text[::-1]\n\n# Example usage:\nprint(reverse_string('Hello World'))  # Output: dlroW olleH";
                explanation = "In Python, the most idiomatic and performant way to reverse a string is using slice notation `text[::-1]`.";
            }
            else if (language == "JavaScript" || language == "TypeScript")
            {
                code = "function reverseString(str) {\n    return str.split('').reverse().join('');\n}\n\n// Example usage:\nconsole.log(reverseString('Hello World')); // Output: dlroW olleH";
                explanation = "In JavaScript, you split the string into an array of characters, reverse the array, and join it back together.";
            }
            else
            {
                code = "public static string ReverseString(string text)\n{\n    if (string.IsNullOrEmpty(text)) return text;\n    char[] chars = text.ToCharArray();\n    Array.Reverse(chars);\n    return new string(chars);\n}\n\n// Example:\n// string reversed = ReverseString(\"Hello World\"); // Output: dlroW olleH";
                explanation = "In C#, convert the string to a character array, invoke `Array.Reverse()`, and return a new string instance.";
            }
        }
        else if (lower.Contains("fibonacci"))
        {
            if (language == "Python")
            {
                code = "def fibonacci(n: int) -> list[int]:\n    fib = [0, 1]\n    for _ in range(2, n):\n        fib.append(fib[-1] + fib[-2])\n    return fib[:n]\n\nprint(fibonacci(10))";
                explanation = "Calculates the first N Fibonacci numbers in O(n) time and O(n) space complexity.";
            }
            else
            {
                code = "public static List<long> Fibonacci(int n)\n{\n    var list = new List<long>();\n    if (n <= 0) return list;\n    list.Add(0);\n    if (n == 1) return list;\n    list.Add(1);\n    for (int i = 2; i < n; i++)\n    {\n        list.Add(list[i - 1] + list[i - 2]);\n    }\n    return list;\n}";
                explanation = "Calculates the Fibonacci sequence iteratively in linear O(n) time complexity.";
            }
        }
        else if (lower.Contains("binary search"))
        {
            if (language == "Python")
            {
                code = "def binary_search(arr: list[int], target: int) -> int:\n    low, high = 0, len(arr) - 1\n    while low <= high:\n        mid = (low + high) // 2\n        if arr[mid] == target:\n            return mid\n        elif arr[mid] < target:\n            low = mid + 1\n        else:\n            high = mid - 1\n    return -1";
                explanation = "Binary search achieves O(log n) efficiency on a sorted array by halving the search window at each step.";
            }
            else
            {
                code = "public static int BinarySearch(int[] arr, int target)\n{\n    int low = 0, high = arr.Length - 1;\n    while (low <= high)\n    {\n        int mid = low + (high - low) / 2;\n        if (arr[mid] == target) return mid;\n        if (arr[mid] < target) low = mid + 1;\n        else high = mid - 1;\n    }\n    return -1;\n}";
                explanation = "Iterative binary search in O(log n) time avoiding integer overflow in midpoint calculation.";
            }
        }
        else if (lower.Contains("palindrome"))
        {
            if (language == "Python")
            {
                code = "def is_palindrome(s: str) -> bool:\n    clean = ''.join(c.lower() for c in s if c.isalnum())\n    return clean == clean[::-1]\n\nprint(is_palindrome('A man, a plan, a canal: Panama')) # True";
                explanation = "Filters out non-alphanumeric characters and checks if the string equals its reverse.";
            }
            else
            {
                code = "public static bool IsPalindrome(string s)\n{\n    int left = 0, right = s.Length - 1;\n    while (left < right)\n    {\n        while (left < right && !char.IsLetterOrDigit(s[left])) left++;\n        while (left < right && !char.IsLetterOrDigit(s[right])) right--;\n        if (char.ToLower(s[left]) != char.ToLower(s[right])) return false;\n        left++;\n        right--;\n    }\n    return true;\n}";
                explanation = "Two-pointer verification in O(n) time and O(1) space.";
            }
        }
        else if (lower.Contains("factorial"))
        {
            if (language == "Python")
            {
                code = "def factorial(n: int) -> int:\n    if n < 0: raise ValueError('Factorial not defined for negative numbers')\n    result = 1\n    for i in range(2, n + 1):\n        result *= i\n    return result\n\nprint(factorial(5)) # 120";
                explanation = "Iterative factorial calculation with O(n) time and O(1) space.";
            }
            else
            {
                code = "public static long Factorial(int n)\n{\n    if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));\n    long result = 1;\n    for (int i = 2; i <= n; i++) result *= i;\n    return result;\n}";
                explanation = "Iterative factorial calculation avoiding call stack depth limits.";
            }
        }

        if (!string.IsNullOrEmpty(code))
        {
            var content = $"Here is the solution in **{language}**:\n\n```{language.ToLowerInvariant()}\n{code}\n```\n\n{explanation}";
            var source = new SourceReference
            {
                Title = $"{language} Algorithmic Engine",
                Url = "https://savi-4grt.onrender.com",
                SourceName = "SAVI Algorithmic Engine",
                Snippet = explanation,
                ReliabilityScore = 0.98
            };
            return ProviderResult.Succeeded("free_ai", "SAVI Neural Engine (Free Model)", content, confidence: 0.96, sources: new[] { source });
        }

        return null;
    }

    private async Task<ProviderResult?> TryKnowledgeFallbackAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
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
                else
                {
                    // If direct summary returns 404, query Wikipedia Search API for top matching article
                    var searchUrl = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(cleanTerm)}&format=json&utf8=";
                    using var sReq = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                    sReq.Headers.UserAgent.ParseAdd("SAVI-Agent/1.0 (https://savi-4grt.onrender.com)");
                    using var sResp = await _httpClient.SendAsync(sReq, linked.Token);
                    if (sResp.IsSuccessStatusCode)
                    {
                        var sJson = await sResp.Content.ReadAsStringAsync(linked.Token);
                        using var sDoc = JsonDocument.Parse(sJson);
                        if (sDoc.RootElement.TryGetProperty("query", out var qObj) &&
                            qObj.TryGetProperty("search", out var arr) &&
                            arr.GetArrayLength() > 0)
                        {
                            var first = arr[0];
                            var title = first.GetProperty("title").GetString() ?? cleanTerm;
                            var rawSnippet = first.GetProperty("snippet").GetString() ?? "";
                            var plainSnippet = global::System.Text.RegularExpressions.Regex.Replace(rawSnippet, "<.*?>", string.Empty);
                            if (!string.IsNullOrWhiteSpace(plainSnippet))
                            {
                                var source = new SourceReference
                                {
                                    Title = $"{title} — Wikipedia",
                                    Url = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title)}",
                                    SourceName = "Wikipedia Knowledge Engine",
                                    Snippet = plainSnippet,
                                    ReliabilityScore = 0.90
                                };
                                return ProviderResult.Succeeded(Id, Name, $"{title}: {plainSnippet}", confidence: 0.88, sources: new[] { source });
                            }
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
