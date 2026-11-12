namespace ZenGear.Data.Entities.Identity;

/// <summary>A permission granted to a role (<c>role_permissions</c>).</summary>
public class RolePermission
{
    private RolePermission()
    {
    }

    /// <param name="createdBy">The Admin granting it through the API (AUT-05); null for the permission sync.</param>
    internal RolePermission(Guid roleId, Guid permissionId, Guid? createdBy = null)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        CreatedBy = createdBy;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
