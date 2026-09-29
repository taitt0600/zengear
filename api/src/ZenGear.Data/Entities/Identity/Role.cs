using ZenGear.Data.Entities.Common;

namespace ZenGear.Data.Entities.Identity;

/// <summary>
/// A role, defined by its permissions and scope (<c>roles</c>). Access is always checked through
/// permissions, never by role name (R-SEC-03).
/// </summary>
public class Role : IAggregateRoot
{
    private readonly List<RolePermission> _rolePermissions = [];

    private Role()
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

    public uint Version { get; private set; }

    /// <summary>Makes the grants exactly <paramref name="permissionIds"/>; requires <see cref="RolePermissions"/> loaded.</summary>
    /// <returns>True when a grant was added or removed.</returns>
    public bool SetPermissions(IEnumerable<Guid> permissionIds)
    {
        var wanted = permissionIds.ToHashSet();
        var removed = _rolePermissions.RemoveAll(rp => !wanted.Contains(rp.PermissionId));
        var added = wanted.Except(_rolePermissions.Select(rp => rp.PermissionId)).ToList();
        _rolePermissions.AddRange(added.Select(id => new RolePermission(Id, id)));
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
}
