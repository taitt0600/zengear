using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Who reads or changes staff accounts, with the two permissions the PENDING flags depend on (R-USR-17, DEC-137 (1),
/// DEC-140 (1)), from the keys authorization already read for the request. It never leaves the services
/// (docs/ARCHITECTURE.md, *Folder structure*).
/// </summary>
internal sealed record StaffViewer(Guid Id, bool MayApprove, bool MayCreate)
{
    public static StaffViewer Of(Guid actorId, IEnumerable<string> permissionKeys)
    {
        ArgumentNullException.ThrowIfNull(permissionKeys);
        var keys = permissionKeys.ToHashSet(StringComparer.Ordinal);
        return new StaffViewer(actorId, keys.Contains(PermissionKeys.StaffApprove), keys.Contains(PermissionKeys.StaffCreate));
    }
}
