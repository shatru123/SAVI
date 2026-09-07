using SAVI.Agent;
using SAVI.Infrastructure;
using SAVI.Tools;
using SAVI.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add SAVI Core, Infrastructure, Tools, and Agent services
builder.Services.AddSaviInfrastructure(builder.Configuration);
builder.Services.AddSaviTools();
builder.Services.AddSaviAgent();

// Add Blazor Interactive Server Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Initialize SQLite database
await app.Services.InitializeSaviDatabaseAsync();

app.Run();
