using Common.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UserManagement.Infrastructure.Database;

namespace UserManagement.Tests;

public sealed class EntityMetadataTests
{
    [Fact]
    public async Task UserManagement_rejects_writes_without_an_actor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<UserManagementDbContext>().UseSqlite(connection).Options;
        await using var db = new UserManagementDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var organization = new UnitOrganizationEntity { Id = Guid.NewGuid(), TenantId = "default", Name = "Unit" };
        var user = new UserEntity
        {
            Id = Guid.NewGuid(), TenantId = "default", ObjectId = Guid.NewGuid().ToString("D"),
            DisplayName = "Person", CreatedAt = DateTimeOffset.UtcNow
        };
        var membership = new UnitOrganizationMembershipEntity
        { TenantId = "default", UserId = user.Id, UnitOrganizationId = organization.Id };
        db.AddRange(organization, user, membership);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Empty(await db.Users.ToListAsync());
    }
}
