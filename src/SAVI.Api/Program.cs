using SAVI.Agent;
using SAVI.Api.Endpoints;
using SAVI.Api.Hubs;
using SAVI.Api.Middleware;
using SAVI.Infrastructure;
using SAVI.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    // Keep malformed or unexpectedly large requests from consuming the API process.
    options.Limits.MaxRequestBodySize = 1_048_576;
});

// Dynamic PORT binding for container deployments
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

// Add services to the container
builder.Services.AddSaviInfrastructure(builder.Configuration);
builder.Services.AddSaviTools();
builder.Services.AddSaviAgent();

builder.Services.AddHealthChecks();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("SaviCors", policy =>
    {
        var configuredOrigins = builder.Configuration["SAVI_CORS_ORIGINS"]?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                             (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<string>();

        if (configuredOrigins.Length > 0)
        {
            policy.WithOrigins(configuredOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Local development can use arbitrary localhost ports; production requires
            // an explicit SAVI_CORS_ORIGINS allow-list.
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// Global Error Handling
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseCors("SaviCors");

// Health Checks
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/health");

// Map Endpoints & Hubs
app.MapSaviEndpoints();
app.MapHub<SaviHub>("/hub/savi");

// Initialize SQLite database
await app.Services.InitializeSaviDatabaseAsync();

app.Run();

// Make Program public for WebApplicationFactory in integration tests
public partial class Program { }
