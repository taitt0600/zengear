using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using ZenGear.Data;
using ZenGear.Data.Externals.Auth;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Undoes a Firebase account that a staff request created when the request then fails, so that no Firebase account is
/// left without its user row: creating an ACTIVE account (STF-01) and approving a PENDING one (STF-09). Best effort:
/// each failure is logged with ids only (R-SEC-11) and never replaces the original error.
/// </summary>
public sealed class StaffAuthCompensation(ZenGearDbContext db, IAuthProvider auth, ILogger<StaffAuthCompensation> logger)
{
    /// <summary>
    /// Rolls back after a failed save so that the compensation can query again (PostgreSQL refuses every statement in
    /// an aborted transaction). A rollback that fails (a broken connection) is logged and never replaces the original
    /// error nor skips the compensation; the server ends the transaction with the connection.
    /// </summary>
    public async Task RollbackQuietlyAsync(IDbContextTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rollback of a failed staff request failed");
        }
    }

    /// <summary>
    /// After a failed Firebase create: deletes the account Firebase may have created on <paramref name="email"/> unless a
    /// user row holds it. A failure of the lookup is logged without the email (personal data, R-SEC-11).
    /// </summary>
    public async Task DeleteUnlinkedAccountOfEmailAsync(string email)
    {
        AuthAccount? left;
        try
        {
            left = await auth.FindByEmailAsync(email, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Firebase account of a failed staff request could not be looked up: check the Firebase console");
            return;
        }

        if (left is not null)
        {
            await DeleteUnlinkedAccountAsync(left.Uid);
        }
    }

    /// <summary>Deletes the Firebase account created by this request unless a user row holds it.</summary>
    public async Task DeleteUnlinkedAccountAsync(string uid)
    {
        try
        {
            if (!await db.Users.AnyAsync(u => u.FirebaseUid == uid, CancellationToken.None))
            {
                await auth.DeleteAsync(uid, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Firebase account {FirebaseUid} left without a user row: delete it in the Firebase console", uid);
        }
    }
}
