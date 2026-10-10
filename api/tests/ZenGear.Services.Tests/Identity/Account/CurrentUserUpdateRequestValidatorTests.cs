using Shouldly;
using ZenGear.Services.Identity.Account;

namespace ZenGear.Services.Tests.Identity.Account;

public class CurrentUserUpdateRequestValidatorTests
{
    private static bool IsValid(string? name) => new CurrentUserUpdateRequestValidator().Validate(new CurrentUserUpdateRequest(name)).IsValid;

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void R_USR_04_a_blank_display_name_is_refused(string? name) => IsValid(name).ShouldBeFalse();

    [Test]
    public void R_USR_04_display_name_is_at_most_100_characters_after_trimming()
    {
        IsValid(new string('a', 100)).ShouldBeTrue();
        IsValid($"  {new string('a', 100)}  ").ShouldBeTrue();
        IsValid(new string('a', 101)).ShouldBeFalse();
    }
}
