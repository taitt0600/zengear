using ZenGear.Data.Common;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Data.Entities.Identity;

/// <summary>
/// A role, defined by its permissions and scope (<c>roles</c>). Access is always checked through
/// permissions, never by role name (R-SEC-03).
/// </summary>
public class Role : IAggregateRoot
{
    private readonly List<RolePermission> _rolePermissions = [];
    private readonly List<RoleManageableRole> _manageableRoles = [];

    protected Role()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public ScopingType ScopingType { get; private set; }

    /// <summary>Admin and Customer: permissions come from code only, never edited through the API (DEC-095).</summary>
    public bool IsSystemRole { get; private set; }

    /// <summary>Highest manual discount in percent this role may give; null = cannot discount (R-PRM-04).</summary>
    public decimal? MaxDiscountPercent { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions;

    /// <summary>The roles whose staff accounts the holders of this role manage (R-SEC-12, DEC-128).</summary>
    public IReadOnlyCollection<RoleManageableRole> ManageableRoles => _manageableRoles;

    public uint Version { get; private set; }

    /// <summary>
    /// A staff role created by the Admin (UC-002): scope NONE, not a system role, managing no role until the Admin grants
    /// it (R-SEC-12). The service validates first for a clear error; these checks are the backstop.
    /// </summary>
    /// <exception cref="ArgumentException">A blank name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A discount outside 0–100.</exception>
    public static Role Create(string name, decimal? maxDiscountPercent, IEnumerable<Guid> permissionIds, Guid createdBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(permissionIds);
        var role = new Role
        {
            Name = RequiredName(name),
            ScopingType = ScopingType.None,
            IsSystemRole = false,
            MaxDiscountPercent = CheckedDiscount(maxDiscountPercent),
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedBy = createdBy,
            UpdatedAt = now,
        };
        role._rolePermissions.AddRange(permissionIds.Distinct().Select(id => new RolePermission(role.Id, id, createdBy)));
        return role;
    }

    /// <summary>
    /// Sets every editable field (UC-003); requires <see cref="RolePermissions"/> and <see cref="ManageableRoles"/>
    /// loaded. Stamps the role when anything changed, so its version (the ETag) changes even when only the grants or the
    /// manageable set did. Which fields a system or default role may change is checked by the service (DEC-095).
    /// </summary>
    /// <returns>True when something changed.</returns>
    /// <exception cref="ArgumentException">A blank name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A discount outside 0–100.</exception>
    public bool Update(string name, decimal? maxDiscountPercent, IEnumerable<Guid> permissionIds,
        IEnumerable<Guid> manageableRoleIds, Guid updatedBy, DateTimeOffset now)
    {
        var trimmed = RequiredName(name);
        var discount = CheckedDiscount(maxDiscountPercent);
        var changed = trimmed != Name || discount != MaxDiscountPercent;
        Name = trimmed;
        MaxDiscountPercent = discount;
        changed |= SetPermissions(permissionIds, updatedBy);
        changed |= SetManageableRoles(manageableRoleIds, updatedBy, now);
        if (changed)
        {
            UpdatedBy = updatedBy;
            UpdatedAt = now;
        }

        return changed;
    }

    /// <summary>Makes the grants exactly <paramref name="permissionIds"/>; requires <see cref="RolePermissions"/> loaded.</summary>
    /// <param name="grantedBy">The Admin editing the role; null for the permission sync.</param>
    /// <returns>True when a grant was added or removed.</returns>
    public bool SetPermissions(IEnumerable<Guid> permissionIds, Guid? grantedBy = null)
    {
        var wanted = permissionIds.ToHashSet();
        var removed = _rolePermissions.RemoveAll(rp => !wanted.Contains(rp.PermissionId));
        var added = wanted.Except(_rolePermissions.Select(rp => rp.PermissionId)).ToList();
        _rolePermissions.AddRange(added.Select(id => new RolePermission(Id, id, grantedBy)));
        return removed > 0 || added.Count > 0;
    }

    /// <summary>Adds one grant; requires <see cref="RolePermissions"/> loaded.</summary>
    /// <returns>False when the role already had it.</returns>
    public bool GrantPermission(Guid permissionId)
    {
        if (_rolePermissions.Any(rp => rp.PermissionId == permissionId))
        {
            return false;
        }

        _rolePermissions.Add(new RolePermission(Id, permissionId));
        return true;
    }

    /// <summary>
    /// Adds one role to the manageable set, for the default sets of the permission sync (R-SEC-12); requires
    /// <see cref="ManageableRoles"/> loaded.
    /// </summary>
    /// <returns>False when the role was already in the set.</returns>
    public bool GrantManageableRole(Guid managedRoleId, DateTimeOffset now)
    {
        if (_manageableRoles.Any(m => m.ManagedRoleId == managedRoleId))
        {
            return false;
        }

        _manageableRoles.Add(new RoleManageableRole(Id, managedRoleId, null, now));
        return true;
    }

    private bool SetManageableRoles(IEnumerable<Guid> managedRoleIds, Guid grantedBy, DateTimeOffset now)
    {
        var wanted = managedRoleIds.ToHashSet();
        var removed = _manageableRoles.RemoveAll(m => !wanted.Contains(m.ManagedRoleId));
        var added = wanted.Except(_manageableRoles.Select(m => m.ManagedRoleId)).ToList();
        _manageableRoles.AddRange(added.Select(id => new RoleManageableRole(Id, id, grantedBy, now)));
        return removed > 0 || added.Count > 0;
    }

    private static string RequiredName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        return trimmed.Length == 0 ? throw new ArgumentException("A role name is blank.", nameof(name)) : trimmed;
    }

    private static decimal? CheckedDiscount(decimal? value) =>
        value is < 0 or > 100
            ? throw new ArgumentOutOfRangeException(nameof(value), "max_discount_percent is between 0 and 100.")
            : value;
}
