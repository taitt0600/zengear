namespace ZenGear.Data.Entities.Identity;

/// <summary>
/// A role held by a user (<c>user_roles</c>); a user may hold several (DEC-012). The database refuses
/// removing the last role of a user (R-SEC-01) and the Admin role of the last active Admin (R-SEC-02).
/// </summary>
public class UserRole
{
    protected UserRole()
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
