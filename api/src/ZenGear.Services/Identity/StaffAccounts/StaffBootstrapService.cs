using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Externals.Auth;
using ZenGear.Services.Common;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>The first Admin of an environment, created once by the <c>bootstrap-admin</c> host command (DEC-096, DEC-156).</summary>
public sealed class StaffBootstrapService(
    ZenGearDbContext db,
    IAuthProvider auth,
    StaffIdentityIssuer identities,
    IClock clock,
    ILogger<StaffBootstrapService> logger)
{
    /// <summary>
    /// Under an advisory lock, refuses when a user already holds the Admin role; otherwise issues the employee code,
    /// username and email of R-USR-24 for <paramref name="name"/> and <paramref name="joinedDate"/>, links the Firebase
    /// account of that email (created without a password when there is none), creates the ACTIVE user, its staff profile
    /// (no forced password change) and the Admin role in one transaction, then generates a password reset link. A failure
    /// of the link does not undo the Admin: the result carries no link.
    /// </summary>
    /// <exception cref="ValidationException">The name or the joined date is not valid (the checks of STF-01); nothing
    /// changed.</exception>
    /// <exception cref="BusinessRuleException">An Admin exists, the Admin role is missing, the email is taken or the year's
    /// codes are issued; nothing changed.</exception>
    public async Task<AdminBootstrapResult> BootstrapAdminAsync(string name, DateOnly joinedDate, CancellationToken cancellationToken)
    {
        if (StaffCreateRequestValidator.IdentityFailures(name, joinedDate) is { Count: > 0 } failures)
        {
            throw new ValidationException(failures);
        }

        name = name.Trim();
        Guid userId;
        StaffIdentity identity;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.AdminBootstrap})", cancellationToken);

            var adminRole = await db.Roles.SingleOrDefaultAsync(r => r.Name == PermissionCatalog.AdminRoleName, cancellationToken)
                ?? throw new BusinessRuleException(null, "admin-role-missing");
            if (await db.UserRoles.AnyAsync(ur => ur.RoleId == adminRole.Id, cancellationToken))
            {
                throw new BusinessRuleException(null, "admin-already-exists");
            }

            identity = await identities.IssueAsync(name, joinedDate, cancellationToken);
            if (await db.Users.AnyAsync(u => u.Email == identity.Email, cancellationToken))
            {
                throw EmailAlreadyRegistered();
            }

            // Only an account this command created is deleted when the transaction fails; an existing one is reused
            // (DEC-096): tools/dev_reset.ps1 creates the emulator account of the generated email first.
            var account = await auth.FindByEmailAsync(identity.Email, cancellationToken);
            var created = false;
            if (account is null)
            {
                account = await auth.CreateWithoutPasswordAsync(identity.Email, CancellationToken.None);
                created = account is not null;
                account ??= await auth.FindByEmailAsync(identity.Email, cancellationToken)
                    ?? throw new InvalidOperationException("The Firebase account could neither be created nor found.");
            }

            if (await db.Users.AnyAsync(u => u.FirebaseUid == account.Uid, cancellationToken))
            {
                throw EmailAlreadyRegistered();
            }

            var user = User.CreateStaff(
                new StaffAccountData(account.Uid, name, identity.Email, identity.EmployeeCode, identity.Username, joinedDate,
                    WorkPhone: null, MustChangePassword: false),
                [adminRole], createdBy: null, clock.UtcNow);
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }
            catch
            {
                if (created)
                {
                    await auth.DeleteAsync(account.Uid, CancellationToken.None);
                }

                throw;
            }

            userId = user.Id;
        }

        logger.LogInformation("First Admin {UserId} created by bootstrap-admin", userId);
        string? errorType = null;
        string? link = null;
        try
        {
            link = await auth.GeneratePasswordResetLinkAsync(identity.Email, CancellationToken.None);
        }
        catch (Exception ex)
        {
            errorType = ex.GetType().Name;
        }

        if (errorType is not null)
        {
            // Only the type, not the exception: the message of a provider error may repeat the email.
            logger.LogError("Password reset link for Admin {UserId} could not be generated: {ErrorType}", userId, errorType);
        }

        return new AdminBootstrapResult(userId, identity.EmployeeCode, identity.Username, identity.Email, link);
    }

    private static BusinessRuleException EmailAlreadyRegistered() =>
        new(null, "email-already-registered");
}
