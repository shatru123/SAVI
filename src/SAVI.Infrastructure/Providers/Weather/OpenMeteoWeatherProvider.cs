using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Weather;

public class OpenMeteoWeatherProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public OpenMeteoWeatherProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.OpenMeteo;
    public string Name => "Open-Meteo Weather Service (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Weather };
    public int Priority => 10;
    public SAVI.Core.Enums.ProviderCategory Category => SAVI.Core.Enums.ProviderCategory.SpecializedPublicApi;
    public SAVI.Core.Enums.ProviderCostType CostType => SAVI.Core.Enums.ProviderCostType.FreePublic;
    public double AuthorityLevel => 0.95;
    public double AccuracyScore => 0.95;
    public double ReliabilityScore => 0.95;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(450);
    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Weather, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly global::System.Collections.Concurrent.ConcurrentDictionary<string, (double Lat, double Lon, string Name, string Country)> GeoCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var city = request.Parameters.GetValueOrDefault("city") ??
                    request.Parameters.GetValueOrDefault("location") ?? "London";

        try
        {
            double lat;
            double lon;
            string resolvedName;
            string country = "";

            if (GeoCache.TryGetValue(city.Trim(), out var cachedGeo))
            {
                lat = cachedGeo.Lat;
                lon = cachedGeo.Lon;
                resolvedName = cachedGeo.Name;
                country = cachedGeo.Country;
            }
            else
            {
                // 1. Geocode city via Open-Meteo geocoding API
                var geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json";
                using var geoResponse = await _httpClient.GetAsync(geoUrl, cancellationToken);
                if (!geoResponse.IsSuccessStatusCode)
                {
                    return ProviderResult.Failed(Id, Name, $"Geocoding failed for {city}");
                }

                var geoJson = await geoResponse.Content.ReadAsStringAsync(cancellationToken);
                using var geoDoc = JsonDocument.Parse(geoJson);

                if (!geoDoc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                {
                    return ProviderResult.Failed(Id, Name, $"Location '{city}' could not be resolved.");
                }

                var first = results[0];
                lat = first.GetProperty("latitude").GetDouble();
                lon = first.GetProperty("longitude").GetDouble();
                resolvedName = first.GetProperty("name").GetString() ?? city;
                country = first.TryGetProperty("country", out var cProp) ? cProp.GetString() ?? "" : "";

                GeoCache.TryAdd(city.Trim(), (lat, lon, resolvedName, country));
            }

            // 2. Fetch current weather
            var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m&timezone=auto";
            using var weatherResponse = await _httpClient.GetAsync(weatherUrl, cancellationToken);
            if (!weatherResponse.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"Weather forecast query failed with code {weatherResponse.StatusCode}");
            }

            var weatherJson = await weatherResponse.Content.ReadAsStringAsync(cancellationToken);
            using var weatherDoc = JsonDocument.Parse(weatherJson);
            var current = weatherDoc.RootElement.GetProperty("current");

            var temp = current.GetProperty("temperature_2m").GetDouble();
            var humidity = current.GetProperty("relative_humidity_2m").GetInt32();
            var feelsLike = current.GetProperty("apparent_temperature").GetDouble();
            var windSpeed = current.GetProperty("wind_speed_10m").GetDouble();
            var weatherCode = current.GetProperty("weather_code").GetInt32();
            var conditionDesc = MapWeatherCode(weatherCode);

            var weatherData = new
            {
                Location = $"{resolvedName}{(string.IsNullOrEmpty(country) ? "" : ", " + country)}",
                TemperatureCelsius = temp,
                TemperatureFahrenheit = Math.Round(temp * 9 / 5 + 32, 1),
                FeelsLikeCelsius = feelsLike,
                HumidityPercentage = humidity,
                WindSpeedKmh = windSpeed,
                Condition = conditionDesc
            };

            var source = new SourceReference
            {
                Title = $"Open-Meteo Current Weather for {resolvedName}",
                Url = "https://open-meteo.com",
                SourceName = "Open-Meteo",
                Snippet = $"{conditionDesc}, {temp}°C (feels like {feelsLike}°C), humidity {humidity}%, wind {windSpeed} km/h.",
                ReliabilityScore = 0.95
            };

            return ProviderResult.Succeeded(Id, Name, weatherData, confidence: 0.95, sources: new[] { source });
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Error fetching weather: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("https://geocoding-api.open-meteo.com/v1/search?name=London&count=1", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string MapWeatherCode(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Foggy",
        51 or 53 or 55 => "Drizzle",
        61 or 63 or 65 => "Rain",
        71 or 73 or 75 => "Snow fall",
        80 or 81 or 82 => "Rain showers",
        95 or 96 or 99 => "Thunderstorm",
        _ => "Fair weather"
    };
}
