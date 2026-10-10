using Moq;
using Shouldly;
using ZenGear.Data.Externals.Auth;
using ZenGear.Services.Identity.Account;

namespace ZenGear.Services.Tests.Identity.Account;

/// <summary>The staff password policy (R-USR-11, DEC-102) and its boundaries.</summary>
public class CurrentUserPasswordChangeRequestValidatorTests
{
    private const string DefaultPassword = "Default-Staff-2026";

    private static CurrentUserPasswordChangeRequestValidator Validator()
    {
        var auth = new Mock<IAuthProvider>(MockBehavior.Strict);
        auth.Setup(a => a.IsDefaultStaffPassword(It.IsAny<string>()))
            .Returns((string p) => string.Equals(p, DefaultPassword, StringComparison.Ordinal));
        return new CurrentUserPasswordChangeRequestValidator(auth.Object);
    }

    private static bool IsValid(string? password) => Validator().Validate(new CurrentUserPasswordChangeRequest(password)).IsValid;

    [TestCase(11, false)]
    [TestCase(12, true)]
    [TestCase(128, true)]
    [TestCase(129, false)]
    public void R_USR_11_length_is_12_to_128_characters(int length, bool valid) =>
        IsValid(Password(length)).ShouldBe(valid);

    [TestCase(11, false)]
    [TestCase(12, true)]
    public void R_USR_11_length_counts_code_points_not_utf16_units(int emoji, bool valid) =>
        // Each 🔒 is one code point but two UTF-16 units: 11 of them are 22 units and still too short.
        IsValid(string.Concat(Enumerable.Repeat("🔒", emoji))).ShouldBe(valid);

    [TestCase(null)]
    [TestCase("")]
    public void R_USR_11_a_missing_password_is_refused(string? password) => IsValid(password).ShouldBeFalse();

    [Test]
    public void R_USR_11_the_default_staff_password_is_refused() => IsValid(DefaultPassword).ShouldBeFalse();

    [Test]
    public void R_USR_11_the_default_staff_password_in_another_case_is_not_the_default() =>
        IsValid(DefaultPassword.ToUpperInvariant()).ShouldBeTrue();

    [TestCase("PASSWORD1234")]
    [TestCase("QwErTyUiOp12")]
    [TestCase("MatKhau12345")]
    public void R_USR_11_a_blocklisted_password_is_refused_in_any_case(string password) => IsValid(password).ShouldBeFalse();

    [TestCase("MyZenGearShop!2026")]
    [TestCase("zengear-cua-toi")]
    [TestCase("abcZENGEARxyz")]
    public void R_USR_11_a_password_containing_zengear_is_refused(string password) => IsValid(password).ShouldBeFalse();

    [Test]
    public void R_USR_11_spaces_are_kept_and_counted() =>
        // 10 characters and two spaces: valid only because the spaces are not trimmed.
        IsValid(" banh-mi-25 ").ShouldBeTrue();

    [Test]
    public void R_USR_11_no_composition_rule() => IsValid("ngaymaitroinang").ShouldBeTrue();

    [TestCase(null)]
    [TestCase("")]
    [TestCase("short-pass1")]
    [TestCase(DefaultPassword)]
    [TestCase("password1234")]
    [TestCase("MyZenGearShop!2026")]
    public void R_USR_11_every_failure_names_the_rule_and_never_the_password(string? password)
    {
        var result = Validator().Validate(new CurrentUserPasswordChangeRequest(password));

        result.Errors.ShouldNotBeEmpty();
        result.Errors.ShouldAllBe(e => e.ErrorCode == "R-USR-11" && e.PropertyName == nameof(CurrentUserPasswordChangeRequest.NewPassword));
        if (!string.IsNullOrEmpty(password))
        {
            result.Errors.ShouldAllBe(e => !e.ErrorMessage.Contains(password));
        }
    }

    /// <summary>"tra-sua-tran-chau-" repeated and cut: on no blocklist.</summary>
    private static string Password(int length) =>
        string.Concat(Enumerable.Range(0, length).Select(i => "tra-sua-tran-chau-"[i % 18]));
}
