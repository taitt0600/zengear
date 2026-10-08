using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Data.Externals.Auth;
using ZenGear.Services.Common;
using ZenGear.Services.Identity.Models;

namespace ZenGear.Services.Identity;

/// <summary>Disables and restores staff accounts (STF-02, UC-006).</summary>
public sealed class StaffStatusService(
    ZenGearDbContext db,
    IAuthProvider auth,
    StaffService staff,
    IClock clock,
    ILogger<StaffStatusService> logger)
{
    /// <summary>
    /// Disables the staff account <paramref name="id"/> and records the left date in the same change (R-USR-07), then
    /// revokes its Firebase sessions (R-SEC-04). The access cache is bumped on commit, so the account is refused from
    /// its next request. Repeating the request is safe: a DISABLED account records a given left date and its sessions
    /// are revoked again, which is how a failed revoke (500) is retried.
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

        if (await staff.HoldsAdminRoleAsync(id, cancellationToken) && !await staff.HoldsAdminRoleAsync(actorId, cancellationToken))
        {
            throw new BusinessRuleException("R-TX-11", "admin-account-not-lockable", "Admin account not lockable",
                "Only an Admin can disable an account that holds the Admin role.");
        }

        if (request.LeftDate < user.StaffProfile!.JoinedDate)
        {
            throw LeftDateBeforeJoinedDate();
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
    /// Restores the DISABLED staff account <paramref name="id"/> to ACTIVE and clears its left date (R-USR-03); an
    /// ACTIVE account is returned unchanged. Its sessions were revoked on disable: the staff member signs in again.
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
        user.Restore(actorId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Staff account {UserId} restored by {ActorId}", id, actorId);
        return await staff.GetAsync(id, CancellationToken.None);
    }

    private Task<User?> FindStaffAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.Include(u => u.StaffProfile)
            .SingleOrDefaultAsync(u => u.Id == id && u.StaffProfile != null, cancellationToken);

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
