using Shouldly;
using ZenGear.Data.Entities.Identity;
using ZenGear.Services.Identity;

namespace ZenGear.Services.Tests.Identity;

public class UserAccessTests
{
    private static readonly DateTimeOffset LastLogin = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);

    [TestCase(UserStatus.Active, true)]
    [TestCase(UserStatus.Disabled, false)]
    [TestCase(UserStatus.Anonymized, false)]
    public void R_SEC_04_only_ACTIVE_user_is_active(UserStatus status, bool active)
    {
        new UserAccess(Guid.NewGuid(), status, null, false).IsActive.ShouldBe(active);
    }

    [Test]
    public void R_SEC_11_first_sign_in_ever_is_new()
    {
        new UserAccess(Guid.NewGuid(), UserStatus.Active, null, false).IsNewSignIn(LastLogin).ShouldBeTrue();
    }

    [Test]
    public void R_SEC_11_auth_time_after_last_login_is_new()
    {
        new UserAccess(Guid.NewGuid(), UserStatus.Active, LastLogin, false).IsNewSignIn(LastLogin.AddSeconds(1)).ShouldBeTrue();
    }

    [Test]
    public void R_SEC_11_same_auth_time_is_the_same_session()
    {
        new UserAccess(Guid.NewGuid(), UserStatus.Active, LastLogin, false).IsNewSignIn(LastLogin).ShouldBeFalse();
    }

    [Test]
    public void R_SEC_03_permission_granted_by_any_role_is_allowed()
    {
        var access = WithRoles(
            (ScopingType.None, ["order.confirm"]),
            (ScopingType.None, ["price.update"]));

        access.HasPermission("order.confirm").ShouldBeTrue();
        access.HasPermission("price.update").ShouldBeTrue();
        access.ScopeOf("order.confirm").ShouldBe(ScopingType.None);
    }

    [Test]
    public void R_SEC_03_permission_not_granted_has_no_scope()
    {
        var access = WithRoles((ScopingType.None, ["order.confirm"]));

        access.HasPermission("price.update").ShouldBeFalse();
        access.ScopeOf("price.update").ShouldBeNull();
    }

    [Test]
    public void R_SEC_03_user_without_roles_has_no_permission()
    {
        new UserAccess(Guid.NewGuid(), UserStatus.Active, null, false).HasPermission("order.confirm").ShouldBeFalse();
    }

    [TestCase(ScopingType.Self, ScopingType.None)]
    [TestCase(ScopingType.None, ScopingType.Self)]
    public void R_SEC_03_NONE_scope_wins_over_SELF(ScopingType first, ScopingType second)
    {
        var access = WithRoles((first, ["order.view"]), (second, ["order.view"]));

        access.ScopeOf("order.view").ShouldBe(ScopingType.None);
    }

    [Test]
    public void R_SEC_03_SELF_only_role_gives_SELF_scope()
    {
        var access = WithRoles((ScopingType.Self, ["order.view"]), (ScopingType.Self, ["order.view"]));

        access.ScopeOf("order.view").ShouldBe(ScopingType.Self);
    }

    private static UserAccess WithRoles(params (ScopingType Scope, string[] Keys)[] roles) =>
        new(Guid.NewGuid(), UserStatus.Active, null, false)
        {
            Permissions = UserAccess.MergeRoles(roles.Select(r => (r.Scope, (IEnumerable<string>)r.Keys))),
        };
}
