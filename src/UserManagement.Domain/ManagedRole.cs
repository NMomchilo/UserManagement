namespace UserManagement.Domain;

public sealed record ManagedRole(Guid Id, string TenantId, Guid UnitOrganizationId, string DisplayName,
    bool IsActive, string Code);
