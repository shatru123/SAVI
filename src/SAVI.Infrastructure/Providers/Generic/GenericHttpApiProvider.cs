using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;
using SAVI.Infrastructure.Security;

namespace SAVI.Infrastructure.Providers.Generic;

public class GenericHttpApiProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;
    private readonly ApiProviderDefinition _definition;

    public GenericHttpApiProvider(HttpClient httpClient, ApiProviderDefinition definition)
    {
        _httpClient = httpClient;
        _definition = definition;
    }

    public string Id => _definition.Id;
    public string Name => _definition.Name;
    public IReadOnlyCollection<string> Capabilities => new[] { _definition.Capability };
    public int Priority => _definition.Priority;

    public bool CanHandle(TaskRequest request)
    {
        return _definition.IsEnabled &&
               request.Capability.Equals(_definition.Capability, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        // 1. SSRF Validation
        if (!SsrfValidator.IsUrlSafe(_definition.Endpoint, out var reason))
        {
            return ProviderResult.Failed(Id, Name, $"Security restriction: {reason}");
        }

        try
        {
            // Build URL with parameter substitution
            var endpoint = _definition.Endpoint;
            foreach (var (k, v) in request.Parameters)
            {
                endpoint = endpoint.Replace($"{{{k}}}", Uri.EscapeDataString(v));
            }

            using var req = new HttpRequestMessage(new HttpMethod(_definition.Method), endpoint);

            // Add custom headers if configured
            if (!string.IsNullOrWhiteSpace(_definition.HeadersJson))
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(_definition.HeadersJson);
                if (headers != null)
                {
                    foreach (var (hk, hv) in headers)
                    {
                        req.Headers.TryAddWithoutValidation(hk, hv);
                    }
                }
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_definition.TimeoutSeconds > 0 ? _definition.TimeoutSeconds : 15));

            using var response = await _httpClient.SendAsync(req, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"Generic provider returned status code {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            var source = new SourceReference
            {
                Title = _definition.Name,
                Url = endpoint,
                SourceName = _definition.Name,
                Snippet = content.Length > 200 ? content[..197] + "..." : content,
                ReliabilityScore = 0.85
            };

            return ProviderResult.Succeeded(Id, Name, content, confidence: 0.85, sources: new[] { source });
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Generic provider execution failed: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        if (!SsrfValidator.IsUrlSafe(_definition.Endpoint, out _)) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Head, _definition.Endpoint);
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
