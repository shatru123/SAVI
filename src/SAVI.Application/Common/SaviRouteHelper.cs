namespace SAVI.Application.Common;

/// <summary>
/// Authoritative helper for SAVI SPA routing, conversation ID extraction, and navigation path generation.
/// </summary>
public static class SaviRouteHelper
{
    public static string? ExtractConversationId(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return null;

        var clean = uri.Trim();
        if (Uri.TryCreate(clean, UriKind.Absolute, out var parsedUri))
        {
            clean = parsedUri.AbsolutePath;
        }
        else
        {
            clean = clean.Split('?', '#')[0];
        }

        var parts = clean.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && (parts[0].Equals("chat", StringComparison.OrdinalIgnoreCase) || parts[0].Equals("voice", StringComparison.OrdinalIgnoreCase)))
        {
            return parts[1];
        }

        return null;
    }

    public static bool IsVoiceRoute(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return false;

        var clean = uri.Trim();
        if (Uri.TryCreate(clean, UriKind.Absolute, out var parsedUri))
        {
            clean = parsedUri.AbsolutePath;
        }
        else
        {
            clean = clean.Split('?', '#')[0];
        }

        var parts = clean.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts[0].Equals("voice", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsChatRoute(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return true;

        var clean = uri.Trim();
        if (Uri.TryCreate(clean, UriKind.Absolute, out var parsedUri))
        {
            clean = parsedUri.AbsolutePath;
        }
        else
        {
            clean = clean.Split('?', '#')[0];
        }

        var parts = clean.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 || parts[0].Equals("chat", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildVoiceRoute(string? conversationId)
    {
        return string.IsNullOrWhiteSpace(conversationId)
            ? "/voice"
            : $"/voice/{conversationId.Trim()}";
    }

    public static string BuildChatRoute(string? conversationId)
    {
        return string.IsNullOrWhiteSpace(conversationId)
            ? "/chat"
            : $"/chat/{conversationId.Trim()}";
    }

    public static string BuildToggleModeRoute(string currentUri, string? activeConversationId)
    {
        var convId = !string.IsNullOrWhiteSpace(activeConversationId)
            ? activeConversationId
            : ExtractConversationId(currentUri);

        return IsVoiceRoute(currentUri)
            ? BuildChatRoute(convId)
            : BuildVoiceRoute(convId);
    }
}
