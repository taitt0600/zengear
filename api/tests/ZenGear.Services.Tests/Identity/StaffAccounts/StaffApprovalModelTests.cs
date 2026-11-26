using Shouldly;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Tests.Identity.StaffAccounts;

/// <summary>
/// <see cref="User.CreatePendingStaff"/>, <see cref="User.Approve"/> and the PENDING backstops of
/// <see cref="User.Disable"/> and <see cref="User.Restore"/> (STF-09, R-USR-17, R-USR-03).
/// </summary>
public class StaffApprovalModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid Creator = Guid.NewGuid();

    [Test]
    public void R_USR_17_a_pending_account_has_no_firebase_account_and_keeps_its_creator()
    {
        var user = User.CreatePendingStaff(Account(firebaseUid: null), [NewRole()], Creator, Now);

        user.Status.ShouldBe(UserStatus.Pending);
        user.FirebaseUid.ShouldBeNull();
        var profile = user.StaffProfile.ShouldNotBeNull();
        profile.CreatedBy.ShouldBe(Creator);
        profile.MustChangePassword.ShouldBeTrue();
        user.CreatedBy.ShouldBe(Creator);
    }

    [Test]
    public void R_USR_17_a_pending_account_cannot_be_created_with_a_firebase_account()
    {
        Should.Throw<ArgumentException>(() => User.CreatePendingStaff(Account("uid-1"), [NewRole()], Creator, Now));
    }

    [Test]
    public void R_USR_17_an_active_account_needs_a_firebase_account_and_keeps_its_creator()
    {
        Should.Throw<ArgumentException>(() => User.CreateStaff(Account(firebaseUid: null), [NewRole()], Creator, Now));

        var user = User.CreateStaff(Account("uid-1"), [NewRole()], Creator, Now);
        user.StaffProfile.ShouldNotBeNull().CreatedBy.ShouldBe(Creator);
    }

    [Test]
    public void R_USR_03_approve_turns_a_pending_account_active_with_its_firebase_account()
    {
        var user = User.CreatePendingStaff(Account(firebaseUid: null), [NewRole()], Creator, Now);
        var approver = Guid.NewGuid();
        var later = Now.AddHours(1);

        user.Approve("uid-new", approver, later);

        user.Status.ShouldBe(UserStatus.Active);
        user.FirebaseUid.ShouldBe("uid-new");
        user.StaffProfile.ShouldNotBeNull().MustChangePassword.ShouldBeTrue();
        user.UpdatedBy.ShouldBe(approver);
        user.UpdatedAt.ShouldBe(later);
    }

    [Test]
    public void R_USR_17_only_a_pending_account_is_approved()
    {
        var active = User.CreateStaff(Account("uid-1"), [NewRole()], Creator, Now);
        var disabled = User.CreateStaff(Account("uid-2"), [NewRole()], Creator, Now);
        disabled.Disable(null, Creator, Now);

        Should.Throw<InvalidOperationException>(() => active.Approve("uid-new", Guid.NewGuid(), Now));
        Should.Throw<InvalidOperationException>(() => disabled.Approve("uid-new", Guid.NewGuid(), Now));
        active.FirebaseUid.ShouldBe("uid-1");
    }

    [Test]
    public void R_USR_17_a_pending_account_is_neither_disabled_nor_restored()
    {
        var user = User.CreatePendingStaff(Account(firebaseUid: null), [NewRole()], Creator, Now);

        Should.Throw<InvalidOperationException>(() => user.Disable(null, Guid.NewGuid(), Now));
        Should.Throw<InvalidOperationException>(() => user.Restore(Guid.NewGuid(), Now));
        user.Status.ShouldBe(UserStatus.Pending);
    }

    private static StaffAccountData Account(string? firebaseUid) =>
        new(firebaseUid, "Võ Thị Lan", "lanvtzg260006@zengear.test", "ZG260006", "LanVTZG260006",
            new DateOnly(2026, 11, 2), "0900000006", MustChangePassword: true);

    private static Role NewRole()
    {
        var role = (Role)Activator.CreateInstance(typeof(Role), nonPublic: true)!;
        typeof(Role).GetProperty(nameof(Role.Id))!.SetValue(role, Guid.NewGuid());
        typeof(Role).GetProperty(nameof(Role.ScopingType))!.SetValue(role, ScopingType.None);
        return role;
    }
}
