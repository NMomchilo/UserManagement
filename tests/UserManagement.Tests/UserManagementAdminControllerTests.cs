using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UserManagement.Application;
using UserManagement.Contracts;
using UserManagement.Infrastructure.Database;
using UserManagement.Service.Controllers;

namespace UserManagement.Tests;

public sealed class UserManagementAdminControllerTests
{
    private const string SuperOid = "11111111-1111-1111-1111-111111111111";
    private const string AdminOid = "22222222-2222-2222-2222-222222222222";
    private const string MemberOid = "33333333-3333-3333-3333-333333333333";
    private static readonly Guid Alpha = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Beta = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task AccessLookupUsesSignedOidAndMatchingTenantAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await GrantSuperAdminAsync(fixture.Service);
        var controller = new AdminAccessController(fixture.Service);
        SetActor(controller, SuperOid);

        var result = Assert.IsType<OkObjectResult>(await controller.GetAsync("tenant", Alpha, default));
        Assert.True(Assert.IsType<AdminAccessInfo>(result.Value).CanManage);
        var otherTenant = Assert.IsType<OkObjectResult>(await controller.GetAsync("other", Alpha, default));
        Assert.False(Assert.IsType<AdminAccessInfo>(otherTenant.Value).CanManage);
    }

    [Fact]
    public async Task SuperAdminCanCreateAndManageUserMembershipsAndDutiesAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await GrantSuperAdminAsync(fixture.Service);
        await fixture.Service.CreateUnitOrganizationAsync(new(Alpha, "tenant", "Alpha"), default);
        await fixture.Service.CreateUnitOrganizationAsync(new(Beta, "tenant", "Beta"), default);
        var controller = new UsersController(fixture.Service, fixture.Authorizer, NullLogger<UsersController>.Instance);
        SetActor(controller, SuperOid);

        Assert.IsType<CreatedResult>(await controller.CreateUserAsync(MemberOid, "tenant", Alpha,
            new CreateUserRequest("Member", "member@example.com"), default));
        Assert.Empty((await fixture.Service.GetAsync("tenant", Alpha, MemberOid, default))!.Roles);
        Assert.IsType<NoContentResult>(await controller.AssignMembershipAsync(MemberOid, Beta, "tenant", default));
        Assert.IsType<NoContentResult>(await controller.SetUnitAdminAsync(MemberOid, Beta, "tenant", new(true), default));
        Assert.Equal([Beta], (await fixture.Service.GetAdminAccessAsync("tenant", MemberOid, null, default)).ManagedUnitOrganizationIds);
        Assert.IsType<NoContentResult>(await controller.RevokeMembershipAsync(MemberOid, Alpha, "tenant", default));
        Assert.Null(await fixture.Service.GetAsync("tenant", Alpha, MemberOid, default));
        Assert.NotNull(await fixture.Service.GetAsync("tenant", Beta, MemberOid, default));
        Assert.IsType<NoContentResult>(await controller.UpdateUserAsync(MemberOid, "tenant", Beta,
            new UserProfileRequest("Changed", null), default));
        Assert.IsType<NoContentResult>(await controller.SetSuperAdminAsync(MemberOid, "tenant", new(true), default));
        Assert.IsType<NoContentResult>(await controller.SetActiveAsync(MemberOid, "tenant", new(false), default));
        Assert.False((await fixture.Service.GetAdminAccessAsync("tenant", MemberOid, null, default)).IsActive);
    }

    [Fact]
    public async Task SuperAdminCanCreateUserWithoutOrganizationThenAssignMembershipAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await GrantSuperAdminAsync(fixture.Service);
        await fixture.Service.CreateUnitOrganizationAsync(new(Alpha, "tenant", "Alpha"), default);
        var controller = new UsersController(fixture.Service, fixture.Authorizer, NullLogger<UsersController>.Instance);
        SetActor(controller, SuperOid);

        var created = Assert.IsType<StatusCodeResult>(await controller.CreateUserAsync(MemberOid, "tenant", null,
            new CreateUserRequest("Member", "member@example.com"), default));
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        PagedResult<UserSummary> users = Assert.IsType<PagedResult<UserSummary>>(
            Assert.IsType<OkObjectResult>(await controller.GetAllAsync("tenant", cancellationToken: default)).Value);
        Assert.Empty(Assert.Single(users.Items, user => user.ObjectId == MemberOid).Memberships);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Service.ResolveAsync(
            new UserIdentityRequest("tenant", Alpha, MemberOid), default));

        Assert.IsType<NoContentResult>(await controller.AssignMembershipAsync(MemberOid, Alpha, "tenant", default));
        Assert.Empty((await fixture.Service.ResolveAsync(new UserIdentityRequest("tenant", Alpha, MemberOid), default)).Roles);
    }

    [Fact]
    public async Task AdministratorUserReadsShowTenantAndOrganizationDutiesAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await GrantSuperAdminAsync(fixture.Service);
        await fixture.Service.CreateUnitOrganizationAsync(new(Alpha, "tenant", "Alpha"), default);
        await fixture.Service.CreateUnitOrganizationAsync(new(Beta, "tenant", "Beta"), default);
        await fixture.Service.CreateUserAsync("tenant", MemberOid, "Member", null, default);
        await fixture.Service.AssignMembershipAsync("tenant", Alpha, MemberOid, default);
        await fixture.Service.AssignMembershipAsync("tenant", Beta, MemberOid, default);
        await fixture.Service.SetUnitAdminAsync("tenant", Alpha, MemberOid, true, default);
        var controller = new UsersController(fixture.Service, fixture.Authorizer, NullLogger<UsersController>.Instance);
        SetActor(controller, SuperOid);

        UserSummary all = Assert.IsType<UserSummary>(Assert.IsType<OkObjectResult>(
            await controller.GetAsync(MemberOid, "tenant", null, default)).Value);
        Assert.False(all.IsSuperAdmin);
        Assert.True(Assert.Single(all.Memberships, membership => membership.UnitOrganizationId == Alpha).IsAdmin);
        Assert.False(Assert.Single(all.Memberships, membership => membership.UnitOrganizationId == Beta).IsAdmin);

        UserSummary scoped = Assert.IsType<UserSummary>(Assert.IsType<OkObjectResult>(
            await controller.GetAsync(MemberOid, "tenant", Beta, default)).Value);
        Assert.Single(scoped.Memberships);
        Assert.False(scoped.Memberships[0].IsAdmin);

        Assert.IsType<NoContentResult>(await controller.SetSuperAdminAsync(MemberOid, "tenant", new(true), default));
        Assert.True(Assert.IsType<UserSummary>(Assert.IsType<OkObjectResult>(
            await controller.GetAsync(MemberOid, "tenant", null, default)).Value).IsSuperAdmin);

        Assert.IsType<NoContentResult>(await controller.SetActiveAsync(MemberOid, "tenant", new(false), default));
        UserSummary inactive = Assert.IsType<UserSummary>(Assert.IsType<OkObjectResult>(
            await controller.GetAsync(MemberOid, "tenant", null, default)).Value);
        Assert.False(inactive.IsActive);
        Assert.True(inactive.IsSuperAdmin);
        Assert.False((await fixture.Service.GetAdminAccessAsync("tenant", MemberOid, Alpha, default)).CanManage);

        Assert.IsType<NoContentResult>(await controller.RevokeMembershipAsync(MemberOid, Alpha, "tenant", default));
        Assert.IsType<NotFoundResult>(await controller.GetAsync(MemberOid, "tenant", Alpha, default));
        Assert.Single(Assert.IsType<UserSummary>(Assert.IsType<OkObjectResult>(
            await controller.GetAsync(MemberOid, "tenant", null, default)).Value).Memberships);
    }

    [Fact]
    public async Task UnitAdminCannotReadUnscopedUserAndSuperAdminCanReadTenantOnlyUserAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await GrantSuperAdminAsync(fixture.Service);
        await fixture.Service.CreateUnitOrganizationAsync(new(Alpha, "tenant", "Alpha"), default);
        await fixture.Service.CreateUserAsync("tenant", AdminOid, "Admin", null, default);
        await fixture.Service.AssignMembershipAsync("tenant", Alpha, AdminOid, default);
        await fixture.Service.SetUnitAdminAsync("tenant", Alpha, AdminOid, true, default);
        await fixture.Service.CreateUserAsync("tenant", MemberOid, "Unassigned", null, default);
        var controller = new UsersController(fixture.Service, fixture.Authorizer, NullLogger<UsersController>.Instance);
        SetActor(controller, AdminOid);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.GetAsync(MemberOid, "tenant", null, default));
        Assert.IsType<NotFoundResult>(await controller.GetAsync(MemberOid, "tenant", Alpha, default));

        SetActor(controller, SuperOid);
        UserSummary unassigned = Assert.IsType<UserSummary>(Assert.IsType<OkObjectResult>(
            await controller.GetAsync(MemberOid, "tenant", null, default)).Value);
        Assert.Empty(unassigned.Memberships);
        Assert.False(unassigned.IsSuperAdmin);
    }

    [Fact]
    public async Task CreatingUserWithoutOrganizationRejectsRoleIdsAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await GrantSuperAdminAsync(fixture.Service);
        var controller = new UsersController(fixture.Service, fixture.Authorizer, NullLogger<UsersController>.Instance);
        SetActor(controller, SuperOid);

        await Assert.ThrowsAsync<ArgumentException>(() => controller.CreateUserAsync(MemberOid, "tenant", null,
            new CreateUserRequest("Member", null, [Guid.NewGuid()]), default));
        PagedResult<UserSummary> users = Assert.IsType<PagedResult<UserSummary>>(
            Assert.IsType<OkObjectResult>(await controller.GetAllAsync("tenant", cancellationToken: default)).Value);
        Assert.DoesNotContain(users.Items, user => user.ObjectId == MemberOid);
    }

    [Fact]
    public async Task UnitAdminCannotCreateUserWithoutOrganizationAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await GrantSuperAdminAsync(fixture.Service);
        await fixture.Service.CreateUnitOrganizationAsync(new(Alpha, "tenant", "Alpha"), default);
        await fixture.Service.CreateUserAsync("tenant", AdminOid, "Admin", null, default);
        await fixture.Service.AssignMembershipAsync("tenant", Alpha, AdminOid, default);
        await fixture.Service.SetUnitAdminAsync("tenant", Alpha, AdminOid, true, default);
        var controller = new UsersController(fixture.Service, fixture.Authorizer, NullLogger<UsersController>.Instance);
        SetActor(controller, AdminOid);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.CreateUserAsync(MemberOid, "tenant", null,
            new CreateUserRequest("Member", null), default));
    }

    [Fact]
    public async Task ScopedAdminCanManageOwnRoleAndMemberProfileButNotAnotherUnitAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await GrantSuperAdminAsync(fixture.Service);
        await fixture.Service.CreateUnitOrganizationAsync(new(Alpha, "tenant", "Alpha"), default);
        await fixture.Service.CreateUnitOrganizationAsync(new(Beta, "tenant", "Beta"), default);
        await fixture.Service.CreateUserAsync("tenant", AdminOid, "Admin", null, default);
        await fixture.Service.AssignMembershipAsync("tenant", Alpha, AdminOid, default);
        await fixture.Service.SetUnitAdminAsync("tenant", Alpha, AdminOid, true, default);
        var roles = new RolesController(fixture.Service, fixture.Authorizer, NullLogger<RolesController>.Instance);
        SetActor(roles, AdminOid);

        var created = Assert.IsType<CreatedResult>(await roles.CreateAsync(
            new CreateRoleRequest("tenant", Alpha, "Editor", "editor"), default));
        var role = Assert.IsType<RoleInfo>(created.Value);
        Assert.IsType<OkObjectResult>(await roles.UpdateAsync(role.RoleId, "tenant", Alpha,
            new UpdateRoleRequest("Senior Editor"), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => roles.CreateAsync(
            new CreateRoleRequest("tenant", Beta, "Editor", "editor"), default));
        Assert.IsType<NoContentResult>(await roles.SetActiveAsync(role.RoleId, "tenant", Alpha, new(false), default));
        var users = new UsersController(fixture.Service, fixture.Authorizer, NullLogger<UsersController>.Instance);
        SetActor(users, AdminOid);
        Assert.IsType<NoContentResult>(await users.UpdateUserAsync(AdminOid, "tenant", Alpha,
            new UserProfileRequest("Changed", null), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => users.UpdateUserAsync(AdminOid, "tenant", Beta,
            new UserProfileRequest("Wrong unit", null), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => users.SetActiveAsync(AdminOid, "tenant", new(false), default));
    }

    [Fact]
    public async Task UnitListShowsScopedAdministratorOnlyManagedUnitsAsync()
    {
        await using var fixture = await Fixture.CreateAsync();
        await GrantSuperAdminAsync(fixture.Service);
        await fixture.Service.CreateUnitOrganizationAsync(new(Alpha, "tenant", "Alpha"), default);
        await fixture.Service.CreateUnitOrganizationAsync(new(Beta, "tenant", "Beta"), default);
        await fixture.Service.CreateUserAsync("tenant", AdminOid, "Admin", null, default);
        await fixture.Service.AssignMembershipAsync("tenant", Alpha, AdminOid, default);
        await fixture.Service.SetUnitAdminAsync("tenant", Alpha, AdminOid, true, default);
        var controller = new UnitOrganizationsController(fixture.Service, fixture.Authorizer,
            NullLogger<UnitOrganizationsController>.Instance);
        SetActor(controller, AdminOid);

        var result = Assert.IsType<OkObjectResult>(await controller.GetAllAsync("tenant", default));
        Assert.Equal([Alpha], Assert.IsType<UnitOrganizationInfo[]>(result.Value).Select(value => value.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.DeleteAsync(Beta, "tenant", default));
    }

    private static void SetActor(ControllerBase controller, string oid) => controller.ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("oid", oid), new Claim("application_tenant_id", "tenant")], "jwt"))
        }
    };

    private static async Task GrantSuperAdminAsync(UserManagementService service)
    {
        await service.CreateUserAsync("tenant", SuperOid, "Super", null, default);
        await service.SetSuperAdminAsync("tenant", SuperOid, true, default);
    }

    private sealed class Fixture(SqliteConnection connection, UserManagementDbContext db) : IAsyncDisposable
    {
        public UserManagementService Service { get; } = new(new SqlUserStore(db, NullLogger<SqlUserStore>.Instance),
            Options.Create(new UserManagementOptions()), NullLogger<UserManagementService>.Instance);
        public IUserManagementAdminAuthorizer Authorizer => new UserManagementAdminAuthorizer(Service);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new UserManagementDbContext(new DbContextOptionsBuilder<UserManagementDbContext>()
                .UseSqlite(connection).Options, TestActorProvider.Instance);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
