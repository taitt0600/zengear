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
}
