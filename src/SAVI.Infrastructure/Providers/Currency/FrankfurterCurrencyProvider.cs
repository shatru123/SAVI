using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Currency;

public class FrankfurterCurrencyProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public FrankfurterCurrencyProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.Frankfurter;
    public string Name => "Frankfurter ECB Currency Exchange (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Currency };
    public int Priority => 10;

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Currency, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var from = (request.Parameters.GetValueOrDefault("from") ?? "USD").ToUpperInvariant();
        var to = (request.Parameters.GetValueOrDefault("to") ?? "EUR").ToUpperInvariant();
        var amountStr = request.Parameters.GetValueOrDefault("amount") ?? "1";
        if (!double.TryParse(amountStr, out var amount)) amount = 1.0;

        try
        {
            var url = $"https://api.frankfurter.app/latest?amount={amount}&from={from}&to={to}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"Exchange rate query failed for {from}->{to}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var rates = doc.RootElement.GetProperty("rates");
            if (!rates.TryGetProperty(to, out var rateProp))
            {
                return ProviderResult.Failed(Id, Name, $"Rate for target currency {to} not found.");
            }

            var convertedValue = rateProp.GetDouble();
            var date = doc.RootElement.GetProperty("date").GetString();

            var data = new
            {
                From = from,
                To = to,
                OriginalAmount = amount,
                ConvertedAmount = convertedValue,
                UnitRate = Math.Round(convertedValue / amount, 4),
                EffectiveDate = date
            };

            var source = new SourceReference
            {
                Title = $"Frankfurter ECB Rates: {from} to {to}",
                Url = "https://api.frankfurter.app",
                SourceName = "European Central Bank",
                Snippet = $"{amount} {from} = {convertedValue:N2} {to} (Rate date: {date})",
                ReliabilityScore = 0.99
            };

            return ProviderResult.Succeeded(Id, Name, data, confidence: 0.99, sources: new[] { source });
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Currency conversion failed: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("https://api.frankfurter.app/latest?from=USD&to=EUR", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
