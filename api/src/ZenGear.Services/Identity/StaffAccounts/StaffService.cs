using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
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
    IValidator<StaffCreateRequest> validator,
    IValidator<StaffListRequest> listValidator,
    IClock clock,
    ILogger<StaffService> logger)
{
    /// <summary>
    /// Creates a staff account with the default password and its roles (UC-005). Every rule is checked before the
    /// Firebase account is created, so a refused request creates nothing. Once that account exists the request token is
    /// no longer used: a client that disconnects cannot leave an account without its user row.
    /// </summary>
    /// <param name="actorId">The signed-in staff member creating the account.</param>
    public async Task<StaffDto> CreateAsync(StaffCreateRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var roleIds = request.RoleIds.Distinct().ToList();
        if (roleIds.Count == 0)
        {
            throw new BusinessRuleException("R-SEC-01", "role-required", "Role required",
                "A staff account needs at least one role.");
        }

        var roles = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken);
        var unknown = roleIds.Except(roles.Select(r => r.Id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationException(unknown.Select(id =>
                new ValidationFailure(nameof(StaffCreateRequest.RoleIds), $"Role {id} does not exist.")));
        }

        if (roles.Any(r => r.ScopingType != ScopingType.None))
        {
            throw new BusinessRuleException("R-USR-01", "role-not-for-staff", "Role not for staff",
                "A staff account holds internal roles only; the customer role belongs to customer accounts.");
        }

        // R-TX-11 by the role name, like the R-SEC-02 trigger: it only refuses, so no access depends on a role name (R-SEC-03).
        if (roles.Any(r => r.Name == PermissionCatalog.AdminRoleName) && !await HoldsAdminRoleAsync(actorId, cancellationToken))
        {
            throw new BusinessRuleException("R-TX-11", "admin-role-not-assignable", "Admin role not assignable",
                "Only an Admin can give the Admin role.");
        }

        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw EmailAlreadyRegistered();
        }

        if (await db.StaffProfiles.AnyAsync(s => s.EmployeeCode == request.EmployeeCode, cancellationToken))
        {
            throw EmployeeCodeTaken();
        }

        // An account that already exists is never linked: whoever registered the email first would keep its password,
        // other sign-in methods and live ID tokens. The race with a parallel request ends the same way.
        if (await auth.FindByEmailAsync(email, cancellationToken) is not null)
        {
            throw AuthAccountExists();
        }

        // From here the request token is no longer used: cancelled during the call, Firebase could still create the
        // account and nothing would delete it.
        var account = await auth.CreateWithDefaultStaffPasswordAsync(email, CancellationToken.None) ?? throw AuthAccountExists();

        var user = User.CreateStaff(
            new StaffAccountData(account.Uid, request.Name, email, request.EmployeeCode, request.JoinedDate,
                request.WorkPhone, MustChangePassword: true),
            roles, actorId, clock.UtcNow);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            await DeleteUnlinkedAccountAsync(account.Uid);
            if (ex is not DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique })
            {
                throw;
            }

            throw unique.ConstraintName == "ix_staff_profiles_employee_code" ? EmployeeCodeTaken() : EmailAlreadyRegistered();
        }

        logger.LogInformation("Staff account {UserId} created by {ActorId} with roles {RoleIds}", user.Id, actorId, roleIds);
        return (await GetAsync(user.Id, CancellationToken.None))!;
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
                u.StaffProfile!.EmployeeCode,
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

        var page = new PageRequest(request.Page, request.PageSize);
        var items = await Sorted(staff, request.Sort)
            .Skip(page.Skip)
            .Take(page.PageSize)
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
                    .ToList()))
            .ToListAsync(cancellationToken);

        // A first page that is not full holds every row: no COUNT (a household business has fewer staff than a page).
        var total = page.Page == 1 && items.Count < page.PageSize ? items.Count : await staff.CountAsync(cancellationToken);
        return new PagedDto<StaffListItemDto>(items, page.Page, page.PageSize, total);
    }

    /// <summary>
    /// <paramref name="userId"/> holds the Admin role, found by name like the R-SEC-02 trigger: the one test of R-TX-11
    /// (STF-01, STF-02). It only refuses; holding the role never grants access (R-SEC-03).
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

    private static BusinessRuleException EmailAlreadyRegistered() =>
        new(null, "email-already-registered", "Email already registered", "Another account already uses this email.");

    private static BusinessRuleException EmployeeCodeTaken() =>
        new(null, "employee-code-taken", "Employee code taken", "Another staff account already has this employee code.");

    private static BusinessRuleException AuthAccountExists() =>
        new(null, "auth-account-exists", "Sign-in account exists",
            "A Firebase sign-in account already exists for this email without a ZenGear account. Delete it in the Firebase console first.");
}
