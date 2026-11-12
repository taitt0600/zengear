namespace ZenGear.Services.Identity.Permissions;

/// <summary>
/// The permissions of ZenGear and their grants (DEC-095). Each feature that protects an endpoint adds
/// its keys here and lists them for the roles that get them by default. Access checks never use role
/// names (R-SEC-03); the names below only tell the sync which rows of <c>roles</c> to update.
/// </summary>
public sealed class PermissionCatalog : IPermissionCatalog
{
    /// <summary>
    /// The Admin role, found by name like the R-SEC-02 trigger does (<c>roles.name = 'Admin'</c>). Besides the sync, R-TX-11
    /// uses it to refuse, and R-SEC-12 gives its holders every role to manage (the rule says so); it never grants a
    /// permission (R-SEC-03).
    /// </summary>
    public const string AdminRoleName = "Admin";

    private const string StoreManager = "Store Manager";
    private const string SalesConsultant = "Sales Consultant";
    private const string TechnicalSupport = "Technical Support";
    private const string Cashier = "Cashier";
    private const string WarehouseKeeper = "Warehouse Keeper";
    private const string Purchasing = "Purchasing";
    private const string Accountant = "Accountant";
    private const string ItSupport = "IT Support";

    /// <summary>Keys only a system role holds (AUT-05, owner's decision): see <see cref="IsAdminOnly"/>.</summary>
    private static readonly HashSet<string> AdminOnlyKeys =
        [PermissionKeys.RoleRead, PermissionKeys.RoleCreate, PermissionKeys.RoleUpdate, PermissionKeys.RoleDelete];

    private static readonly PermissionDefinition[] Definitions =
    [
        new(PermissionKeys.StaffCreate, "Create staff accounts and give them roles"),
        new(PermissionKeys.StaffRead, "View staff accounts"),
        new(PermissionKeys.StaffDisable, "Disable and restore staff accounts"),
        new(PermissionKeys.BusinessLocationUpdate, "Edit the business location: address, hotline, opening hours"),
        new(PermissionKeys.BrandRead, "View brands"),
        new(PermissionKeys.BrandCreate, "Create brands"),
        new(PermissionKeys.BrandUpdate, "Rename, retire and restore brands"),
        new(PermissionKeys.ProductSeriesRead, "View product series"),
        new(PermissionKeys.ProductSeriesCreate, "Create product series"),
        new(PermissionKeys.ProductSeriesUpdate, "Rename, move, retire and restore product series"),
        new(PermissionKeys.RoleRead, "View roles, their permissions and manageable roles"),
        new(PermissionKeys.RoleCreate, "Create roles"),
        new(PermissionKeys.RoleUpdate, "Change the permissions, discount and manageable roles of roles"),
        new(PermissionKeys.RoleDelete, "Delete roles no account holds"),
    ];

    public IReadOnlyList<PermissionDefinition> Permissions => Definitions;

    /// <summary>
    /// A key the API never grants to a role other than a system role (owner's decision for AUT-05, "Chỉ Admin được thao
    /// tác"): with it, the holder could raise their own role's permissions. Checked by key, never by role name (R-SEC-03).
    /// </summary>
    public static bool IsAdminOnly(string key) => AdminOnlyKeys.Contains(key);

    public IReadOnlyList<RoleGrants> Roles { get; } =
    [
        // LOC-01, the owner's decision: business_location.update stays with the Admin only.
        new(AdminRoleName, IsSystemRole: true, [.. Definitions.Select(p => p.Key)]),
        new("Customer", IsSystemRole: true, []),
        // STF-02, the owner's decision: a Store Manager disables and restores staff, and reads the accounts it changes.
        // R-TX-11 still refuses the Store Manager disabling an Admin account.
        // PRD-02, the owner's decision: a Store Manager manages the brands (A02, DEC-013).
        // PRD-03, the owner's decision: a Store Manager manages the product series (UC-012, UC-013, DEC-013).
        // The default manageable sets of Store Manager and IT Support are in api/seed/01_roles.sql (R-SEC-12, DEC-129).
        new(StoreManager, IsSystemRole: false,
            [PermissionKeys.StaffRead, PermissionKeys.StaffDisable, PermissionKeys.BrandRead, PermissionKeys.BrandCreate,
                PermissionKeys.BrandUpdate, PermissionKeys.ProductSeriesRead, PermissionKeys.ProductSeriesCreate,
                PermissionKeys.ProductSeriesUpdate]),
        new(SalesConsultant, IsSystemRole: false, []),
        new(TechnicalSupport, IsSystemRole: false, []),
        new(Cashier, IsSystemRole: false, []),
        new(WarehouseKeeper, IsSystemRole: false, []),
        new(Purchasing, IsSystemRole: false, []),
        new(Accountant, IsSystemRole: false, []),
        // DEC-128 (1): IT Support manages staff accounts, with no business, price or cost permission. staff.export
        // (DEC-127) joins this list with the feature that checks it, as a key new to the database then.
        new(ItSupport, IsSystemRole: false,
            [PermissionKeys.StaffRead, PermissionKeys.StaffCreate, PermissionKeys.StaffDisable]),
    ];
}
