namespace ZenGear.Services.Identity.Permissions;

/// <summary>
/// The permissions of ZenGear and their grants (DEC-095). Each feature that protects an endpoint adds
/// its keys here and lists them for the roles that get them by default. Access checks never use role
/// names (R-SEC-03); the names below only tell the sync which rows of <c>roles</c> to update.
/// </summary>
public sealed class PermissionCatalog : IPermissionCatalog
{
    /// <summary>
    /// The Admin role, found by name like the R-SEC-02 trigger does (<c>roles.name = 'Admin'</c>). Besides the sync,
    /// only R-TX-11 uses it, and only to refuse: holding it never grants access (R-SEC-03).
    /// </summary>
    public const string AdminRoleName = "Admin";

    private static readonly PermissionDefinition[] Definitions =
    [
        new(PermissionKeys.StaffCreate, "Create staff accounts and give them roles"),
        new(PermissionKeys.StaffRead, "View staff accounts"),
    ];

    public IReadOnlyList<PermissionDefinition> Permissions => Definitions;

    public IReadOnlyList<RoleGrants> Roles { get; } =
    [
        new(AdminRoleName, IsSystemRole: true, [.. Definitions.Select(p => p.Key)]),
        new("Customer", IsSystemRole: true, []),
        new("Store Manager", IsSystemRole: false, []),
        new("Sales Consultant", IsSystemRole: false, []),
        new("Technical Support", IsSystemRole: false, []),
        new("Cashier", IsSystemRole: false, []),
        new("Warehouse Keeper", IsSystemRole: false, []),
        new("Purchasing", IsSystemRole: false, []),
        new("Accountant", IsSystemRole: false, []),
    ];
}
