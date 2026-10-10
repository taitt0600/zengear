using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Data.Externals.Auth;
using ZenGear.Services.Common;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>Disables and restores staff accounts (STF-02, UC-006).</summary>
public sealed class StaffStatusService(
    ZenGearDbContext db,
    IAuthProvider auth,
    StaffService staff,
    IClock clock,
    ILogger<StaffStatusService> logger)
{
    /// <summary>
    /// Disables the staff account <paramref name="id"/> (STF-02). The access cache is bumped on commit, so the account
    /// is refused from its next request. Repeating the request is safe: a DISABLED account records a given left date
    /// and its sessions are revoked again, which is how a failed revoke (500) is retried.
    /// </summary>
    /// <param name="actorId">The signed-in staff member disabling the account.</param>
    /// <returns>Null when there is no staff account with this id.</returns>
    public async Task<StaffDto?> DisableAsync(Guid id, StaffDisableRequest request, Guid actorId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await FindStaffAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        ThrowIfAnonymized(user);

        if (id == actorId)
        {
            throw new BusinessRuleException("R-USR-12", "own-account-not-lockable", "Own account not lockable",
                "No one can disable their own account.");
        }

        var holdsAdmin = await staff.HoldsAdminRoleAsync(id, cancellationToken);
        if (holdsAdmin && !await staff.HoldsAdminRoleAsync(actorId, cancellationToken))
        {
            throw new BusinessRuleException("R-TX-11", "admin-account-not-lockable", "Admin account not lockable",
                "Only an Admin can disable an account that holds the Admin role.");
        }

        if (request.LeftDate < user.StaffProfile!.JoinedDate)
        {
            throw LeftDateBeforeJoinedDate();
        }

        if (request.LeftDate > clock.Today)
        {
            throw new BusinessRuleException("R-USR-07", "left-date-in-future", "Left date in the future",
                "The left date cannot be after today.");
        }

        // Only disabling an ACTIVE Admin needs the lock of the last-Admin check held until the commit.
        await using var transaction = holdsAdmin && user.Status == UserStatus.Active
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (transaction is not null)
        {
            await ThrowIfLastActiveAdminAsync(id, cancellationToken);
        }

        // From the save on the request token is no longer used: a client that disconnects during the commit could
        // otherwise leave the account DISABLED with its sessions alive and no 500 telling it to repeat.
        user.Disable(request.LeftDate, actorId, clock.UtcNow);

        // staff_profiles has no version: when the profile changes, the users row is written too, so its version makes a
        // concurrent restore conflict instead of leaving an ACTIVE account with a left date.
        if (db.Entry(user.StaffProfile).State == EntityState.Modified)
        {
            db.Entry(user).Property(u => u.UpdatedAt).IsModified = true;
        }

        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
            if (transaction is not null)
            {
                await transaction.CommitAsync(CancellationToken.None);
            }
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.CheckViolation, ConstraintName: "ck_staff_profiles_left_date",
        })
        {
            throw LeftDateBeforeJoinedDate();
        }

        // After the commit: the account is already refused by status.
        if (user.FirebaseUid is { } uid)
        {
            await RevokeSessionsAsync(id, uid);
        }
        else
        {
            logger.LogWarning("Staff account {UserId} has no Firebase account: no session to revoke", id);
        }

        logger.LogInformation("Staff account {UserId} disabled by {ActorId}", id, actorId);
        return await staff.GetAsync(id, CancellationToken.None);
    }

    /// <summary>
    /// Restores the DISABLED staff account <paramref name="id"/> (STF-02); an ACTIVE account is returned unchanged. Its
    /// sessions were revoked on disable: the staff member signs in again.
    /// </summary>
    /// <param name="actorId">The signed-in staff member restoring the account.</param>
    /// <returns>Null when there is no staff account with this id.</returns>
    public async Task<StaffDto?> RestoreAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        var user = await FindStaffAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        ThrowIfAnonymized(user);

        if (await staff.HoldsAdminRoleAsync(id, cancellationToken) && !await staff.HoldsAdminRoleAsync(actorId, cancellationToken))
        {
            throw new BusinessRuleException("R-USR-12", "admin-account-not-restorable", "Admin account not restorable",
                "Only an Admin can restore an account that holds the Admin role.");
        }

        user.Restore(actorId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Staff account {UserId} restored by {ActorId}", id, actorId);
        return await staff.GetAsync(id, CancellationToken.None);
    }

    private Task<User?> FindStaffAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.Include(u => u.StaffProfile)
            .SingleOrDefaultAsync(u => u.Id == id && u.StaffProfile != null, cancellationToken);

    /// <summary>
    /// Locks the ACTIVE accounts holding the Admin role until the transaction ends and refuses when none is left besides
    /// <paramref name="id"/> (R-USR-12). Two Admins disabling each other at once: the second waits on the lock, then no
    /// longer counts the first one, now DISABLED, and is refused.
    /// </summary>
    private async Task ThrowIfLastActiveAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        var activeAdmins = await db.Database.SqlQuery<Guid>($"""
            SELECT u.id AS "Value" FROM users u
            WHERE u.status = 'ACTIVE'
              AND EXISTS (SELECT 1 FROM user_roles ur JOIN roles r ON r.id = ur.role_id
                          WHERE ur.user_id = u.id AND r.name = {PermissionCatalog.AdminRoleName})
            ORDER BY u.id
            FOR UPDATE OF u
            """).ToListAsync(cancellationToken);

        if (activeAdmins.TrueForAll(adminId => adminId == id))
        {
            throw new BusinessRuleException("R-USR-12", "last-active-admin", "Last active Admin",
                "The last ACTIVE account holding the Admin role cannot be disabled.");
        }
    }

    private async Task RevokeSessionsAsync(Guid id, string uid)
    {
        try
        {
            await auth.RevokeSessionsAsync(uid, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // CancellationToken.None is passed, so even a cancellation here is a failure (an HTTP timeout to Firebase).
            // Logged by the host as an unhandled exception (500): ids only, the caller repeats the disable.
            throw new InvalidOperationException(
                $"Staff account {id} is disabled but its sessions were not revoked: repeat the disable.", ex);
        }
    }

    private static void ThrowIfAnonymized(User user)
    {
        if (user.Status == UserStatus.Anonymized)
        {
            throw new BusinessRuleException("R-USR-03", "account-anonymized", "Account anonymized",
                "An anonymized account cannot be disabled or restored.");
        }
    }

    private static BusinessRuleException LeftDateBeforeJoinedDate() =>
        new("R-USR-07", "left-date-before-joined-date", "Left date before joined date",
            "The left date cannot be before the joined date.");
}
