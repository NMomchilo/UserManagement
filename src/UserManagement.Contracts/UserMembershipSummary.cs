namespace UserManagement.Contracts;

public sealed record UserMembershipSummary(Guid UnitOrganizationId, string UnitOrganizationName,
    bool IsAdmin,
    IReadOnlyList<UserRoleAssignmentInfo> Roles);

public sealed record UserRoleAssignmentInfo(Guid RoleId, string Code, string DisplayName, bool IsActive);
