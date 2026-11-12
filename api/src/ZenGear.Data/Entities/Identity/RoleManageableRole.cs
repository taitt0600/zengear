namespace ZenGear.Data.Entities.Identity;

/// <summary>
/// A role whose staff accounts the holders of <see cref="RoleId"/> may manage (<c>role_manageable_roles</c>, R-SEC-12,
/// DEC-128): part of the manageable set (Phạm vi quản lý) of that role.
/// </summary>
public class RoleManageableRole
{
    private RoleManageableRole()
    {
    }

    internal RoleManageableRole(Guid roleId, Guid managedRoleId, Guid? createdBy, DateTimeOffset createdAt)
    {
        RoleId = roleId;
        ManagedRoleId = managedRoleId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid RoleId { get; private set; }

    public Guid ManagedRoleId { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
