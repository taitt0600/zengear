using System.Text.Json.Serialization;

namespace ZenGear.Data.Entities.Identity;

/// <summary><c>users.status</c>: ACTIVE ↔ DISABLED; ANONYMIZED is final (R-USR-03).</summary>
public enum UserStatus
{
    [JsonStringEnumMemberName("ACTIVE")]
    Active,

    [JsonStringEnumMemberName("DISABLED")]
    Disabled,

    /// <summary>Personal data erased on request; the row is kept for order history.</summary>
    [JsonStringEnumMemberName("ANONYMIZED")]
    Anonymized,
}
