namespace SAVI.Core.Constants;

public static class SaviConstants
{
    public const string SystemName = "SAVI";
    public const string FullName = "Shatru's Adaptive Virtual Intelligence";
    public const string Version = "1.0.0";
    public const string DefaultUser = "Shatru";

    public static class Capabilities
    {
        public const string Weather = "weather";
        public const string Currency = "currency";
        public const string Knowledge = "knowledge";
        public const string Search = "search";
        public const string GitHub = "github";
        public const string System = "system";
        public const string FileSystem = "filesystem";
        public const string Terminal = "terminal";
        public const string Browser = "browser";
        public const string Calculator = "calculator";
        public const string Time = "time";
        public const string Document = "document";
        public const string Memory = "memory";
        public const string Reasoning = "reasoning";
    }

    public static class Providers
    {
        public const string OpenMeteo = "provider-openmeteo";
        public const string WttrIn = "provider-wttrin";
        public const string Frankfurter = "provider-frankfurter";
        public const string Wikipedia = "provider-wikipedia";
        public const string DuckDuckGo = "provider-duckduckgo";
        public const string GitHubPublic = "provider-github";
        public const string LocalSystem = "provider-local-system";
        public const string LocalCalculator = "provider-local-calc";
        public const string GenericHttp = "provider-generic-http";
        public const string Ollama = "provider-ollama";
    }
}
