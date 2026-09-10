using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using SAVI.Application.Interfaces;
using SAVI.Core.Constants;

namespace SAVI.Application.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly AuthenticationStateProvider? _authStateProvider;
    private ClaimsPrincipal? _overridePrincipal;

    public CurrentUserService(
        IHttpContextAccessor? httpContextAccessor = null,
        AuthenticationStateProvider? authStateProvider = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _authStateProvider = authStateProvider;
    }

    public void SetUser(ClaimsPrincipal principal)
    {
        _overridePrincipal = principal;
    }

    private ClaimsPrincipal? Principal
    {
        get
        {
            if (_overridePrincipal != null)
            {
                return _overridePrincipal;
            }

            if (_httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                return _httpContextAccessor.HttpContext.User;
            }

            if (_authStateProvider != null)
            {
                try
                {
                    var authTask = _authStateProvider.GetAuthenticationStateAsync();
                    if (authTask.IsCompletedSuccessfully)
                    {
                        var state = authTask.Result;
                        if (state?.User?.Identity?.IsAuthenticated == true)
                        {
                            return state.User;
                        }
                    }
                }
                catch
                {
                    // In detached circuits or testing, swallow exception
                }
            }

            return null;
        }
    }

    public string? UserId => Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value;
    public string? DisplayName => Principal?.FindFirst(ClaimTypes.Name)?.Value;
    public string? Role => Principal?.FindFirst(ClaimTypes.Role)?.Value;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public bool IsOwner => string.Equals(Role, SaviConstants.Roles.Owner, StringComparison.OrdinalIgnoreCase);
}
