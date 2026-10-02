using Shouldly;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Tests.Identity;

/// <summary><see cref="User.CreateStaff"/>: the only way to create a staff account.</summary>
public class StaffAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    [Test]
    public void R_USR_01_create_staff_gives_the_user_exactly_one_staff_profile()
    {
        var user = User.CreateStaff(Account(), [NewRole(ScopingType.None)], Actor, Now);

        var profile = user.StaffProfile.ShouldNotBeNull();
        profile.EmployeeCode.ShouldBe("NV0012");
        profile.JoinedDate.ShouldBe(new DateOnly(2026, 10, 1));
        profile.WorkPhone.ShouldBe("0901234567");
        user.Status.ShouldBe(UserStatus.Active);
        user.FirebaseUid.ShouldBe("uid-1");
        user.Name.ShouldBe("Nguyễn Văn An");
        user.CreatedBy.ShouldBe(Actor);
        user.CreatedAt.ShouldBe(Now);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Create_staff_carries_the_must_change_password_flag(bool mustChange)
    {
        var user = User.CreateStaff(Account() with { MustChangePassword = mustChange }, [NewRole(ScopingType.None)], Actor, Now);

        user.StaffProfile!.MustChangePassword.ShouldBe(mustChange);
    }

    [Test]
    public void R_USR_01_a_staff_user_cannot_hold_a_self_scoped_role()
    {
        Should.Throw<ArgumentException>(() =>
            User.CreateStaff(Account(), [NewRole(ScopingType.None), NewRole(ScopingType.Self)], Actor, Now));
    }

    [Test]
    public void R_SEC_01_create_staff_refuses_an_empty_role_list()
    {
        Should.Throw<ArgumentException>(() => User.CreateStaff(Account(), [], Actor, Now));
    }

    [Test]
    public void R_USR_02_create_staff_trims_and_lowercases_the_email()
    {
        var user = User.CreateStaff(Account() with { Email = "  An.Nguyen@ZenGear.TEST " }, [NewRole(ScopingType.None)], Actor, Now);

        user.Email.ShouldBe("an.nguyen@zengear.test");
    }

    [Test]
    public void Create_staff_gives_each_role_once()
    {
        var cashier = NewRole(ScopingType.None);
        var sales = NewRole(ScopingType.None);

        var user = User.CreateStaff(Account(), [cashier, sales, cashier], Actor, Now);

        user.UserRoles.Select(ur => ur.RoleId).ShouldBe([cashier.Id, sales.Id], ignoreOrder: true);
        user.UserRoles.ShouldAllBe(ur => ur.CreatedBy == Actor && ur.CreatedAt == Now);
    }

    private static StaffAccountData Account() =>
        new("uid-1", " Nguyễn Văn An ", "an@zengear.test", "NV0012", new DateOnly(2026, 10, 1), "0901234567", MustChangePassword: true);

    /// <summary>Roles are created by the seed and AUT-05, not by the code under test.</summary>
    private static Role NewRole(ScopingType scope)
    {
        var role = (Role)Activator.CreateInstance(typeof(Role), nonPublic: true)!;
        typeof(Role).GetProperty(nameof(Role.Id))!.SetValue(role, Guid.NewGuid());
        typeof(Role).GetProperty(nameof(Role.ScopingType))!.SetValue(role, scope);
        return role;
    }
}
