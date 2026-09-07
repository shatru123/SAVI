using System.Data;
using System.Text.RegularExpressions;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Calculator;

public class CalculatorProvider : ICapabilityProvider
{
    private static readonly Regex SafeMathRegex = new(@"^[0-9\.\+\-\*\/\(\)\s\%\^\,]+$", RegexOptions.Compiled);

    public string Id => SaviConstants.Providers.LocalCalculator;
    public string Name => "Deterministic Calculator & Converter (Local/Zero-Cost)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Calculator };
    public int Priority => 1;
    public ProviderCategory Category => ProviderCategory.LocalDeterministic;
    public ProviderCostType CostType => ProviderCostType.LocalZeroCost;
    public double AuthorityLevel => 1.0;
    public double AccuracyScore => 1.0;
    public double ReliabilityScore => 1.0;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(5);
    public TimeSpan Timeout => TimeSpan.FromMilliseconds(200);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Calculator, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var expr = request.Parameters.GetValueOrDefault("expression") ??
                   request.Parameters.GetValueOrDefault("formula") ?? request.Prompt;

        try
        {
            // Unit conversion checks: e.g. "10 km to miles" or "100 c to f"
            var unitResult = TryConvertUnits(expr);
            if (unitResult != null)
            {
                var sourceUnit = new SourceReference
                {
                    Title = "Unit Converter",
                    Url = "local://converter",
                    SourceName = "SAVI Local Converter",
                    Snippet = unitResult.Message,
                    ReliabilityScore = 1.0
                };
                return Task.FromResult(ProviderResult.Succeeded(Id, Name, unitResult, confidence: 1.0, sources: new[] { sourceUnit }));
            }

            // Extract numeric math expression
            var mathCandidate = ExtractMathExpression(expr);
            if (string.IsNullOrWhiteSpace(mathCandidate) || !SafeMathRegex.IsMatch(mathCandidate))
            {
                return Task.FromResult(ProviderResult.Failed(Id, Name, "Could not extract a safe mathematical expression to compute."));
            }

            // Clean formula
            var sanitized = mathCandidate.Replace("%", "*0.01").Replace("^", "**");
            var dt = new DataTable();
            var computeResult = dt.Compute(sanitized, null);
            var resultValue = Convert.ToDouble(computeResult);

            var calcData = new
            {
                Expression = mathCandidate,
                Result = resultValue,
                Formatted = $"{mathCandidate} = {resultValue:G10}"
            };

            var source = new SourceReference
            {
                Title = "Local Mathematical Evaluation",
                Url = "local://calculator",
                SourceName = "SAVI Local Math Engine",
                Snippet = $"{mathCandidate} = {resultValue:G10}",
                ReliabilityScore = 1.0
            };

            return Task.FromResult(ProviderResult.Succeeded(Id, Name, calcData, confidence: 1.0, sources: new[] { source }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ProviderResult.Failed(Id, Name, $"Calculation failed: {ex.Message}"));
        }
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    private static string ExtractMathExpression(string input)
    {
        var clean = input.Replace("calculate", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("what is", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("what's", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("evaluate", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("solve", "", StringComparison.OrdinalIgnoreCase)
                         .Replace("equals", "=", StringComparison.OrdinalIgnoreCase)
                         .Replace("=", "")
                         .Replace("?", "")
                         .Trim();
        return clean;
    }

    private static dynamic? TryConvertUnits(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        var matchKmToMiles = Regex.Match(lower, @"([\d\.]+)\s*(?:km|kilometers?)\s+(?:to|in)\s+(?:miles?|mi)");
        if (matchKmToMiles.Success && double.TryParse(matchKmToMiles.Groups[1].Value, out var km))
        {
            var miles = km * 0.621371;
            return new { FromValue = km, FromUnit = "km", ToValue = Math.Round(miles, 3), ToUnit = "miles", Message = $"{km} km = {miles:F2} miles" };
        }

        var matchMilesToKm = Regex.Match(lower, @"([\d\.]+)\s*(?:miles?|mi)\s+(?:to|in)\s+(?:km|kilometers?)");
        if (matchMilesToKm.Success && double.TryParse(matchMilesToKm.Groups[1].Value, out var milesVal))
        {
            var kmVal = milesVal * 1.60934;
            return new { FromValue = milesVal, FromUnit = "miles", ToValue = Math.Round(kmVal, 3), ToUnit = "km", Message = $"{milesVal} miles = {kmVal:F2} km" };
        }

        var matchCtoF = Regex.Match(lower, @"([\d\.]+)\s*(?:c|celsius)\s+(?:to|in)\s+(?:f|fahrenheit)");
        if (matchCtoF.Success && double.TryParse(matchCtoF.Groups[1].Value, out var cVal))
        {
            var fVal = (cVal * 9 / 5) + 32;
            return new { FromValue = cVal, FromUnit = "°C", ToValue = Math.Round(fVal, 1), ToUnit = "°F", Message = $"{cVal}°C = {fVal:F1}°F" };
        }

        var matchFtoC = Regex.Match(lower, @"([\d\.]+)\s*(?:f|fahrenheit)\s+(?:to|in)\s+(?:c|celsius)");
        if (matchFtoC.Success && double.TryParse(matchFtoC.Groups[1].Value, out var fIn))
        {
            var cOut = (fIn - 32) * 5 / 9;
            return new { FromValue = fIn, FromUnit = "°F", ToValue = Math.Round(cOut, 1), ToUnit = "°C", Message = $"{fIn}°F = {cOut:F1}°C" };
        }

        return null;
    }
}
