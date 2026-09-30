using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Services.Common;
using ZenGear.Services.Identity.AccessCache;

namespace ZenGear.Services.Identity.Permissions;

/// <summary>
/// Applies <see cref="IPermissionCatalog"/> to the database (DEC-095, R-SEC-03), in one transaction under an
/// advisory lock so several API instances starting together run it one after the other.
/// </summary>
public sealed class PermissionSyncService(
    ZenGearDbContext db, IPermissionCatalog catalog, IAccessCache cache, IClock clock, ILogger<PermissionSyncService> logger)
{
    public async Task<PermissionSyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.PermissionSync})", cancellationToken);

        // 0. Nothing before every role exists: permissions inserted now would no longer be "new" once the
        //    seed has run, and the default roles would never receive them.
        var roleNames = catalog.Roles.Select(r => r.RoleName).ToList();
        var roles = await db.Roles
            .Include(r => r.RolePermissions)
            .Where(r => roleNames.Contains(r.Name))
            .ToDictionaryAsync(r => r.Name, cancellationToken);
        var missing = roleNames.Where(name => !roles.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            logger.LogError(
                "Permission sync skipped: roles {MissingRoles} do not exist; run api/seed/01_roles.sql first, then restart the API",
                missing);
            return PermissionSyncResult.RolesMissing;
        }

        foreach (var grants in catalog.Roles.Where(g => roles[g.RoleName].IsSystemRole != g.IsSystemRole))
        {
            logger.LogWarning(
                "Role {RoleName} has is_system_role = {Stored} but the catalog classifies it as {Expected} (DEC-095)",
                grants.RoleName, roles[grants.RoleName].IsSystemRole, grants.IsSystemRole);
        }

        // 1. New keys and descriptions.
        var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, cancellationToken);
        var newKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in catalog.Permissions)
        {
            if (existing.TryGetValue(definition.Key, out var permission))
            {
                permission.Describe(definition.Description, clock.UtcNow);
            }
            else
            {
                db.Permissions.Add(new Permission(definition.Key, definition.Description));
                newKeys.Add(definition.Key);
            }
        }

        // 2. Keys no longer in the catalog: the cascade also removes them from roles outside the catalog,
        //    which the change tracker does not see, so those roles are bumped explicitly after the commit.
        var catalogKeys = catalog.Permissions.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        var removed = existing.Values.Where(p => !catalogKeys.Contains(p.Key)).ToList();
        var affectedRoleIds = new List<Guid>();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(p => p.Id).ToList();
            var affected = await db.RolePermissions
                .Where(rp => removedIds.Contains(rp.PermissionId))
                .Join(db.Roles, rp => rp.RoleId, r => r.Id, (_, r) => new { r.Id, r.Name })
                .Distinct()
                .ToListAsync(cancellationToken);
            affectedRoleIds.AddRange(affected.Select(r => r.Id));
            logger.LogWarning("Permission sync removes {PermissionKeys}, no longer defined in code; roles losing them: {RoleNames}",
                removed.Select(p => p.Key).ToList(), affected.Select(r => r.Name).ToList());
            db.Permissions.RemoveRange(removed);
        }

        await db.SaveChangesAsync(cancellationToken);

        // 3–4. Grants.
        var idsByKey = await db.Permissions.ToDictionaryAsync(p => p.Key, p => p.Id, cancellationToken);
        foreach (var grants in catalog.Roles)
        {
            var role = roles[grants.RoleName];
            if (grants.IsSystemRole)
            {
                role.SetPermissions(grants.PermissionKeys.Select(key => idsByKey[key]));
            }
            else
            {
                foreach (var key in grants.PermissionKeys.Where(newKeys.Contains))
                {
                    role.GrantPermission(idsByKey[key]);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await cache.BumpAsync(affectedRoleIds.Select(AccessBump.Role).ToList());
        return PermissionSyncResult.Synchronized;
    }
}
