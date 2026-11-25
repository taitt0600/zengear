using System.ComponentModel.DataAnnotations;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Section <c>Staff</c>: the domain of the company emails that R-USR-24 generates, set per environment (DEC-145):
/// <c>zengear.test</c> in development and tests, the company's domain in production (<c>Staff__EmailDomain</c>). Not a
/// secret. Checked when the host starts.
/// </summary>
public sealed class StaffIdentityOptions
{
    public const string SectionName = "Staff";

    /// <summary>
    /// A host name: labels of letters, digits and hyphens separated by dots, with at least one dot. At most 190
    /// characters, so that the longest username (64) + "@" + the domain fits <c>users.email</c> (varchar(255)).
    /// </summary>
    [Required]
    [RegularExpression(@"^(?=.{1,190}$)([A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,63}$")]
    public string EmailDomain { get; set; } = "";
}
