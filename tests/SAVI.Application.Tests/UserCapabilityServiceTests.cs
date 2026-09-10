using System.Security.Claims;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Constants;
using Xunit;

namespace SAVI.Application.Tests;

public class UserCapabilityServiceTests
{
    private static CurrentUserService CreateUser(string? userId, string? role)
    {
        var service = new CurrentUserService();
        if (userId != null && role != null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Role, role),
                new(ClaimTypes.Name, $"User {userId}"),
                new(ClaimTypes.Email, $"{userId}@example.com")
            };
            service.SetUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));
        }
        return service;
    }

    [Fact]
    public void Unauthenticated_User_Has_Public_Level()
    {
        var currentUserService = CreateUser(null, null);
        var capabilityService = new UserCapabilityService(currentUserService);

        Assert.True(capabilityService.CanAccess(UserCapability.BasicChat));
        Assert.True(capabilityService.CanAccess(UserCapability.Weather));
        Assert.True(capabilityService.CanAccess(UserCapability.PublicSearch));
        Assert.False(capabilityService.CanAccess(UserCapability.Memory));
        Assert.False(capabilityService.CanAccess(UserCapability.Tasks));
        Assert.False(capabilityService.CanAccess(UserCapability.Automations));
        Assert.False(capabilityService.CanAccess(UserCapability.Admin));
    }

    [Fact]
    public void Authenticated_User_Can_Access_Memory_Tasks_Automations()
    {
        var currentUserService = CreateUser("user-1", SaviConstants.Roles.User);
        var capabilityService = new UserCapabilityService(currentUserService);

        Assert.True(capabilityService.CanAccess(UserCapability.BasicChat));
        Assert.True(capabilityService.CanAccess(UserCapability.Weather));
        Assert.True(capabilityService.CanAccess(UserCapability.Memory));
        Assert.True(capabilityService.CanAccess(UserCapability.Tasks));
        Assert.True(capabilityService.CanAccess(UserCapability.Automations));
        Assert.False(capabilityService.CanAccess(UserCapability.Admin));
    }

    [Fact]
    public void Owner_Can_Access_All_Capabilities()
    {
        var currentUserService = CreateUser("owner-1", SaviConstants.Roles.Owner);
        var capabilityService = new UserCapabilityService(currentUserService);

        Assert.True(capabilityService.CanAccess(UserCapability.Admin));
        Assert.True(capabilityService.CanAccess(UserCapability.Automations));
        Assert.True(capabilityService.CanAccess(UserCapability.Memory));
        Assert.True(capabilityService.CanAccess(UserCapability.Tasks));
    }

    [Theory]
    [InlineData(UserCapability.Memory, "Memory")]
    [InlineData(UserCapability.Tasks, "Task")]
    [InlineData(UserCapability.Automations, "Automation")]
    public void GetGateTitle_Returns_Contextual_Title(UserCapability capability, string expectedSubstring)
    {
        var currentUserService = CreateUser(null, null);
        var capabilityService = new UserCapabilityService(currentUserService);

        var title = capabilityService.GetGatingTitle(capability);
        Assert.Contains(expectedSubstring, title);
    }
}
