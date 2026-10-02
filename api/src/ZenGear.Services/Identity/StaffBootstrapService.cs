using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Externals.Auth;
using ZenGear.Services.Common;
using ZenGear.Services.Identity.Models;
using ZenGear.Services.Identity.Permissions;
using ZenGear.Services.Identity.Validators;

namespace ZenGear.Services.Identity;

/// <summary>The first Admin of an environment, created once by the <c>bootstrap-admin</c> host command (DEC-096).</summary>
public sealed class StaffBootstrapService(
    ZenGearDbContext db,
    IAuthProvider auth,
    IClock clock,
    ILogger<StaffBootstrapService> logger)
{
    /// <summary>
    /// Under an advisory lock, refuses when a user already holds the Admin role; otherwise links the Firebase account of
    /// <paramref name="email"/> (created without a password when there is none), creates the ACTIVE user, its staff
    /// profile (no forced password change) and the Admin role in one transaction, then generates a password reset link.
    /// A failure of the link does not undo the Admin: the result carries no link.
    /// </summary>
    /// <exception cref="ValidationException">Email or name not valid; nothing changed.</exception>
    /// <exception cref="BusinessRuleException">An Admin exists, the Admin role is missing or the email is taken; nothing changed.</exception>
    public async Task<AdminBootstrapResult> BootstrapAdminAsync(string email, string name, CancellationToken cancellationToken)
    {
        email = User.NormalizeEmail(email);
        name = name.Trim();
        Validate(email, name);

        Guid userId;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKeys.AdminBootstrap})", cancellationToken);

            var adminRole = await db.Roles.SingleOrDefaultAsync(r => r.Name == PermissionCatalog.AdminRoleName, cancellationToken)
                ?? throw new BusinessRuleException(null, "admin-role-missing", "Admin role missing",
                    "The Admin role does not exist: run api/db/zengear_08_roles.sql and api/seed first.");
            if (await db.UserRoles.AnyAsync(ur => ur.RoleId == adminRole.Id, cancellationToken))
            {
                throw new BusinessRuleException(null, "admin-already-exists", "Admin already exists",
                    "An Admin already exists; nothing was changed. The Admin creates the other accounts.");
            }

            if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            {
                throw EmailAlreadyRegistered();
            }

            // Only an account this command created is deleted when the transaction fails; an existing one is reused (DEC-096).
            var account = await auth.FindByEmailAsync(email, cancellationToken);
            var created = false;
            if (account is null)
            {
                account = await auth.CreateWithoutPasswordAsync(email, CancellationToken.None);
                created = account is not null;
                account ??= await auth.FindByEmailAsync(email, cancellationToken)
                    ?? throw new InvalidOperationException("The Firebase account could neither be created nor found.");
            }

            if (await db.Users.AnyAsync(u => u.FirebaseUid == account.Uid, cancellationToken))
            {
                throw EmailAlreadyRegistered();
            }

            var user = User.CreateStaff(
                new StaffAccountData(account.Uid, name, email, EmployeeCode: null, JoinedDate: null, WorkPhone: null,
                    MustChangePassword: false),
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
            link = await auth.GeneratePasswordResetLinkAsync(email, CancellationToken.None);
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

        return new AdminBootstrapResult(userId, link);
    }

    private static void Validate(string email, string name)
    {
        var failures = new List<ValidationFailure>();
        if (!StaffCreateRequestValidator.IsEmail(email))
        {
            failures.Add(new ValidationFailure(nameof(email), "Email is not a valid email address."));
        }

        if (name.Length == 0 || name.Length > StaffCreateRequestValidator.NameMaxLength)
        {
            failures.Add(new ValidationFailure(nameof(name),
                $"Name is required and at most {StaffCreateRequestValidator.NameMaxLength} characters."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }

    private static BusinessRuleException EmailAlreadyRegistered() =>
        new(null, "email-already-registered", "Email already registered", "Another account already uses this email.");
}
