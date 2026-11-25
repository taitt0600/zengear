using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Data.Externals.Auth;
using ZenGear.Services.Common;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// Staff accounts, for the staff screens (STF-01, STF-05); the one projection of <see cref="StaffDto"/> and of
/// <see cref="StaffListItemDto"/>.
/// </summary>
public sealed class StaffService(
    ZenGearDbContext db,
    IAuthProvider auth,
    StaffIdentityIssuer identities,
    IValidator<StaffCreateRequest> validator,
    IValidator<StaffListRequest> listValidator,
    IValidator<PageRequest> pageValidator,
    IClock clock,
    ILogger<StaffService> logger)
{
    /// <summary>
    /// Creates a staff account with the default password, its generated identifiers (R-USR-24) and its roles (UC-005).
    /// Every rule is checked before the employee code is taken and before the Firebase account is created, so a refused
    /// request creates nothing and takes no number. The code is taken in the transaction that stores the account; once
    /// the Firebase account exists the request token is no longer used, so a client that disconnects cannot leave an
    /// account without its user row.
    /// </summary>
    /// <param name="actorId">The signed-in staff member creating the account.</param>
    public async Task<StaffDto> CreateAsync(StaffCreateRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var roles = await AssignableRolesAsync(request.RoleIds ?? [], actorId, cancellationToken);

        Guid userId;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var identity = await identities.IssueAsync(request.Name ?? "", request.JoinedDate, cancellationToken);

            // An account that already exists is never linked: whoever registered the email first would keep its
            // password, other sign-in methods and live ID tokens. Its uid is logged so the operator can find it (an
            // orphan left by a failed compensation); the code is not, since the rollback issues it again.
            if (await auth.FindByEmailAsync(identity.Email, cancellationToken) is { } existing)
            {
                logger.LogWarning("Staff account not created: Firebase account {FirebaseUid} already holds the generated email",
                    existing.Uid);
                throw AuthAccountExists();
            }

            // From here the request token is no longer used: cancelled during the call, Firebase could still create the
            // account and nothing would delete it.
            AuthAccount? account;
            try
            {
                account = await auth.CreateWithDefaultStaffPasswordAsync(identity.Email, CancellationToken.None);
            }
            catch
            {
                // Firebase may have created the account before failing (a timeout after the write). The rollback issues
                // this number, hence this email, again, so an account left on it would block the next person with the
                // same abbreviation: it is found by its email and deleted. This runs before the rollback, while this
                // transaction still locks the number, so no other request can hold this email yet (it was free just
                // above); no statement failed, so the transaction can still query.
                await DeleteUnlinkedAccountOfEmailAsync(identity.Email);
                await RollbackQuietlyAsync(transaction);
                throw;
            }

            if (account is null)
            {
                throw AuthAccountExists();
            }

            var user = User.CreateStaff(
                new StaffAccountData(account.Uid, request.Name ?? "", identity.Email, identity.EmployeeCode, identity.Username,
                    request.JoinedDate, request.WorkPhone, MustChangePassword: true),
                roles, actorId, clock.UtcNow);
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(CancellationToken.None);
                await transaction.CommitAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                db.ChangeTracker.Clear();
                await RollbackQuietlyAsync(transaction);
                await DeleteUnlinkedAccountAsync(account.Uid);
                if (Translated(ex) is { } refusal)
                {
                    throw refusal;
                }

                throw;
            }

            userId = user.Id;
        }

        logger.LogInformation("Staff account {UserId} created by {ActorId} with roles {RoleIds}", userId, actorId,
            roles.Select(r => r.Id).ToList());
        return (await GetAsync(userId, CancellationToken.None))!;
    }

    /// <summary>The staff account <paramref name="id"/>; null when there is no user with a staff profile of that id.</summary>
    public Task<StaffDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.Id == id && u.StaffProfile != null)
            .Select(u => new StaffDto(
                u.Id,
                u.Name,
                u.Email,
                u.StaffProfile!.Username,
                u.StaffProfile.EmployeeCode,
                u.StaffProfile.JoinedDate,
                u.StaffProfile.LeftDate,
                u.StaffProfile.WorkPhone,
                u.Status,
                u.StaffProfile.MustChangePassword,
                db.Roles
                    .Where(r => u.UserRoles.Any(ur => ur.RoleId == r.Id))
                    .OrderBy(r => r.Name)
                    .Select(r => new StaffRoleDto(r.Id, r.Name))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>One page of the staff accounts (R-USR-13).</summary>
    public async Task<PagedDto<StaffListItemDto>> ListAsync(StaffListRequest request, CancellationToken cancellationToken)
    {
        await listValidator.ValidateAndThrowAsync(request, cancellationToken);

        var staff = db.Users.AsNoTracking().Where(u => u.StaffProfile != null && u.Status != UserStatus.Anonymized);
        if (request.Status is { } status)
        {
            var wanted = StaffListRequestValidator.Statuses[status];
            staff = staff.Where(u => u.Status == wanted);
        }

        if (request.RoleId is { } roleId)
        {
            staff = staff.Where(u => u.UserRoles.Any(ur => ur.RoleId == roleId));
        }

        var rows = Sorted(staff, request.Sort)
            .Select(u => new StaffListItemDto(
                u.Id,
                u.Name,
                u.Email,
                u.StaffProfile!.EmployeeCode,
                u.StaffProfile.JoinedDate,
                u.StaffProfile.LeftDate,
                u.Status,
                db.Roles
                    .Where(r => u.UserRoles.Any(ur => ur.RoleId == r.Id))
                    .OrderBy(r => r.Name)
                    .Select(r => new StaffRoleDto(r.Id, r.Name))
                    .ToList()));

        return await PagedQuery.ToPagedDtoAsync(rows, new PageRequest(request.Page, request.PageSize), cancellationToken);
    }

    /// <summary>
    /// The roles whose staff accounts <paramref name="actorId"/> may create, disable and restore (R-SEC-12): every role
    /// for a holder of Admin, otherwise the union of the manageable sets of the actor's roles, never a system role.
    /// </summary>
    public async Task<ManageableRoleSet> ManageableRolesOfAsync(Guid actorId, CancellationToken cancellationToken)
    {
        if (await HoldsAdminRoleAsync(actorId, cancellationToken))
        {
            return ManageableRoleSet.Every;
        }

        var ids = await db.RoleManageableRoles
            .Where(m => db.UserRoles.Any(ur => ur.UserId == actorId && ur.RoleId == m.RoleId)
                && !db.Roles.Any(r => r.Id == m.ManagedRoleId && r.IsSystemRole))
            .Select(m => m.ManagedRoleId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ManageableRoleSet.Of(ids);
    }

    /// <summary>
    /// One page of the staff roles <paramref name="actorId"/> may give a new account (R-SEC-12, R-USR-01), by name.
    /// </summary>
    public async Task<PagedDto<StaffRoleDto>> ListAssignableRolesAsync(Guid actorId, PageRequest page,
        CancellationToken cancellationToken)
    {
        await pageValidator.ValidateAndThrowAsync(page, cancellationToken);

        var manageable = await ManageableRolesOfAsync(actorId, cancellationToken);
        var roles = db.Roles.AsNoTracking().Where(r => r.ScopingType == ScopingType.None);
        if (!manageable.EveryRole)
        {
            var ids = manageable.RoleIds.ToList();
            roles = roles.Where(r => ids.Contains(r.Id));
        }

        return await PagedQuery.ToPagedDtoAsync(
            roles.OrderBy(r => r.Name).ThenBy(r => r.Id).Select(r => new StaffRoleDto(r.Id, r.Name)), page, cancellationToken);
    }

    /// <summary>
    /// <paramref name="userId"/> holds the Admin role, found by name like the R-SEC-02 trigger: the test of R-TX-11 (STF-01,
    /// STF-02), which only refuses, and of R-SEC-12, whose rule gives a holder of Admin every role to manage. It never
    /// grants a permission (R-SEC-03).
    /// </summary>
    public Task<bool> HoldsAdminRoleAsync(Guid userId, CancellationToken cancellationToken) =>
        db.UserRoles.AnyAsync(
            ur => ur.UserId == userId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == PermissionCatalog.AdminRoleName),
            cancellationToken);

    /// <summary>
    /// The order of <see cref="StaffListRequest.Sort"/>, one arm per value of <see cref="StaffListRequestValidator.Sorts"/>;
    /// the id last keeps pages stable. Names sort in Vietnamese order through the column collation, not here.
    /// </summary>
    private static IQueryable<User> Sorted(IQueryable<User> staff, string? sort) => sort switch
    {
        null or "name" => staff.OrderBy(u => u.Name).ThenBy(u => u.Id),
        "-name" => staff.OrderByDescending(u => u.Name).ThenBy(u => u.Id),
        "employeeCode" => staff.OrderBy(u => u.StaffProfile!.EmployeeCode == null)
            .ThenBy(u => u.StaffProfile!.EmployeeCode).ThenBy(u => u.Id),
        "-employeeCode" => staff.OrderBy(u => u.StaffProfile!.EmployeeCode == null)
            .ThenByDescending(u => u.StaffProfile!.EmployeeCode).ThenBy(u => u.Id),
        "joinedDate" => staff.OrderBy(u => u.StaffProfile!.JoinedDate == null)
            .ThenBy(u => u.StaffProfile!.JoinedDate).ThenBy(u => u.Id),
        "-joinedDate" => staff.OrderBy(u => u.StaffProfile!.JoinedDate == null)
            .ThenByDescending(u => u.StaffProfile!.JoinedDate).ThenBy(u => u.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(sort), "A sort accepted by the validator has no order."),
    };

    /// <summary>
    /// The roles of a new staff account, each rule checked in this order: at least one (R-SEC-01), all known, none
    /// scoped (R-USR-01), Admin only by an Admin (R-TX-11), all within what the actor manages (R-SEC-12).
    /// </summary>
    private async Task<List<Role>> AssignableRolesAsync(IEnumerable<Guid> requested, Guid actorId,
        CancellationToken cancellationToken)
    {
        var roleIds = requested.Distinct().ToList();
        if (roleIds.Count == 0)
        {
            throw new BusinessRuleException("R-SEC-01", "role-required", nameof(StaffCreateRequest.RoleIds));
        }

        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);
        var unknown = roleIds.Except(roles.Select(r => r.Id)).ToList();
        if (unknown.Count > 0)
        {
            throw UnknownRole();
        }

        if (roles.Any(r => r.ScopingType != ScopingType.None))
        {
            throw new BusinessRuleException("R-USR-01", "role-not-for-staff", nameof(StaffCreateRequest.RoleIds));
        }

        // R-TX-11 by the role name, like the R-SEC-02 trigger: it only refuses, so no access depends on a role name (R-SEC-03).
        if (roles.Any(r => r.Name == PermissionCatalog.AdminRoleName) && !await HoldsAdminRoleAsync(actorId, cancellationToken))
        {
            throw new BusinessRuleException("R-TX-11", "admin-role-not-assignable", nameof(StaffCreateRequest.RoleIds));
        }

        if (!(await ManageableRolesOfAsync(actorId, cancellationToken)).Covers(roleIds))
        {
            throw new BusinessRuleException("R-SEC-12", "role-out-of-scope", nameof(StaffCreateRequest.RoleIds));
        }

        return roles;
    }

    /// <summary>
    /// Rolls back after a failed save so that the compensation can query again (PostgreSQL refuses every statement in
    /// an aborted transaction). A rollback that fails (a broken connection) is logged and never replaces the original
    /// error nor skips the compensation; the server ends the transaction with the connection.
    /// </summary>
    private async Task RollbackQuietlyAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rollback of a failed staff creation failed");
        }
    }

    /// <summary>
    /// Compensation after a failed Firebase create: deletes the account Firebase may have created on
    /// <paramref name="email"/> unless a user row holds it. Best effort, as <see cref="DeleteUnlinkedAccountAsync"/>; a
    /// failure of the lookup is logged without the email (personal data, R-SEC-11).
    /// </summary>
    private async Task DeleteUnlinkedAccountOfEmailAsync(string email)
    {
        AuthAccount? left;
        try
        {
            left = await auth.FindByEmailAsync(email, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Firebase account of a failed staff creation could not be looked up: check the Firebase console");
            return;
        }

        if (left is not null)
        {
            await DeleteUnlinkedAccountAsync(left.Uid);
        }
    }

    /// <summary>
    /// Compensation: deletes the Firebase account created by this request unless a user row holds it. Best effort; a
    /// failure is logged with the uid only and the original error is kept.
    /// </summary>
    private async Task DeleteUnlinkedAccountAsync(string uid)
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

    /// <summary>A role id that names no role (400); the id is never written back.</summary>
    private static ValidationException UnknownRole() =>
        new([new ValidationFailure(nameof(StaffCreateRequest.RoleIds), RoleNotFound) { ErrorCode = RoleNotFound }]);

    /// <summary>
    /// The refusal a failed save stands for, or null: a unique email (a customer may hold the generated address), a unique
    /// employee code or username (a counter set back or a row written by hand; DEC-156 (4) keeps the years apart), or a
    /// role deleted (AUT-05) after the checks. No pointer: the request has no such field (R-USR-24).
    /// </summary>
    private static Exception? Translated(Exception ex) => ex switch
    {
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique } =>
            unique.ConstraintName is "ix_staff_profiles_employee_code" or "ix_staff_profiles_username"
                ? new BusinessRuleException(null, "employee-code-taken")
                : new BusinessRuleException(null, "email-already-registered"),
        DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "fk_user_roles_roles_role_id",
            },
        } => UnknownRole(),
        _ => null,
    };

    private static BusinessRuleException AuthAccountExists() => new(null, "auth-account-exists");

    /// <summary>The code of a role id that names no role (400 on <c>roleIds</c>, DEC-117 (2)); the id is never written back.</summary>
    public const string RoleNotFound = "ROLE_NOT_FOUND";
}
