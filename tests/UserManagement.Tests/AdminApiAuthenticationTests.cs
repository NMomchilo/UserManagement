using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UserManagement.Service.Security;

namespace UserManagement.Tests;

public sealed class AdminApiAuthenticationTests
{
    [Fact]
    public async Task AdministratorWithoutAuthenticatedActor_IsRejectedByUserManagementAsync()
    {
        bool userManagementCalled = false;
        UserManagementRequestMiddleware users = new(_ =>
        {
            userManagementCalled = true;
            return Task.CompletedTask;
        }, NullLogger<UserManagementRequestMiddleware>.Instance);
        DefaultHttpContext userContext = new();
        userContext.Request.Method = "GET";
        userContext.Request.Path = "/v1/users";
        await users.InvokeAsync(userContext);
        Assert.Equal(StatusCodes.Status401Unauthorized, userContext.Response.StatusCode);
        Assert.False(userManagementCalled);

    }
}
