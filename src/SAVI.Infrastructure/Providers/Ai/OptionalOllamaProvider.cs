using System.Text;
using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Ai;

public class OptionalOllamaProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _modelName;

    public OptionalOllamaProvider(HttpClient httpClient, string baseUrl = "http://localhost:11434", string modelName = "llama3")
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
        _modelName = modelName;
    }

    public string Id => SaviConstants.Providers.Ollama;
    public string Name => $"Local Ollama LLM ({_modelName}) [Optional]";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Reasoning };
    public int Priority => 50;
    public SAVI.Core.Enums.ProviderCategory Category => SAVI.Core.Enums.ProviderCategory.ReasoningSynthesis;
    public SAVI.Core.Enums.ProviderCostType CostType => SAVI.Core.Enums.ProviderCostType.LocalZeroCost;
    public double AuthorityLevel => 0.50;
    public double AccuracyScore => 0.80;
    public double ReliabilityScore => 0.70;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(2000);
    public TimeSpan Timeout => TimeSpan.FromSeconds(5);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = request.Parameters.GetValueOrDefault("prompt") ?? request.Prompt;
            var body = new
            {
                model = _modelName,
                prompt = prompt,
                stream = false
            };

            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"Ollama returned code {response.StatusCode}. Ensure Ollama is running locally.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var responseText = doc.RootElement.GetProperty("response").GetString() ?? "";

            var source = new SourceReference
            {
                Title = $"Ollama Local Model ({_modelName})",
                Url = "http://localhost:11434",
                SourceName = "Local Ollama",
                Snippet = responseText.Length > 150 ? responseText[..147] + "..." : responseText,
                ReliabilityScore = 0.85
            };

            return ProviderResult.Succeeded(Id, Name, responseText, confidence: 0.85, sources: new[] { source });
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Local Ollama not reachable: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.GetAsync($"{_baseUrl}/api/tags", linked.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
