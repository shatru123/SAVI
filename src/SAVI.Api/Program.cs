using SAVI.Agent;
using SAVI.Api.Endpoints;
using SAVI.Api.Hubs;
using SAVI.Api.Middleware;
using SAVI.Infrastructure;
using SAVI.Tools;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddSaviInfrastructure(builder.Configuration);
builder.Services.AddSaviTools();
builder.Services.AddSaviAgent();

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("SaviCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Global Error Handling
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseCors("SaviCors");

// Map Endpoints & Hubs
app.MapSaviEndpoints();
app.MapHub<SaviHub>("/hub/savi");

// Initialize SQLite database
await app.Services.InitializeSaviDatabaseAsync();

app.Run();

// Make Program public for WebApplicationFactory in integration tests
public partial class Program { }
