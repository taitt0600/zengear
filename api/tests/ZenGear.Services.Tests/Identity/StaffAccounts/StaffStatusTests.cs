using Shouldly;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Tests.Identity.StaffAccounts;

/// <summary><see cref="User.Disable"/> and <see cref="User.Restore"/> (STF-02).</summary>
public class StaffStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly JoinedDate = new(2026, 10, 1);
    private static readonly Guid Actor = Guid.NewGuid();

    [Test]
    public void R_USR_07_disable_with_left_date_sets_disabled_and_left_date_together()
    {
        var user = NewStaff();
        var admin = Guid.NewGuid();
        var later = Now.AddDays(30);

        user.Disable(new DateOnly(2026, 10, 31), admin, later);

        user.Status.ShouldBe(UserStatus.Disabled);
        user.StaffProfile!.LeftDate.ShouldBe(new DateOnly(2026, 10, 31));
        user.UpdatedBy.ShouldBe(admin);
        user.UpdatedAt.ShouldBe(later);
        user.StaffProfile.UpdatedBy.ShouldBe(admin);
        user.StaffProfile.UpdatedAt.ShouldBe(later);
    }

    [Test]
    public void R_USR_07_left_date_before_joined_date_is_refused()
    {
        var user = NewStaff();

        Should.Throw<ArgumentException>(() => user.Disable(JoinedDate.AddDays(-1), Actor, Now));
        user.Status.ShouldBe(UserStatus.Active);
        user.StaffProfile!.LeftDate.ShouldBeNull();
    }

    [Test]
    public void R_USR_07_left_date_equal_to_joined_date_is_accepted()
    {
        var user = NewStaff();

        user.Disable(JoinedDate, Actor, Now);

        user.StaffProfile!.LeftDate.ShouldBe(JoinedDate);
    }

    [Test]
    public void R_USR_03_disable_without_left_date_keeps_the_stored_one()
    {
        var user = NewStaff();
        user.Disable(new DateOnly(2026, 10, 31), Actor, Now);

        user.Disable(null, Actor, Now);

        user.Status.ShouldBe(UserStatus.Disabled);
        user.StaffProfile!.LeftDate.ShouldBe(new DateOnly(2026, 10, 31));
    }

    [Test]
    public void R_USR_03_disable_of_a_disabled_account_with_nothing_new_keeps_the_stamps()
    {
        var user = NewStaff();
        user.Disable(new DateOnly(2026, 10, 31), Actor, Now);

        user.Disable(new DateOnly(2026, 10, 31), Guid.NewGuid(), Now.AddDays(1));
        user.Disable(null, Guid.NewGuid(), Now.AddDays(1));

        user.UpdatedBy.ShouldBe(Actor);
        user.UpdatedAt.ShouldBe(Now);
        user.StaffProfile!.UpdatedBy.ShouldBe(Actor);
        user.StaffProfile.UpdatedAt.ShouldBe(Now);
    }

    [Test]
    public void R_USR_07_a_new_left_date_on_a_disabled_account_stamps_the_account_too()
    {
        var user = NewStaff();
        user.Disable(null, Actor, Now);
        var admin = Guid.NewGuid();
        var later = Now.AddDays(1);

        user.Disable(new DateOnly(2026, 11, 15), admin, later);

        user.StaffProfile!.LeftDate.ShouldBe(new DateOnly(2026, 11, 15));
        user.UpdatedBy.ShouldBe(admin);
        user.UpdatedAt.ShouldBe(later);
    }

    [Test]
    public void R_USR_03_disabled_account_restores_to_active_and_clears_left_date()
    {
        var user = NewStaff();
        user.Disable(new DateOnly(2026, 10, 31), Actor, Now);
        var admin = Guid.NewGuid();
        var later = Now.AddDays(60);

        user.Restore(admin, later);

        user.Status.ShouldBe(UserStatus.Active);
        user.StaffProfile!.LeftDate.ShouldBeNull();
        user.UpdatedBy.ShouldBe(admin);
        user.UpdatedAt.ShouldBe(later);
    }

    [Test]
    public void R_USR_03_restore_of_an_active_account_changes_nothing()
    {
        var user = NewStaff();

        user.Restore(Guid.NewGuid(), Now.AddDays(1));

        user.Status.ShouldBe(UserStatus.Active);
        user.UpdatedBy.ShouldBe(Actor);
        user.UpdatedAt.ShouldBe(Now);
    }

    [Test]
    public void R_USR_03_anonymized_account_cannot_be_disabled_or_restored()
    {
        var user = NewStaff();
        typeof(User).GetProperty(nameof(User.Status))!.SetValue(user, UserStatus.Anonymized);

        Should.Throw<InvalidOperationException>(() => user.Disable(null, Actor, Now));
        Should.Throw<InvalidOperationException>(() => user.Restore(Actor, Now));
        user.Status.ShouldBe(UserStatus.Anonymized);
    }

    [Test]
    public void Store_manager_gets_exactly_the_grants_the_owner_decided_by_default()
    {
        var manager = new PermissionCatalog().Roles.Single(r => r.RoleName == "Store Manager");

        // STF-02: staff.read and staff.disable; PRD-02: the three brand keys. Anything else is a new owner decision.
        manager.PermissionKeys.ShouldBe(
            [PermissionKeys.StaffRead, PermissionKeys.StaffDisable, PermissionKeys.BrandRead, PermissionKeys.BrandCreate,
                PermissionKeys.BrandUpdate],
            ignoreOrder: true);
        manager.IsSystemRole.ShouldBeFalse();
    }

    private static User NewStaff()
    {
        var role = (Role)Activator.CreateInstance(typeof(Role), nonPublic: true)!;
        typeof(Role).GetProperty(nameof(Role.Id))!.SetValue(role, Guid.NewGuid());
        return User.CreateStaff(
            new StaffAccountData("uid-1", "Nguyễn Văn An", "an@zengear.test", "NV0012", JoinedDate, null, MustChangePassword: false),
            [role], Actor, Now);
    }
}
