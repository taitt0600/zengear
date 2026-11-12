namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// The roles whose staff accounts a person manages (Phạm vi quản lý, R-SEC-12): every role for a holder of Admin,
/// otherwise the union of the manageable sets of the roles the person holds, never Admin or Customer.
/// </summary>
public sealed class ManageableRoleSet
{
    private readonly HashSet<Guid> _roleIds;

    private ManageableRoleSet(bool everyRole, IEnumerable<Guid> roleIds)
    {
        EveryRole = everyRole;
        _roleIds = [.. roleIds];
    }

    public static ManageableRoleSet Every { get; } = new(true, []);

    public bool EveryRole { get; }

    /// <summary>The roles of the set when it is not <see cref="EveryRole"/>.</summary>
    public IReadOnlyCollection<Guid> RoleIds => _roleIds;

    public static ManageableRoleSet Of(IEnumerable<Guid> roleIds) => new(false, roleIds);

    /// <summary>Every role of <paramref name="roleIds"/> is in the set: what creating, disabling and restoring require.</summary>
    public bool Covers(IEnumerable<Guid> roleIds) => EveryRole || roleIds.All(_roleIds.Contains);
}
