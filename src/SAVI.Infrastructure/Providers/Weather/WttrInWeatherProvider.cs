using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Weather;

public class WttrInWeatherProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public WttrInWeatherProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.WttrIn;
    public string Name => "wttr.in Weather Service (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Weather };
    public int Priority => 20; // Secondary fallback for verification comparison

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Weather, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var city = request.Parameters.GetValueOrDefault("city") ??
                   request.Parameters.GetValueOrDefault("location") ?? "London";

        try
        {
            var url = $"https://wttr.in/{Uri.EscapeDataString(city)}?format=j1";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"wttr.in returned {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var currentCondition = doc.RootElement.GetProperty("current_condition")[0];

            var tempC = double.Parse(currentCondition.GetProperty("temp_C").GetString() ?? "0");
            var humidity = int.Parse(currentCondition.GetProperty("humidity").GetString() ?? "0");
            var desc = currentCondition.GetProperty("weatherDesc")[0].GetProperty("value").GetString() ?? "Fair";
            var windKmph = double.Parse(currentCondition.GetProperty("windspeedKmph").GetString() ?? "0");

            var weatherData = new
            {
                Location = city,
                TemperatureCelsius = tempC,
                TemperatureFahrenheit = Math.Round(tempC * 9 / 5 + 32, 1),
                HumidityPercentage = humidity,
                WindSpeedKmh = windKmph,
                Condition = desc
            };

            var source = new SourceReference
            {
                Title = $"wttr.in Report for {city}",
                Url = $"https://wttr.in/{city}",
                SourceName = "wttr.in",
                Snippet = $"{desc}, {tempC}°C, humidity {humidity}%, wind {windKmph} km/h.",
                ReliabilityScore = 0.90
            };

            return ProviderResult.Succeeded(Id, Name, weatherData, confidence: 0.90, sources: new[] { source });
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Error fetching wttr.in weather: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("https://wttr.in/London?format=3", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
