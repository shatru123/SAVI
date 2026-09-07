using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Tools.Browser;

public class BrowserAgentTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly IAuditService _auditService;

    public BrowserAgentTool(HttpClient httpClient, IAuditService auditService)
    {
        _httpClient = httpClient;
        _auditService = auditService;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (SAVI-BrowserAgent/1.0; +https://github.com/shatru123/SAVI)");
    }

    public string Name => "browser";
    public string Description => "Automated web browsing, page content extraction, and website verification using Playwright / HTTP engine.";
    public PermissionLevel PermissionLevel => PermissionLevel.Controlled;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var action = input.Action.ToLowerInvariant();
        var url = input.Arguments.GetValueOrDefault("url") ?? "";

        if (string.IsNullOrWhiteSpace(url))
        {
            return ToolExecutionResult.Failed(Name, "URL is required for browser actions.");
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        // Respect security and robots / paywalls
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 403 || (int)response.StatusCode == 429)
                {
                    return ToolExecutionResult.Failed(Name, "The website has automated access restrictions or rate limiting enabled. I stopped rather than bypassing site policy.");
                }
                return ToolExecutionResult.Failed(Name, $"HTTP response: {response.StatusCode}");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // Extract readable text from HTML
            var text = ExtractTextFromHtml(html);
            if (text.Length > 8000)
            {
                text = text[..7950] + "\n... [Content truncated for display]";
            }

            await _auditService.LogAsync($"Browser visited {url}", Name, PermissionLevel.Controlled, "Success", $"{text.Length} chars extracted", input.UserConfirmed, input.TaskId, cancellationToken);
            return ToolExecutionResult.Succeeded(Name, $"Page Content from {url}:\n\n{text}");
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failed(Name, $"Browser automation error: {ex.Message}");
        }
    }

    private static string ExtractTextFromHtml(string html)
    {
        // Remove scripts and styles
        var noScripts = System.Text.RegularExpressions.Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var noStyles = System.Text.RegularExpressions.Regex.Replace(noScripts, @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Strip tags
        var noTags = System.Text.RegularExpressions.Regex.Replace(noStyles, @"<[^>]+>", " ");
        // Decode entities
        var decoded = System.Net.WebUtility.HtmlDecode(noTags);
        // Normalize whitespace
        return System.Text.RegularExpressions.Regex.Replace(decoded, @"\s+", " ").Trim();
    }
}
