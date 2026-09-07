using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Finance;

public class CoinGeckoCryptoProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public CoinGeckoCryptoProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.CoinGecko;
    public string Name => "CoinGecko Keyless Crypto Market Rates (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Crypto };
    public int Priority => 15;
    public ProviderCategory Category => ProviderCategory.SpecializedPublicApi;
    public ProviderCostType CostType => ProviderCostType.FreePublic;
    public double AuthorityLevel => 0.90;
    public double AccuracyScore => 0.95;
    public double ReliabilityScore => 0.85;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(450);
    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Crypto, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var coinInput = request.Parameters.GetValueOrDefault("coin") ??
                        request.Parameters.GetValueOrDefault("crypto") ??
                        request.Parameters.GetValueOrDefault("query") ??
                        request.Prompt;

        var coinId = MapToCoinId(coinInput);

        try
        {
            var url = $"https://api.coingecko.com/api/v3/simple/price?ids={coinId}&vs_currencies=usd,inr,eur&include_24hr_change=true";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0");

            using var cts = new CancellationTokenSource(Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.SendAsync(req, linked.Token);

            if ((int)resp.StatusCode == 429)
            {
                return ProviderResult.Failed(Id, Name, "CoinGecko rate limit reached (HTTP 429). Please retry in 60 seconds.");
            }

            if (!resp.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"CoinGecko returned HTTP {(int)resp.StatusCode}");
            }

            var json = await resp.Content.ReadAsStringAsync(linked.Token);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty(coinId, out var coinObj))
            {
                var usd = coinObj.TryGetProperty("usd", out var u) ? u.GetDouble() : 0.0;
                var inr = coinObj.TryGetProperty("inr", out var i) ? i.GetDouble() : 0.0;
                var eur = coinObj.TryGetProperty("eur", out var e) ? e.GetDouble() : 0.0;
                var change24h = coinObj.TryGetProperty("usd_24h_change", out var ch) ? ch.GetDouble() : 0.0;

                var cryptoData = new
                {
                    Coin = coinId.ToUpperInvariant(),
                    PriceUsd = usd,
                    PriceInr = inr,
                    PriceEur = eur,
                    Change24hPercent = Math.Round(change24h, 2),
                    Formatted = $"**{coinId.ToUpperInvariant()}**: ${usd:N2} USD (₹{inr:N2} INR, €{eur:N2} EUR) | 24h Change: {change24h:+0.00;-0.00}%"
                };

                var source = new SourceReference
                {
                    Title = $"{coinId.ToUpperInvariant()} Live Market Rate — CoinGecko",
                    Url = $"https://www.coingecko.com/en/coins/{coinId}",
                    SourceName = "CoinGecko Public API",
                    Snippet = $"{coinId.ToUpperInvariant()}: ${usd:N2} USD, ₹{inr:N2} INR (24h Change: {change24h:+0.00;-0.00}%)",
                    ReliabilityScore = 0.90
                };

                return ProviderResult.Succeeded(Id, Name, cryptoData, confidence: 0.90, sources: new[] { source });
            }

            return ProviderResult.Failed(Id, Name, $"Could not find price data for crypto symbol \"{coinInput}\".");
        }
        catch (OperationCanceledException)
        {
            return ProviderResult.Failed(Id, Name, "CoinGecko request timed out.");
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"CoinGecko error: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd");
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0");
            using var resp = await _httpClient.SendAsync(req, linked.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string MapToCoinId(string input)
    {
        var clean = input.ToLowerInvariant()
                         .Replace("price", "")
                         .Replace("rate", "")
                         .Replace("what is the", "")
                         .Replace("what is", "")
                         .Replace("how much is", "")
                         .Replace("crypto", "")
                         .Replace("coin", "")
                         .Replace("?", "")
                         .Trim();

        return clean switch
        {
            "btc" or "bitcoin" => "bitcoin",
            "eth" or "ethereum" => "ethereum",
            "sol" or "solana" => "solana",
            "doge" or "dogecoin" => "dogecoin",
            "ada" or "cardano" => "cardano",
            "xrp" or "ripple" => "ripple",
            "dot" or "polkadot" => "polkadot",
            "matic" or "polygon" => "matic-network",
            "bnb" or "binance" => "binancecoin",
            "link" or "chainlink" => "chainlink",
            "avax" or "avalanche" => "avalanche-2",
            _ => clean.Length > 0 ? clean : "bitcoin"
        };
    }
}
