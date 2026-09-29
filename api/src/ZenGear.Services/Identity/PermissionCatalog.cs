namespace ZenGear.Services.Identity;

/// <summary>
/// The permissions of ZenGear and their grants (DEC-095). Each feature that protects an endpoint adds
/// its keys here and lists them for the roles that get them by default. Access checks never use role
/// names (R-SEC-03); the names below only tell the sync which rows of <c>roles</c> to update.
/// </summary>
public sealed class PermissionCatalog : IPermissionCatalog
{
    private static readonly PermissionDefinition[] Definitions = [];

    public IReadOnlyList<PermissionDefinition> Permissions => Definitions;

    public IReadOnlyList<RoleGrants> Roles { get; } =
    [
        new("Admin", IsSystemRole: true, [.. Definitions.Select(p => p.Key)]),
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
