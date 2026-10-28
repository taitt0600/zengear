using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZenGear.Data;
using ZenGear.Data.Externals.Auth;
using ZenGear.Services.Common;
using ZenGear.Services.Identity.Access;

namespace ZenGear.Services.Identity.Account;

/// <summary>The signed-in user's own account (AUT-01, STF-03).</summary>
public sealed class CurrentUserService(
    ZenGearDbContext db,
    IAuthProvider auth,
    IValidator<CurrentUserUpdateRequest> updateValidator,
    IValidator<CurrentUserPasswordChangeRequest> passwordValidator,
    IClock clock,
    ILogger<CurrentUserService> logger)
{
    /// <summary>How old the sign-in behind a password change may be (DEC-094).</summary>
    public static readonly TimeSpan RecentSignInWindow = TimeSpan.FromMinutes(5);

    /// <param name="permissionKeys">The keys the caller's roles grant, as authorization read them for this request
    /// (<see cref="UserAccess.Permissions"/>); returned sorted, never queried again.</param>
    public async Task<CurrentUserDto> GetAsync(Guid userId, IEnumerable<string> permissionKeys,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                IsStaff = u.StaffProfile != null,
                MustChangePassword = u.StaffProfile != null && u.StaffProfile.MustChangePassword,
                u.Version,
            })
            .SingleAsync(cancellationToken);
        return new CurrentUserDto(user.Id, user.Name, user.Email, user.IsStaff, user.MustChangePassword,
            [.. permissionKeys.Order(StringComparer.Ordinal)], user.Version);
    }

    /// <summary>Changes the display name of the signed-in user, staff or customer (R-USR-04).</summary>
    /// <param name="permissionKeys">As for <see cref="GetAsync"/>.</param>
    /// <param name="expectedVersion">The <c>If-Match</c> version; null for <c>If-Match: *</c>.</param>
    /// <exception cref="DbUpdateConcurrencyException">The account changed since <paramref name="expectedVersion"/>.</exception>
    public async Task<CurrentUserDto> UpdateAsync(Guid userId, IEnumerable<string> permissionKeys, uint? expectedVersion,
        CurrentUserUpdateRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        if (expectedVersion is { } expected)
        {
            // An unchanged name issues no UPDATE, so the stored version is compared here as well as on save.
            if (user.Version != expected)
            {
                throw new DbUpdateConcurrencyException("The account changed since it was read.");
            }

            db.Entry(user).Property(u => u.Version).OriginalValue = expected;
        }

        user.Rename(request.Name!, userId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, permissionKeys, cancellationToken);
    }

    /// <summary>
    /// Changes the password of the signed-in staff member (STF-03, R-USR-03). Checks, in order: a staff account, a
    /// sign-in within <see cref="RecentSignInWindow"/> (R-USR-04), the password policy (R-USR-11); a refused request
    /// changes nothing. Then Firebase first, the flag second, the sessions last: a failure after Firebase leaves the
    /// flag set or the sessions alive, and the staff member changes the password again.
    /// </summary>
    /// <param name="authTime"><c>auth_time</c> of the ID token; null when the token has none.</param>
    public async Task ChangePasswordAsync(Guid userId, DateTimeOffset? authTime, CurrentUserPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.Include(u => u.StaffProfile).SingleAsync(u => u.Id == userId, cancellationToken);
        if (user.StaffProfile is not { } profile || user.FirebaseUid is not { } uid)
        {
            throw new BusinessRuleException(null, "staff-account-required");
        }

        var now = clock.UtcNow;
        if (authTime is not { } signedInAt || now - signedInAt > RecentSignInWindow)
        {
            throw new RecentSignInRequiredException();
        }

        await passwordValidator.ValidateAndThrowAsync(request, cancellationToken);

        // From here the request token is no longer used: cancelled during the call, Firebase could still change the
        // password while the flag and the sessions stay as they were.
        await auth.ChangePasswordAsync(uid, request.NewPassword!, CancellationToken.None);
        profile.PasswordChanged(userId, now);
        await db.SaveChangesAsync(CancellationToken.None);
        await auth.RevokeSessionsAsync(uid, CancellationToken.None);

        logger.LogInformation("Password changed for user {UserId}; other sessions revoked", userId);
    }
}
