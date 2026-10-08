namespace ZenGear.Services.Identity.Permissions;

/// <summary>
/// The permission keys checked by the endpoints (<c>resource.action</c>, DEC-095). A key is stored in
/// <c>permissions</c>: renaming one later drops the grants the Admin gave it (AUT-05), so treat names as permanent.
/// </summary>
public static class PermissionKeys
{
    /// <summary>Create a staff account and give it roles (STF-01).</summary>
    public const string StaffCreate = "staff.create";

    /// <summary>Read a staff account (STF-01).</summary>
    public const string StaffRead = "staff.read";

    /// <summary>Disable and restore a staff account (STF-02).</summary>
    public const string StaffDisable = "staff.disable";
}
