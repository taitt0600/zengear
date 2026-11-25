using System.ComponentModel.DataAnnotations;
using Shouldly;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Services.Tests.Identity.StaffAccounts;

/// <summary>The email domain of R-USR-24, checked when the host starts.</summary>
public class StaffIdentityOptionsTests
{
    [TestCase("zengear.test")]
    [TestCase("mail.zengear.vn")]
    public void R_USR_24_accepts_a_host_name(string domain) => IsValid(domain).ShouldBeTrue();

    [TestCase("")]
    [TestCase("localhost")]
    [TestCase("zengear..test")]
    [TestCase("-zengear.test")]
    [TestCase("zengear.test.")]
    public void R_USR_24_refuses_what_is_not_a_host_name(string domain) => IsValid(domain).ShouldBeFalse();

    [Test]
    public void R_USR_24_the_longest_username_and_the_domain_fit_the_email_column()
    {
        // 64 (username) + "@" + 190 = 255, the length of users.email.
        IsValid(DomainOfLength(190)).ShouldBeTrue();
        IsValid(DomainOfLength(191)).ShouldBeFalse();
    }

    /// <summary>Labels of 60 letters and a shorter last one, then ".vn": a valid host name of <paramref name="length"/>.</summary>
    private static string DomainOfLength(int length)
    {
        var builder = new System.Text.StringBuilder();
        var remaining = length - ".vn".Length;
        while (remaining > 61)
        {
            builder.Append(new string('a', 60)).Append('.');
            remaining -= 61;
        }

        return builder.Append(new string('a', remaining)).Append(".vn").ToString();
    }

    private static bool IsValid(string domain)
    {
        var options = new StaffIdentityOptions { EmailDomain = domain };
        return Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);
    }
}
