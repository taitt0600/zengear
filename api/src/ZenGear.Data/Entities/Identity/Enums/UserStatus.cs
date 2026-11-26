using System.Text.Json.Serialization;

namespace ZenGear.Data.Entities.Identity.Enums;

/// <summary>
/// <c>users.status</c>: PENDING → ACTIVE on approval, a PENDING account is deleted when rejected or withdrawn; ACTIVE ↔
/// DISABLED; ANONYMIZED is final (R-USR-03, R-USR-17).
/// </summary>
public enum UserStatus
{
    [JsonStringEnumMemberName("ACTIVE")]
    Active,

    [JsonStringEnumMemberName("DISABLED")]
    Disabled,

    /// <summary>Personal data erased on request; the row is kept for order history.</summary>
    [JsonStringEnumMemberName("ANONYMIZED")]
    Anonymized,

    /// <summary>
    /// A staff account created by someone without <c>staff.approve</c>: it has no Firebase account and cannot sign in
    /// until another person approves it (R-USR-17, DEC-128 (3)). Never a customer.
    /// </summary>
    [JsonStringEnumMemberName("PENDING")]
    Pending,
}
