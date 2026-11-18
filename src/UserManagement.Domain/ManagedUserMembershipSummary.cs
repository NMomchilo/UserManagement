namespace UserManagement.Domain;

public sealed record ManagedUserMembershipSummary(Guid UnitOrganizationId, string UnitOrganizationName,
    bool IsAdmin,
    IReadOnlyList<ManagedRole> Roles);
