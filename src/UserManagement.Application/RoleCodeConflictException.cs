namespace UserManagement.Application;

public sealed class RoleCodeConflictException() : InvalidOperationException("Role code already exists in this unit organization.");
