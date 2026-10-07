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

    [Test]
    public void R_USR_04_rename_trims_and_stamps_the_editor()
    {
        var user = User.CreateStaff(Account(), [NewRole(ScopingType.None)], Actor, Now);
        var self = Guid.NewGuid();
        var later = Now.AddDays(1);

        user.Rename("  Nguyễn Văn Bình ", self, later);

        user.Name.ShouldBe("Nguyễn Văn Bình");
        user.UpdatedBy.ShouldBe(self);
        user.UpdatedAt.ShouldBe(later);
    }

    [Test]
    public void R_USR_04_rename_to_the_same_name_changes_nothing()
    {
        var user = User.CreateStaff(Account(), [NewRole(ScopingType.None)], Actor, Now);

        user.Rename(" Nguyễn Văn An", Guid.NewGuid(), Now.AddDays(1));

        user.UpdatedBy.ShouldBe(Actor);
        user.UpdatedAt.ShouldBe(Now);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void R_USR_04_rename_to_a_blank_name_throws(string name)
    {
        var user = User.CreateStaff(Account(), [NewRole(ScopingType.None)], Actor, Now);

        Should.Throw<ArgumentException>(() => user.Rename(name, Actor, Now));
        user.Name.ShouldBe("Nguyễn Văn An");
    }

    [Test]
    public void R_USR_04_password_changed_clears_must_change_password_and_stamps_the_editor()
    {
        var profile = User.CreateStaff(Account(), [NewRole(ScopingType.None)], Actor, Now).StaffProfile!;
        var self = Guid.NewGuid();
        var later = Now.AddHours(1);

        profile.PasswordChanged(self, later);

        profile.MustChangePassword.ShouldBeFalse();
        profile.UpdatedBy.ShouldBe(self);
        profile.UpdatedAt.ShouldBe(later);
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
