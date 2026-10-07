using Microsoft.Extensions.Options;
using Shouldly;
using ZenGear.Data.Externals.Auth;

namespace ZenGear.Services.Tests.Data;

/// <summary>The default staff password is checked before any call to Firebase, so nothing is created without it.</summary>
public class FirebaseAuthProviderTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("12345")]
    public void A_missing_or_short_default_password_fails_before_any_call(string? password)
    {
        var provider = new FirebaseAuthProvider(Options.Create(new AuthProviderOptions { DefaultStaffPassword = password }));

        var error = Should.Throw<InvalidOperationException>(() =>
            provider.CreateWithDefaultStaffPasswordAsync("a@zengear.test", CancellationToken.None));

        error.Message.ShouldContain("Firebase:DefaultStaffPassword");
        if (password is { Length: > 0 })
        {
            error.Message.ShouldNotContain(password);
        }
    }

    [TestCase("Default-Staff-2026", true)]
    [TestCase("default-staff-2026", false)]
    [TestCase(" Default-Staff-2026", false)]
    [TestCase("Default-Staff-2026 ", false)]
    public void R_USR_11_is_default_staff_password_compares_exactly(string password, bool expected)
    {
        var provider = new FirebaseAuthProvider(Options.Create(new AuthProviderOptions { DefaultStaffPassword = "Default-Staff-2026" }));

        provider.IsDefaultStaffPassword(password).ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    public void R_USR_11_no_password_is_the_default_when_none_is_configured(string? configured)
    {
        var provider = new FirebaseAuthProvider(Options.Create(new AuthProviderOptions { DefaultStaffPassword = configured }));

        provider.IsDefaultStaffPassword("").ShouldBeFalse();
        provider.IsDefaultStaffPassword("anything-at-all").ShouldBeFalse();
    }

    [Test]
    public void A_password_of_six_characters_is_accepted()
    {
        FirebaseAuthProvider.DefaultStaffPassword(new AuthProviderOptions { DefaultStaffPassword = "123456" }).ShouldBe("123456");
    }
}
