using System.Globalization;
using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Location;

public class NominatimLocationProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public NominatimLocationProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.Nominatim;
    public string Name => "OpenStreetMap Nominatim Geocoding (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Location };
    public int Priority => 15;
    public ProviderCategory Category => ProviderCategory.SpecializedPublicApi;
    public ProviderCostType CostType => ProviderCostType.FreePublic;
    public double AuthorityLevel => 0.95;
    public double AccuracyScore => 0.95;
    public double ReliabilityScore => 0.95;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(450);
    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Location, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var place = request.Parameters.GetValueOrDefault("place") ??
                    request.Parameters.GetValueOrDefault("city") ??
                    request.Parameters.GetValueOrDefault("query") ??
                    request.Prompt;

        if (string.IsNullOrWhiteSpace(place))
        {
            return ProviderResult.Failed(Id, Name, "Place name is empty.");
        }

        try
        {
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(place.Trim())}&format=json&limit=1&addressdetails=1";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0 (https://github.com/shatru123/SAVI)");

            using var cts = new CancellationTokenSource(Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.SendAsync(req, linked.Token);

            if (!resp.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"Nominatim returned HTTP {(int)resp.StatusCode}");
            }

            var json = await resp.Content.ReadAsStringAsync(linked.Token);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.GetArrayLength() == 0)
            {
                return ProviderResult.Failed(Id, Name, $"No location coordinates found for \"{place}\".");
            }

            var item = doc.RootElement[0];
            var displayName = item.GetProperty("display_name").GetString() ?? place;
            var latStr = item.GetProperty("lat").GetString() ?? "0";
            var lonStr = item.GetProperty("lon").GetString() ?? "0";
            var type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "place" : "place";

            double.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lat);
            double.TryParse(lonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lon);

            var locationData = new
            {
                Place = place,
                DisplayName = displayName,
                Latitude = lat,
                Longitude = lon,
                Type = type,
                MapUrl = $"https://www.openstreetmap.org/#map=13/{lat:F4}/{lon:F4}"
            };

            var source = new SourceReference
            {
                Title = $"{displayName} — OpenStreetMap",
                Url = $"https://www.openstreetmap.org/#map=13/{lat:F4}/{lon:F4}",
                SourceName = "OpenStreetMap / Nominatim",
                Snippet = $"{displayName} (Coordinates: {lat:F4}, {lon:F4})",
                ReliabilityScore = 0.95
            };

            return ProviderResult.Succeeded(Id, Name, locationData, confidence: 0.95, sources: new[] { source });
        }
        catch (OperationCanceledException)
        {
            return ProviderResult.Failed(Id, Name, "Nominatim request timed out.");
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Nominatim error: {ex.Message}");
        }
    }

    public async Task<(double Lat, double Lon, string DisplayName)?> GeocodeAsync(string place, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(place.Trim())}&format=json&limit=1";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.SendAsync(req, linked.Token);

            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(linked.Token);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.GetArrayLength() == 0) return null;

            var item = doc.RootElement[0];
            var displayName = item.GetProperty("display_name").GetString() ?? place;
            var latStr = item.GetProperty("lat").GetString() ?? "0";
            var lonStr = item.GetProperty("lon").GetString() ?? "0";

            if (double.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(lonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var lon))
            {
                return (lat, lon, displayName);
            }
        }
        catch { }
        return null;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://nominatim.openstreetmap.org/search?q=London&format=json&limit=1");
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.SendAsync(req, linked.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
