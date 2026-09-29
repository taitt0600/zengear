using System.Text.Json.Serialization;

namespace ZenGear.Data.Entities.Identity;

/// <summary><c>roles.scoping_type</c>: which rows a permission of the role reaches (R-SEC-03, DEC-002).</summary>
public enum ScopingType
{
    /// <summary>All data (staff roles).</summary>
    [JsonStringEnumMemberName("NONE")]
    None,

    /// <summary>Only the caller's own data (Customer).</summary>
    [JsonStringEnumMemberName("SELF")]
    Self,
}
