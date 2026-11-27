using AdminManagement.Security;

namespace UserManagement.Service.Security;

public sealed class UserManagementRequestMiddleware(RequestDelegate next, ILogger<UserManagementRequestMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) &&
            (context.Request.Path == "/health" || context.Request.Path == "/ready"))
        {
            await next(context);
            return;
        }

        bool resolution = (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path == "/v1/users/resolve") ||
            (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/v1/users/revision");
        bool registration = HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path == "/v1/registrations/unit-organizations";

        if (registration)
            context.User = RegistrationClientIdentity.Create("unattributed-registration");

        if (!resolution && !registration && (context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(context.User.FindFirst("oid")?.Value ??
                context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value,
                out Guid objectId) || objectId == Guid.Empty))
        {
            logger.LogWarning("Administrator request lacks a validated actor; trace {TraceId}",
                context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        logger.LogDebug("User Management request accepted; trace {TraceId}", context.TraceIdentifier);
        await next(context);
    }
}
