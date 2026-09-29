using Microsoft.EntityFrameworkCore;
using Shouldly;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Services.Identity;

namespace ZenGear.Services.Tests.Identity;

/// <summary>Which tracked changes bump which version (R-SEC-07); no database, entries only.</summary>
public class AccessCacheInvalidationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();

    [TestCase(EntityState.Added)]
    [TestCase(EntityState.Deleted)]
    public void R_SEC_07_staff_profile_added_or_deleted_bumps_user(EntityState state)
    {
        using var db = NewContext();
        Track<StaffProfile>(db, state, (nameof(StaffProfile.UserId), UserId));

        Bumps(db).ShouldBe([AccessBump.User(UserId)]);
    }

    [Test]
    public void R_SEC_07_must_change_password_modified_bumps_user()
    {
        using var db = NewContext();
        var entry = Track<StaffProfile>(db, EntityState.Unchanged, (nameof(StaffProfile.UserId), UserId));
        entry.Property(nameof(StaffProfile.MustChangePassword)).CurrentValue = true;

        Bumps(db).ShouldBe([AccessBump.User(UserId)]);
    }

    [Test]
    public void R_SEC_07_other_staff_profile_change_bumps_nothing()
    {
        using var db = NewContext();
        var entry = Track<StaffProfile>(db, EntityState.Unchanged, (nameof(StaffProfile.UserId), UserId));
        entry.Property(nameof(StaffProfile.WorkPhone)).CurrentValue = "+84901234567";

        Bumps(db).ShouldBeEmpty();
    }

    [TestCase(nameof(User.Status), UserStatus.Disabled)]
    [TestCase(nameof(User.FirebaseUid), "new-uid")]
    public void R_SEC_07_user_status_or_uid_modified_bumps_user(string property, object value)
    {
        using var db = NewContext();
        var entry = Track<User>(db, EntityState.Unchanged, (nameof(User.Id), UserId));
        entry.Property(property).CurrentValue = value;

        Bumps(db).ShouldBe([AccessBump.User(UserId)]);
    }

    [Test]
    public void R_SEC_07_user_name_modified_bumps_nothing()
    {
        using var db = NewContext();
        var entry = Track<User>(db, EntityState.Unchanged, (nameof(User.Id), UserId));
        entry.Property(nameof(User.Name)).CurrentValue = "Trần Thị B";

        Bumps(db).ShouldBeEmpty();
    }

    [TestCase(EntityState.Added)]
    [TestCase(EntityState.Deleted)]
    public void R_SEC_07_user_role_added_or_deleted_bumps_user(EntityState state)
    {
        using var db = NewContext();
        Track<UserRole>(db, state, (nameof(UserRole.UserId), UserId), (nameof(UserRole.RoleId), RoleId));

        Bumps(db).ShouldBe([AccessBump.User(UserId)]);
    }

    [TestCase(EntityState.Added)]
    [TestCase(EntityState.Deleted)]
    public void R_SEC_07_role_permission_added_or_deleted_bumps_role(EntityState state)
    {
        using var db = NewContext();
        Track<RolePermission>(db, state, (nameof(RolePermission.RoleId), RoleId), (nameof(RolePermission.PermissionId), Guid.NewGuid()));

        Bumps(db).ShouldBe([AccessBump.Role(RoleId)]);
    }

    [Test]
    public void R_SEC_07_role_scope_modified_bumps_role()
    {
        using var db = NewContext();
        var entry = Track<Role>(db, EntityState.Unchanged, (nameof(Role.Id), RoleId));
        entry.Property(nameof(Role.ScopingType)).CurrentValue = ScopingType.Self;

        Bumps(db).ShouldBe([AccessBump.Role(RoleId)]);
    }

    private static List<AccessBump> Bumps(ZenGearDbContext db) =>
        AccessCacheInvalidationInterceptor.CollectBumps(db.ChangeTracker).ToList();

    private static Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Track<T>(
        ZenGearDbContext db, EntityState state, params (string Name, object Value)[] keys) where T : class
    {
        var entity = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
        var entry = db.Entry(entity);
        foreach (var (name, value) in keys)
        {
            entry.Property(name).CurrentValue = value;
        }

        entry.State = state;
        return entry;
    }

    private static ZenGearDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ZenGearDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options);
}
