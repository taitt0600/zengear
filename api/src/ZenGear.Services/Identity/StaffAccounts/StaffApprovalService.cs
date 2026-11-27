using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Data.Externals.Auth;
using ZenGear.Services.Common;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Approves, rejects and withdraws PENDING staff accounts (STF-09, UC-140, R-USR-17, DEC-128 (3), DEC-140). Each action
/// runs in one transaction that first locks the account's row, so two actions on one account run one after the other;
/// each refusal changes nothing. The checks, in order: the account exists (null), it is PENDING (409), it is the version
/// the caller saw (412, DEC-140 (2)), then the rules of the action.
/// </summary>
public sealed class StaffApprovalService(
    ZenGearDbContext db,
    IAuthProvider auth,
    StaffService staff,
    StaffAuthCompensation compensation,
    IClock clock,
    ILogger<StaffApprovalService> logger)
{
    /// <summary>
    /// PENDING → ACTIVE with a Firebase account on the default password; <c>must_change_password</c> stays true. Refused
    /// to the creator (R-USR-17) and when a role of the account is outside the approver's manageable set (R-SEC-12); an
    /// email another Firebase account holds leaves the account PENDING. A failure after Firebase created the account
    /// deletes that account again, as creating an ACTIVE account does (STF-01).
    /// </summary>
    /// <param name="expectedVersion">The version of <c>If-Match</c> (DEC-140 (2)).</param>
    /// <returns>The approved account; null when there is no staff account with this id.</returns>
    /// <exception cref="DbUpdateConcurrencyException">The account changed since <paramref name="expectedVersion"/>.</exception>
    public async Task<StaffDto?> ApproveAsync(Guid id, uint expectedVersion, Guid actorId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockPendingAsync(id, expectedVersion, cancellationToken);
        if (user is null)
        {
            return null;
        }

        await EnsureApproverAsync(user, actorId, cancellationToken);

        // An account that already exists is never linked: whoever registered the email first would keep its password.
        var email = user.Email ?? throw new InvalidOperationException($"Staff account {id} has no email.");
        if (await auth.FindByEmailAsync(email, cancellationToken) is { } existing)
        {
            logger.LogWarning("Staff account {UserId} not approved: Firebase account {FirebaseUid} already holds its email",
                id, existing.Uid);
            throw AuthAccountExists();
        }

        // From here the request token is no longer used: cancelled during the call, Firebase could still create the
        // account and nothing would delete it.
        AuthAccount? account;
        try
        {
            account = await auth.CreateWithDefaultStaffPasswordAsync(email, CancellationToken.None);
        }
        catch
        {
            // The row lock keeps every other approval of this account waiting, so the account found on this email is the
            // one this request may have created.
            await compensation.DeleteUnlinkedAccountOfEmailAsync(email);
            await compensation.RollbackQuietlyAsync(transaction);
            throw;
        }

        if (account is null)
        {
            throw AuthAccountExists();
        }

        user.Approve(account.Uid, actorId, clock.UtcNow);
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch
        {
            db.ChangeTracker.Clear();
            await compensation.RollbackQuietlyAsync(transaction);
            await compensation.DeleteUnlinkedAccountAsync(account.Uid);
            throw;
        }

        logger.LogInformation("Staff account {UserId} approved by {ActorId}", id, actorId);
        return await staff.GetAsync(id, new StaffViewer(actorId, MayApprove: false, MayCreate: false), CancellationToken.None);
    }

    /// <summary>
    /// Deletes the PENDING account with its profile and roles (R-USR-17). Refused to the creator (R-USR-17) and when a
    /// role of the account is outside the approver's manageable set (R-SEC-12).
    /// </summary>
    /// <param name="expectedVersion">The version of <c>If-Match</c> (DEC-140 (2)).</param>
    /// <returns>False when there is no staff account with this id.</returns>
    /// <exception cref="DbUpdateConcurrencyException">The account changed since <paramref name="expectedVersion"/>.</exception>
    public async Task<bool> RejectAsync(Guid id, uint expectedVersion, Guid actorId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockPendingAsync(id, expectedVersion, cancellationToken);
        if (user is null)
        {
            return false;
        }

        await EnsureApproverAsync(user, actorId, cancellationToken);
        await DeleteAsync(user, transaction);
        logger.LogInformation("Staff account {UserId} rejected by {ActorId}", id, actorId);
        return true;
    }

    /// <summary>
    /// The creator deletes their own PENDING account, as a rejection does, to enter it again with the right data
    /// (DEC-140 (1)); anyone else is refused with 409.
    /// </summary>
    /// <param name="expectedVersion">The version of <c>If-Match</c> (DEC-140 (2)).</param>
    /// <returns>False when there is no staff account with this id.</returns>
    /// <exception cref="DbUpdateConcurrencyException">The account changed since <paramref name="expectedVersion"/>.</exception>
    public async Task<bool> WithdrawAsync(Guid id, uint expectedVersion, Guid actorId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockPendingAsync(id, expectedVersion, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (user.StaffProfile!.CreatedBy != actorId)
        {
            throw new BusinessRuleException("R-USR-17", "not-account-creator");
        }

        await DeleteAsync(user, transaction);
        logger.LogInformation("Staff account {UserId} withdrawn by {ActorId}", id, actorId);
        return true;
    }

    /// <summary>
    /// Locks the account's row until the transaction ends, then loads it: null when there is no staff account; 409 when
    /// it is not PENDING; 412 when its version is not <paramref name="expectedVersion"/>, which also becomes the
    /// concurrency token of the save.
    /// </summary>
    private async Task<User?> LockPendingAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken)
    {
        await db.Database.SqlQuery<Guid>($"""SELECT id AS "Value" FROM users WHERE id = {id} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        var user = await db.Users.Include(u => u.StaffProfile).Include(u => u.UserRoles)
            .SingleOrDefaultAsync(u => u.Id == id && u.StaffProfile != null, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (user.Status != UserStatus.Pending)
        {
            throw new BusinessRuleException("R-USR-17", "account-not-pending");
        }

        if (user.Version != expectedVersion)
        {
            throw new DbUpdateConcurrencyException("The staff account changed since it was read.");
        }

        db.Entry(user).Property(u => u.Version).OriginalValue = expectedVersion;
        return user;
    }

    /// <summary>
    /// The approver is not the creator (R-USR-17) and manages every role of the account (R-SEC-12); 409 otherwise.
    /// Holding <c>staff.approve</c> is checked by the endpoint.
    /// </summary>
    private async Task EnsureApproverAsync(User user, Guid actorId, CancellationToken cancellationToken)
    {
        if (user.StaffProfile!.CreatedBy == actorId)
        {
            throw new BusinessRuleException("R-USR-17", "own-account-request-not-approvable");
        }

        var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
        if (!(await staff.ManageableRolesOfAsync(actorId, cancellationToken)).Covers(roleIds))
        {
            throw new BusinessRuleException("R-SEC-12", "account-out-of-scope");
        }
    }

    /// <summary>
    /// Deletes the account with its <c>staff_profiles</c> and <c>user_roles</c> rows, loaded by
    /// <see cref="LockPendingAsync"/> so EF Core deletes them itself and the interceptor of R-AUD-01 writes their rows (a
    /// cascade of the database would not reach it); the R-SEC-01 trigger lets the roles go with their user. The version
    /// is the delete's concurrency token.
    /// </summary>
    private async Task DeleteAsync(User user, IDbContextTransaction transaction)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
    }

    private static BusinessRuleException AuthAccountExists() => new(null, "auth-account-exists");
}
