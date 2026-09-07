using SAVI.Agent;
using SAVI.Infrastructure;
using SAVI.Tools;
using SAVI.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Dynamic PORT binding for container deployments (Render, Cloud Run, Heroku)
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

// Add SAVI Core, Infrastructure, Tools, and Agent services
builder.Services.AddSaviInfrastructure(builder.Configuration);
builder.Services.AddSaviTools();
builder.Services.AddSaviAgent();

// Add Health Checks
builder.Services.AddHealthChecks();

// Add Blazor Interactive Server Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

// Health Check Endpoints
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/health");
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    system = "SAVI (Shatru's Adaptive Virtual Intelligence)",
    version = "1.0.0",
    creator = "Shatrughna Ambhore",
    timestamp = DateTimeOffset.UtcNow,
    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
}));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Initialize SQLite database
await app.Services.InitializeSaviDatabaseAsync();

app.Run();
