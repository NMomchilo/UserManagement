using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UserManagement.Service.Security;

namespace UserManagement.Tests;

public sealed class AdminApiAuthenticationTests
{
    [Fact]
    public async Task AdministratorKeyWithoutAuthenticatedActor_IsRejectedByUserManagementAsync()
    {
        bool userManagementCalled = false;
        UserManagementKeyMiddleware users = new(_ =>
        {
            userManagementCalled = true;
            return Task.CompletedTask;
        }, Options.Create(new UserManagementKeyOptions
        {
            Clients = [new ServiceCredential { ClientId = "client", Key = "client-key" }],
            Administrators = [new AdministratorCredential { ActorId = "portal", Key = "admin-key" }]
        }), NullLogger<UserManagementKeyMiddleware>.Instance);
        DefaultHttpContext userContext = new();
        userContext.Request.Method = "GET";
        userContext.Request.Path = "/v1/users";
        userContext.Request.Headers[UserManagementKeyMiddleware.HeaderName] = "admin-key";
        await users.InvokeAsync(userContext);
        Assert.Equal(StatusCodes.Status401Unauthorized, userContext.Response.StatusCode);
        Assert.False(userManagementCalled);

    }
}
