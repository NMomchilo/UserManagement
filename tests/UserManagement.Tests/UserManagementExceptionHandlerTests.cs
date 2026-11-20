using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UserManagement.Application;
using UserManagement.Service.Security;

namespace UserManagement.Tests;

public sealed class UserManagementExceptionHandlerTests
{
    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), 403, "Forbidden")]
    [InlineData(typeof(AdminDutyConflictException), 409, "Conflict")]
    [InlineData(typeof(ArgumentException), 400, "Invalid request")]
    public async Task KnownRequestFailures_ReturnSafeStatusAsync(Type exceptionType, int expectedStatus,
        string expectedTitle)
    {
        DefaultHttpContext context = new();
        await using MemoryStream body = new();
        context.Response.Body = body;
        UserManagementExceptionHandler sut = new(NullLogger<UserManagementExceptionHandler>.Instance);

        Assert.True(await sut.TryHandleAsync(context,
            (Exception)Activator.CreateInstance(exceptionType, "private detail")!, CancellationToken.None));
        body.Position = 0;
        string response = await new StreamReader(body).ReadToEndAsync();
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Contains(expectedTitle, response);
        Assert.DoesNotContain("private detail", response);
    }

    [Fact]
    public async Task UnexpectedStoreFailure_Returns500WithoutExceptionDetailsAsync()
    {
        DefaultHttpContext context = new();
        await using MemoryStream body = new();
        context.Response.Body = body;
        UserManagementExceptionHandler sut = new(NullLogger<UserManagementExceptionHandler>.Instance);

        bool handled = await sut.TryHandleAsync(context,
            new InvalidOperationException("sensitive database details"), CancellationToken.None);

        body.Position = 0;
        string response = await new StreamReader(body).ReadToEndAsync();
        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Contains("Service error", response);
        Assert.DoesNotContain("sensitive database details", response);
    }
}
