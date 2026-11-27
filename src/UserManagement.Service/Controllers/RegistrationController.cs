using Microsoft.AspNetCore.Mvc;
using UserManagement.Application;
using UserManagement.Contracts;

namespace UserManagement.Service.Controllers;

[ApiController]
[Route("v1/registrations/unit-organizations")]
public sealed class RegistrationController(UserManagementService service, ILogger<RegistrationController> logger)
    : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> RegisterAsync([FromBody] CreateUnitOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TenantId) || request.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Name)) return BadRequest();
        var (organization, created) = await service.RegisterUnitOrganizationAsync(request, cancellationToken);
        logger.LogInformation("Registered unit organization {UnitOrganizationId}; created {Created}; trace {TraceId}",
            organization.Id, created, HttpContext.TraceIdentifier);
        return created ? StatusCode(StatusCodes.Status201Created, organization) : Ok(organization);
    }
}
