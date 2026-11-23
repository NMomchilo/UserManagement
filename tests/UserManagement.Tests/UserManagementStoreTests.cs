using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UserManagement.Application;
using UserManagement.Contracts;
using UserManagement.Infrastructure.Database;

namespace UserManagement.Tests;

public sealed class UserManagementStoreTests
{
    private const string Oid = "9155ff4d-2708-4770-b87e-e238645ec5df";
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherOrganizationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task ResolveUnknownUser_DoesNotCreateUserOrMembershipAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        long revision = await service.GetRevisionAsync(default);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ResolveAsync(
            new UserIdentityRequest("default", OrganizationId, Oid), default));
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.UnitOrganizationMemberships.ToListAsync());
        Assert.Empty(await db.UnitOrganizations.ToListAsync());
        Assert.Empty(await db.Roles.ToListAsync());
        Assert.Equal(revision, await service.GetRevisionAsync(default));
    }

    [Fact]
    public async Task ResolveExistingUserWithoutMembership_DoesNotCreateMembershipOrRoleAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await service.CreateUnitOrganizationAsync(new(OrganizationId, "default", "Alpha"), default);
        Assert.True(await service.CreateUserAsync("default", Oid, "User", null, default));
        long revision = await service.GetRevisionAsync(default);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ResolveAsync(
            new UserIdentityRequest("default", OrganizationId, Oid), default));

        Assert.Empty(await db.UnitOrganizationMemberships.ToListAsync());
        Assert.Empty(await db.Roles.ToListAsync());
        Assert.Equal(revision, await service.GetRevisionAsync(default));
    }

    [Fact]
    public async Task ActiveSuperAdminResolvesActiveOrganizationWithoutMembershipAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await service.CreateUnitOrganizationAsync(new(OrganizationId, "default", "Alpha"), default);
        Assert.True(await service.CreateUserAsync("default", Oid, "SuperAdmin", null, default));
        Assert.True(await service.SetSuperAdminAsync("default", Oid, true, default));

        UserIdentityResponse resolved = await service.ResolveAsync(
            new UserIdentityRequest("default", OrganizationId, Oid), default);

        Assert.True(resolved.IsActive);
        Assert.Empty(resolved.Roles);
        Assert.Equal(OrganizationId, resolved.UnitOrganizationId);
        Assert.Empty(await db.UnitOrganizationMemberships.ToListAsync());
    }

    [Fact]
    public async Task UnitAdminDutyIsLimitedToAssignedActiveOrganizationAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await ProvisionAsync(service, db, "default", OrganizationId, "Alpha", Oid);
        await ProvisionAsync(service, db, "default", OtherOrganizationId, "Beta", Oid);
        Assert.True(await service.SetUnitAdminAsync("default", OrganizationId, Oid, true, default));

        UserIdentityResponse assigned = await service.ResolveAsync(new("default", OrganizationId, Oid), default);
        UserIdentityResponse other = await service.ResolveAsync(new("default", OtherOrganizationId, Oid), default);

        Assert.True(assigned.IsUnitAdmin);
        Assert.False(other.IsUnitAdmin);
        Assert.False(assigned.IsSuperAdmin);
        Assert.False(other.IsSuperAdmin);
    }

    [Fact]
    public async Task SuperAdminCannotResolveInactiveOrganizationAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await service.CreateUnitOrganizationAsync(new(OrganizationId, "default", "Alpha"), default);
        Assert.True(await service.CreateUserAsync("default", Oid, "SuperAdmin", null, default));
        Assert.True(await service.SetSuperAdminAsync("default", Oid, true, default));
        Assert.True(await service.DeleteUnitOrganizationAsync("default", OrganizationId, default));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ResolveAsync(
            new UserIdentityRequest("default", OrganizationId, Oid), default));
    }

    [Fact]
    public async Task AdminReads_ShowScopedAssignmentsAndFindEmailWithoutProvisioningAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await ProvisionAsync(service, db, "default", OrganizationId, "Alpha", Oid, "Alice@Example.com", "Alice");
        await ProvisionAsync(service, db, "default", OtherOrganizationId, "Beta", Oid, "Alice@Example.com", "Alice");
        await service.SetSuperAdminAsync("default", Oid, true, default);
        await service.SetUnitAdminAsync("default", OrganizationId, Oid, true, default);
        await service.ResolveAsync(new("default", OrganizationId, Oid), CancellationToken.None);
        await service.ResolveAsync(new("default", OtherOrganizationId, Oid), CancellationToken.None);
        RoleInfo extra = await service.CreateRoleAsync(new("default", OrganizationId, "Approver", "approver"), CancellationToken.None);
        await service.ReplaceRolesAsync("default", OrganizationId, Oid, new([extra.RoleId]), CancellationToken.None);
        Assert.True(await service.SetRoleActiveAsync("default", OrganizationId, extra.RoleId,
            new SetActiveRequest(false), CancellationToken.None));

        PagedResult<UserSummary> users = (await service.GetUsersPageAsync("default", null, 1, 10, CancellationToken.None))!;
        Assert.Single(users.Items);
        Assert.True(users.Items.Single().IsSuperAdmin);
        Assert.Equal(2, users.Items.Single().Memberships.Count);
        Assert.True(users.Items.Single().Memberships.Single(x => x.UnitOrganizationId == OrganizationId).IsAdmin);
        Assert.False(users.Items.Single().Memberships.Single(x => x.UnitOrganizationId == OtherOrganizationId).IsAdmin);
        Assert.Equal([extra.RoleId], users.Items.Single().Memberships.Single(x => x.UnitOrganizationId == OrganizationId)
            .Roles.Select(x => x.RoleId));
        Assert.False(users.Items.Single().Memberships.Single(x => x.UnitOrganizationId == OrganizationId)
            .Roles.Single().IsActive);
        Assert.Empty(users.Items.Single().Memberships.Single(x => x.UnitOrganizationId == OtherOrganizationId).Roles);

        PagedResult<UserSummary> matches = (await service.GetUsersPageAsync("default", OrganizationId, 1, 10,
            CancellationToken.None, "alice@example.com"))!;
        Assert.Single(matches.Items);
        Assert.Single(matches.Items.Single().Memberships);
        Assert.Empty((await service.GetUsersPageAsync("default", OrganizationId, 1, 10, CancellationToken.None,
            "missing@example.com"))!.Items);
        Assert.NotNull(await service.GetRoleAsync("default", OrganizationId, extra.RoleId, CancellationToken.None));
        Assert.Null(await service.GetRoleAsync("default", OtherOrganizationId, extra.RoleId, CancellationToken.None));

        string secondOid = Guid.NewGuid().ToString("D");
        await ProvisionAsync(service, db, "default", OrganizationId, "Alpha", secondOid, "ALICE@example.com", "Other");
        await service.ResolveAsync(new("default", OrganizationId, secondOid),
            CancellationToken.None);
        Assert.Equal(2, (await service.GetUsersPageAsync("default", OrganizationId, 1, 10,
            CancellationToken.None, "alice@example.com"))!.TotalCount);
    }

    [Fact]
    public async Task ResolveAsync_ExistingUserDoesNotChangeProfileAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await ProvisionAsync(service, db, "default", OrganizationId, "Alpha", Oid, "first@example.com", "First");

        UserIdentityResponse first = await service.ResolveAsync(new UserIdentityRequest("default", OrganizationId, Oid),
            CancellationToken.None);
        UserIdentityResponse second = await service.ResolveAsync(new UserIdentityRequest("default", OrganizationId, Oid),
            CancellationToken.None);

        Assert.Equal(first.UserId, second.UserId);
        Assert.Empty(second.Roles);
        Assert.Single(await db.Users.ToListAsync());
        Assert.Equal("first@example.com", (await db.Users.SingleAsync()).Email);
        Assert.Equal("Alpha", second.UnitOrganizationName);
    }

    [Fact]
    public async Task ReplaceRolesAsync_ExistingRoleAndNewRole_ReplacesSetAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await ProvisionAsync(service, db, "default", OrganizationId, "Alpha", Oid);
        RoleInfo reader = await service.CreateRoleAsync(new CreateRoleRequest("default", OrganizationId, "Reader", "reader"), CancellationToken.None);
        await service.ReplaceRolesAsync("default", OrganizationId, Oid, new ReplaceRolesRequest([reader.RoleId]), CancellationToken.None);
        RoleInfo admin = await service.CreateRoleAsync(new CreateRoleRequest("default", OrganizationId, "Administrator", "administrator"), CancellationToken.None);

        bool found = await service.ReplaceRolesAsync("default", OrganizationId, Oid,
            new ReplaceRolesRequest([reader.RoleId, admin.RoleId]), CancellationToken.None);
        UserIdentityResponse? updated = await service.GetAsync("default", OrganizationId, Oid, CancellationToken.None);

        Assert.True(found);
        Assert.NotNull(updated);
        Assert.Equal(["Administrator", "Reader"], updated.Roles.Select(role => role.DisplayName).ToArray());
    }

    [Fact]
    public async Task SetActiveAsync_DisabledUser_RemainsDisabledOnResolveAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await ProvisionAsync(service, db, "default", OrganizationId, "Alpha", Oid);
        await service.ResolveAsync(new UserIdentityRequest("default", OrganizationId, Oid), CancellationToken.None);

        Assert.True(await service.SetActiveAsync("default", Oid, new SetActiveRequest(false), CancellationToken.None));
        UserIdentityResponse resolved = await service.ResolveAsync(new UserIdentityRequest("default", OrganizationId, Oid),
            CancellationToken.None);

        Assert.False(resolved.IsActive);
    }

    [Fact]
    public async Task DisabledRole_IsExcludedFromResolutionAndCannotBeAssignedAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await ProvisionAsync(service, db, "default", OrganizationId, "Alpha", Oid);
        RoleInfo reader = await service.CreateRoleAsync(new("default", OrganizationId, "Reader", "reader"), CancellationToken.None);
        await service.ReplaceRolesAsync("default", OrganizationId, Oid, new([reader.RoleId]), CancellationToken.None);
        Guid roleId = reader.RoleId;
        long before = await service.GetRevisionAsync(CancellationToken.None);

        Assert.True(await service.SetRoleActiveAsync("default", OrganizationId, roleId, new SetActiveRequest(false), CancellationToken.None));
        Assert.Equal(before + 1, await service.GetRevisionAsync(CancellationToken.None));
        Assert.Empty((await service.ResolveAsync(new("default", OrganizationId, Oid), CancellationToken.None)).Roles);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceRolesAsync("default", OrganizationId, Oid,
            new ReplaceRolesRequest([roleId]), CancellationToken.None));
        Assert.True(await service.SetRoleActiveAsync("default", OrganizationId, roleId, new SetActiveRequest(true), CancellationToken.None));
        Assert.Equal(roleId, (await service.ResolveAsync(new("default", OrganizationId, Oid), CancellationToken.None)).Roles.Single().RoleId);
    }

    [Fact]
    public async Task AdministrativeCreation_LeavesRolesEmptyUntilExplicitAssignmentAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await service.CreateUnitOrganizationAsync(new(OrganizationId, "default", "Alpha"), CancellationToken.None);
        string newOid = Guid.NewGuid().ToString("D");
        Assert.True(await service.CreateUserInOrganizationAsync("default", OrganizationId, newOid,
            "New", null, CancellationToken.None));

        UserIdentityResponse created = await service.ResolveAsync(new("default", OrganizationId, newOid), CancellationToken.None);

        Assert.Empty(created.Roles);
        Assert.False(await db.UserRoles.AnyAsync(value => value.UserId == created.UserId));
        Assert.Empty(await db.Roles.ToListAsync());
        RoleInfo reader = await service.CreateRoleAsync(new("default", OrganizationId, "Reader", "reader"), default);
        Assert.True(await service.ReplaceRolesAsync("default", OrganizationId, newOid,
            new([reader.RoleId]), default));
        Assert.Equal(reader.RoleId, (await service.ResolveAsync(new("default", OrganizationId, newOid),
            default)).Roles.Single().RoleId);
    }

    [Fact]
    public async Task AdministrativeCreation_WithExistingRole_AssignsOnlyThatRoleAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await service.CreateUnitOrganizationAsync(new(OrganizationId, "default", "Alpha"), default);
        RoleInfo reader = await service.CreateRoleAsync(new("default", OrganizationId, "Reader", "reader"), default);

        Assert.True(await service.CreateUserInOrganizationAsync("default", OrganizationId, Oid,
            "User", null, default, [reader.RoleId]));

        UserIdentityResponse user = await service.ResolveAsync(new("default", OrganizationId, Oid), default);
        Assert.Equal(reader.RoleId, Assert.Single(user.Roles).RoleId);
        Assert.Single(await db.Roles.ToListAsync());
    }

    [Fact]
    public async Task AdministrativeCreation_WithForeignRole_DoesNotCreateUserAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await service.CreateUnitOrganizationAsync(new(OrganizationId, "default", "Alpha"), default);
        await service.CreateUnitOrganizationAsync(new(OtherOrganizationId, "default", "Beta"), default);
        RoleInfo foreign = await service.CreateRoleAsync(new("default", OtherOrganizationId, "Reader", "reader"), default);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateUserInOrganizationAsync(
            "default", OrganizationId, Oid, "User", null, default, [foreign.RoleId]));

        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.UnitOrganizationMemberships.ToListAsync());
        Assert.Empty(await db.UserRoles.ToListAsync());
    }

    [Fact]
    public async Task ResolveAsync_InvalidOid_RejectsWithoutWritingAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ResolveAsync(
            new UserIdentityRequest("default", OrganizationId, "not-an-oid"), CancellationToken.None));
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task SameOidAcrossOrganizations_RequiresExplicitRoleAssignmentsAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await ProvisionAsync(service, db, "default", OrganizationId, "Alpha", Oid);
        await ProvisionAsync(service, db, "default", OtherOrganizationId, "Beta", Oid);
        RoleInfo alphaRole = await service.CreateRoleAsync(new("default", OrganizationId, "Reader", "reader"), default);
        RoleInfo betaRole = await service.CreateRoleAsync(new("default", OtherOrganizationId, "Reader", "reader"), default);
        await service.ReplaceRolesAsync("default", OrganizationId, Oid, new([alphaRole.RoleId]), default);
        await service.ReplaceRolesAsync("default", OtherOrganizationId, Oid, new([betaRole.RoleId]), default);
        UserIdentityResponse first = await service.ResolveAsync(new("default", OrganizationId, Oid), CancellationToken.None);
        UserIdentityResponse second = await service.ResolveAsync(new("default", OtherOrganizationId, Oid), CancellationToken.None);
        Assert.Equal(first.UserId, second.UserId);
        Assert.NotEqual(first.Roles.Single().RoleId, second.Roles.Single().RoleId);
        Assert.Equal(2, await db.UnitOrganizationMemberships.CountAsync());
        Assert.Equal(2, await db.UserRoles.CountAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceRolesAsync("default", OrganizationId,
            Oid, new ReplaceRolesRequest([second.Roles.Single().RoleId]), CancellationToken.None));
        await service.ReplaceRolesAsync("default", OrganizationId, Oid, new ReplaceRolesRequest([]), CancellationToken.None);
        Assert.Empty((await service.GetAsync("default", OrganizationId, Oid, CancellationToken.None))!.Roles);
        Assert.Single((await service.GetAsync("default", OtherOrganizationId, Oid, CancellationToken.None))!.Roles);
    }

    [Fact]
    public async Task CrossTenantOrganizationId_IsRejectedAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await ProvisionAsync(service, db, "tenant-a", OrganizationId, "Alpha", Oid);
        await service.ResolveAsync(new("tenant-a", OrganizationId, Oid), CancellationToken.None);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ResolveAsync(
            new("tenant-b", OrganizationId, Oid), CancellationToken.None));
    }

    [Fact]
    public async Task OrganizationNamesAreUniqueButRenameCannotBeAssertedByGatewayAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await ProvisionAsync(service, db, "default", OrganizationId, "Alpha", Oid);
        await service.ResolveAsync(new("default", OrganizationId, Oid), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ResolveAsync(
            new("default", OtherOrganizationId, Oid), CancellationToken.None));
        UserIdentityResponse existing = await service.ResolveAsync(
            new("default", OrganizationId, Oid), CancellationToken.None);

        Assert.Equal("Alpha", existing.UnitOrganizationName);
        Assert.Single(await db.UnitOrganizations.ToListAsync());
    }

    [Fact]
    public async Task RoleCodeIsUniqueWithinOrganizationIncludingInactiveRolesAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await service.CreateUnitOrganizationAsync(new(OrganizationId, "default", "Alpha"), CancellationToken.None);
        await service.CreateUnitOrganizationAsync(new(OtherOrganizationId, "default", "Beta"), CancellationToken.None);
        CreateRoleRequest firstRequest = System.Text.Json.JsonSerializer.Deserialize<CreateRoleRequest>(
            $$"""{"tenantId":"default","unitOrganizationId":"{{OrganizationId}}","code":"Reviewer","displayName":"Reviewer"}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        RoleInfo first = await service.CreateRoleAsync(firstRequest, CancellationToken.None);
        Assert.Equal("reviewer", first.Code);
        await service.SetRoleActiveAsync("default", OrganizationId, first.RoleId, new(false), CancellationToken.None);
        CreateRoleRequest duplicate = System.Text.Json.JsonSerializer.Deserialize<CreateRoleRequest>(
            $$"""{"tenantId":"default","unitOrganizationId":"{{OrganizationId}}","code":"reviewer","displayName":"Another name"}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        await Assert.ThrowsAsync<RoleCodeConflictException>(() => service.CreateRoleAsync(duplicate, CancellationToken.None));
        Assert.Single(await db.Roles.ToListAsync());
        RoleInfo otherOrganization = await service.CreateRoleAsync(
            new("default", OtherOrganizationId, "Reviewer", "reviewer"), CancellationToken.None);
        Assert.Equal("reviewer", otherOrganization.Code);
    }

    [Fact]
    public async Task OrganizationRoleAdministration_EnforcesScopeAndDeletedStateAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRoleAsync(
            new("default", OrganizationId, "Admin", "admin"), CancellationToken.None));
        await service.CreateUnitOrganizationAsync(new(OrganizationId, "default", "Alpha"), CancellationToken.None);
        await service.CreateUnitOrganizationAsync(new(OtherOrganizationId, "default", "Beta"), CancellationToken.None);
        RoleInfo first = await service.CreateRoleAsync(new("default", OrganizationId, "Admin", "admin"), CancellationToken.None);
        RoleInfo second = await service.CreateRoleAsync(new("default", OtherOrganizationId, "Admin", "admin"), CancellationToken.None);
        Assert.NotEqual(first.RoleId, second.RoleId);
        Assert.Single((await service.GetRolesPageAsync("default", OrganizationId, 1, 100,
            CancellationToken.None))!.Items);
        RoleInfo duplicateName = await service.CreateRoleAsync(
            new("default", OrganizationId, "Admin", "admin-copy"), CancellationToken.None);
        Assert.NotEqual(first.RoleId, duplicateName.RoleId);

        Assert.True(await service.DeleteUnitOrganizationAsync("default", OrganizationId, CancellationToken.None));
        Assert.False((await db.UnitOrganizations.SingleAsync(value => value.Id == OrganizationId)).IsActive);
        Assert.Null(await service.GetRolesPageAsync("default", OrganizationId, 1, 100,
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRoleAsync(
            new("default", OrganizationId, "Another", "another"), CancellationToken.None));
        await Assert.ThrowsAsync<UnitOrganizationConflictException>(() => service.CreateUnitOrganizationAsync(
            new(OrganizationId, "default", "Reused"), CancellationToken.None));
    }

    [Fact]
    public async Task UnknownUserOrMembership_DoesNotAcceptAdministrativeChangesAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);

        Assert.False(await service.SetActiveAsync("default", Oid, new(false), CancellationToken.None));
        Assert.False(await service.ReplaceRolesAsync("default", OrganizationId, Oid,
            new ReplaceRolesRequest([]), CancellationToken.None));
        Assert.Null(await service.GetAsync("default", OrganizationId, Oid, CancellationToken.None));

        await ProvisionAsync(service, db, "default", OtherOrganizationId, "Beta", Oid);
        Assert.False(await service.ReplaceRolesAsync("default", OrganizationId, Oid,
            new ReplaceRolesRequest([]), CancellationToken.None));
        Assert.Null(await service.GetAsync("default", OrganizationId, Oid, CancellationToken.None));
    }

    [Fact]
    public async Task MissingRevisionRow_RollsBackOrganizationRenameAsync()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using UserManagementDbContext db = CreateDb(connection);
        await db.Database.EnsureCreatedAsync();
        UserManagementService service = CreateService(db);
        await service.CreateUnitOrganizationAsync(new(OrganizationId, "default", "Alpha"), CancellationToken.None);
        await db.Revisions.ExecuteDeleteAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateUnitOrganizationAsync(
            "default", OrganizationId, new("Changed"), CancellationToken.None));
        db.ChangeTracker.Clear();
        Assert.Equal("Alpha", (await service.GetUnitOrganizationAsync("default", OrganizationId,
            CancellationToken.None))?.Name);
    }

    private static UserManagementDbContext CreateDb(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<UserManagementDbContext>().UseSqlite(connection).Options, TestActorProvider.Instance);

    private static UserManagementService CreateService(UserManagementDbContext db) =>
        new(new SqlUserStore(db, NullLogger<SqlUserStore>.Instance),
            Options.Create(new UserManagementOptions()), NullLogger<UserManagementService>.Instance);

    private static async Task ProvisionAsync(UserManagementService service, UserManagementDbContext db,
        string tenant, Guid organizationId, string organizationName, string oid,
        string? email = null, string displayName = "User")
    {
        if (!await db.UnitOrganizations.AnyAsync(value => value.TenantId == tenant && value.Id == organizationId))
            await service.CreateUnitOrganizationAsync(new(organizationId, tenant, organizationName), default);
        if (!await db.Users.AnyAsync(value => value.TenantId == tenant && value.ObjectId == oid))
            Assert.True(await service.CreateUserInOrganizationAsync(tenant, organizationId, oid,
                displayName, email, default));
        else if (!await db.UnitOrganizationMemberships.AnyAsync(value => value.TenantId == tenant &&
                     value.UnitOrganizationId == organizationId && value.User.ObjectId == oid))
            Assert.True(await service.AssignMembershipAsync(tenant, organizationId, oid, default));
    }
}
