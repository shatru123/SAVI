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

// Add Health Checks & HttpClient
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());

// Add Blazor Interactive Server Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Cookie Authentication & Authorization
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "SAVI_AUTH";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOnly", policy => policy.RequireRole(SAVI.Core.Constants.SaviConstants.Roles.Owner));
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Auth Endpoints
app.MapPost("/api/auth/login", async (SAVI.Application.DTOs.LoginRequestDto request, SAVI.Application.Interfaces.IAuthService authService, HttpContext context) =>
{
    var (success, error, user) = await authService.LoginAsync(request.Email, request.Password);
    if (!success || user == null)
    {
        return Results.BadRequest(new { success = false, message = error ?? "Invalid credentials" });
    }

    var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>
    {
        new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
        new(System.Security.Claims.ClaimTypes.Email, user.Email),
        new(System.Security.Claims.ClaimTypes.Name, user.DisplayName),
        new(System.Security.Claims.ClaimTypes.Role, user.Role)
    };

    var identity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new System.Security.Claims.ClaimsPrincipal(identity);
    var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        IsPersistent = request.RememberMe,
        ExpiresUtc = request.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null
    };

    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignInAsync(context, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    return Results.Ok(new { success = true, user, redirect = "/" });
});

app.MapPost("/api/auth/register", async (SAVI.Application.DTOs.RegisterRequestDto request, SAVI.Application.Interfaces.IAuthService authService, HttpContext context) =>
{
    var (success, error, user) = await authService.RegisterAsync(request);
    if (!success || user == null)
    {
        return Results.BadRequest(new { success = false, message = error ?? "Registration failed" });
    }

    var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>
    {
        new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
        new(System.Security.Claims.ClaimTypes.Email, user.Email),
        new(System.Security.Claims.ClaimTypes.Name, user.DisplayName),
        new(System.Security.Claims.ClaimTypes.Role, user.Role)
    };

    var identity = new System.Security.Claims.ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new System.Security.Claims.ClaimsPrincipal(identity);
    var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        IsPersistent = true,
        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
    };

    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignInAsync(context, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    return Results.Ok(new { success = true, user, redirect = "/" });
});

app.MapGet("/api/auth/logout", async (HttpContext context) =>
{
    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(context, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapPost("/api/auth/logout", async (HttpContext context) =>
{
    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(context, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { success = true, redirect = "/login" });
});

app.MapGet("/api/auth/me", (SAVI.Application.Interfaces.ICurrentUserService currentUserService) =>
{
    if (!currentUserService.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        userId = currentUserService.UserId,
        email = currentUserService.Email,
        displayName = currentUserService.DisplayName,
        role = currentUserService.Role,
        isOwner = currentUserService.IsOwner
    });
});

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
