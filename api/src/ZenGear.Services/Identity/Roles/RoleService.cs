using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Services.Common;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Services.Identity.Roles;

/// <summary>
/// Roles, their permissions and manageable sets, for the role screen of the Admin (AUT-05, UC-002, UC-003). Grants change
/// through the change tracker, so the access cache is bumped on commit (R-SEC-03, R-SEC-07).
/// </summary>
public sealed class RoleService(
    ZenGearDbContext db,
    IPermissionCatalog catalog,
    IValidator<RoleCreateRequest> createValidator,
    IValidator<RoleUpdateRequest> updateValidator,
    IValidator<PageRequest> pageValidator,
    IClock clock)
{
    /// <summary>The code of a permission key that names no permission (400, DEC-117 (2)).</summary>
    public const string PermissionNotFound = "PERMISSION_NOT_FOUND";

    /// <summary>The code of a <c>role.*</c> key given to a role that is not a system role (400; owner's decision).</summary>
    public const string PermissionNotGrantable = "PERMISSION_NOT_GRANTABLE";

    /// <summary>The code of a role id that names no role (400); the id is never written back.</summary>
    public const string RoleNotFound = "ROLE_NOT_FOUND";

    /// <summary>One page of the roles by name (Vietnamese order through the column collation).</summary>
    public async Task<PagedDto<RoleListItemDto>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        await pageValidator.ValidateAndThrowAsync(page, cancellationToken);

        var roles = db.Roles.AsNoTracking()
            .OrderBy(r => r.Name).ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.Name, r.IsSystemRole, r.MaxDiscountPercent });
        return await PagedQuery.ToPagedDtoAsync(roles, page,
            r => new RoleListItemDto(r.Id, r.Name, r.IsSystemRole, IsDefault(r.Name, r.IsSystemRole), r.MaxDiscountPercent),
            cancellationToken);
    }

    /// <summary>The role <paramref name="id"/> with its grants and manageable set; null when there is none.</summary>
    public async Task<RoleDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        // Two collections: split, so that the grants and the managed roles do not multiply into one joined result.
        var role = await db.Roles.AsNoTracking().AsSplitQuery()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.IsSystemRole,
                r.MaxDiscountPercent,
                r.Version,
                Keys = db.RolePermissions.Where(rp => rp.RoleId == r.Id)
                    .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Key)
                    .ToList(),
                Managed = db.RoleManageableRoles.Where(m => m.RoleId == r.Id)
                    .Join(db.Roles, m => m.ManagedRoleId, managed => managed.Id, (_, managed) => managed)
                    .OrderBy(managed => managed.Name)
                    .Select(managed => new RoleManagedRoleDto(managed.Id, managed.Name))
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return role is null
            ? null
            : new RoleDto(role.Id, role.Name, role.IsSystemRole, IsDefault(role.Name, role.IsSystemRole), role.MaxDiscountPercent,
                [.. role.Keys.Order(StringComparer.Ordinal)], role.Managed, role.Version);
    }

    /// <summary>
    /// One page of the permissions of the code (DEC-095), by key: the database holds the same set once the sync has run.
    /// </summary>
    public PagedDto<PermissionDto> ListPermissions(PageRequest page)
    {
        pageValidator.ValidateAndThrow(page);

        var permissions = catalog.Permissions.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        return new PagedDto<PermissionDto>(
            [.. permissions.Skip(page.Skip).Take(page.PageSize).Select(p => new PermissionDto(p.Key, p.Description))],
            page.Page, page.PageSize, permissions.Count);
    }

    /// <summary>
    /// Creates a staff role with its permissions and discount (UC-002); it manages no role until the Admin grants it
    /// (R-SEC-12). Checks, in order: the input (400), the keys (400), the name is free (409).
    /// </summary>
    /// <param name="actorId">The signed-in Admin.</param>
    public async Task<RoleDto> CreateAsync(RoleCreateRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var permissionIds = await PermissionIdsAsync(request.PermissionKeys!, isSystemRole: false, cancellationToken);
        await EnsureNameFreeAsync(request.Name!, exceptId: null, cancellationToken);

        var role = Role.Create(request.Name!, request.MaxDiscountPercent, permissionIds, actorId, clock.UtcNow);
        db.Roles.Add(role);
        await SaveAsync(cancellationToken);
        return (await GetAsync(role.Id, cancellationToken))!;
    }

    /// <summary>
    /// Sets the name, permissions, discount and manageable set of the role (UC-003). Checks, in order: the input (400), the
    /// role exists (null), the version (412), the keys and roles exist (400), a system role changes only its discount
    /// (R-SEC-03), a default role keeps its name (DEC-095), the name is free, no system role is managed (R-SEC-12).
    /// </summary>
    /// <param name="expectedVersion">The <c>If-Match</c> version; null for <c>If-Match: *</c>.</param>
    /// <returns>The role after the change; null when there is no role <paramref name="id"/>.</returns>
    /// <exception cref="DbUpdateConcurrencyException">The role changed since <paramref name="expectedVersion"/>.</exception>
    public async Task<RoleDto?> UpdateAsync(Guid id, uint? expectedVersion, RoleUpdateRequest request, Guid actorId,
        CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var role = await db.Roles.Include(r => r.RolePermissions).Include(r => r.ManageableRoles)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        // The fifth copy of this check (BusinessLocationService, CurrentUserService, BrandService, ProductSeriesService),
        // kept until the Decision of docs/STATUS.md "before PRD-01" (2) gives it a shared place.
        if (expectedVersion is { } expected)
        {
            // A role with nothing to change issues no UPDATE, so the stored version is compared here as well as on save.
            if (role.Version != expected)
            {
                throw new DbUpdateConcurrencyException("The role changed since it was read.");
            }

            db.Entry(role).Property(r => r.Version).OriginalValue = expected;
        }

        var permissionIds = await PermissionIdsAsync(request.PermissionKeys!, role.IsSystemRole, cancellationToken);
        var managed = await ManagedRolesAsync(request.ManagedRoleIds!, cancellationToken);
        var name = request.Name!.Trim();

        if (role.IsSystemRole && (name != role.Name || managed.Count > 0
                || !permissionIds.ToHashSet().SetEquals(role.RolePermissions.Select(rp => rp.PermissionId))))
        {
            throw new BusinessRuleException("R-SEC-03", "system-role-not-editable");
        }

        if (name != role.Name)
        {
            if (IsDefault(role.Name, role.IsSystemRole))
            {
                throw new BusinessRuleException(null, "default-role-not-renamable", nameof(RoleUpdateRequest.Name));
            }

            await EnsureNameFreeAsync(name, role.Id, cancellationToken);
        }

        if (managed.Exists(r => r.IsSystemRole))
        {
            throw new BusinessRuleException("R-SEC-12", "role-not-manageable", nameof(RoleUpdateRequest.ManagedRoleIds));
        }

        if (role.Update(name, request.MaxDiscountPercent, permissionIds, managed.Select(r => r.Id), actorId, clock.UtcNow))
        {
            // The roles row is written even when only child rows changed (and the stamp holds the same values), so its
            // version moves and a concurrent update with the old ETag gets 412.
            db.Entry(role).Property(r => r.UpdatedAt).IsModified = true;
        }

        await SaveAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Deletes a role no user holds, with its grants and manageable pairs (UC-003). A system or default role is never
    /// deleted (DEC-095); a role any account holds, whatever its status, is refused (UC-003 3b, R-SEC-01).
    /// </summary>
    /// <returns>False when there is no role <paramref name="id"/>.</returns>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return false;
        }

        if (role.IsSystemRole || IsDefault(role.Name, role.IsSystemRole))
        {
            throw new BusinessRuleException(null, "role-not-deletable");
        }

        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == id, cancellationToken))
        {
            throw RoleInUse();
        }

        db.Roles.Remove(role);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // A staff account given this role after the check above.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "fk_user_roles_roles_role_id",
        })
        {
            throw RoleInUse();
        }

        return true;
    }

    /// <summary>A default role is a staff role of the catalog: found by name, which is why it is never renamed (DEC-095).</summary>
    private bool IsDefault(string name, bool isSystemRole) =>
        !isSystemRole && catalog.Roles.Any(g => !g.IsSystemRole && g.RoleName == name);

    /// <summary>
    /// The ids of <paramref name="keys"/>, a key given twice counted once. Refuses (400) a key no permission has, and a
    /// <c>role.*</c> key for a role that is not a system role.
    /// </summary>
    private async Task<List<Guid>> PermissionIdsAsync(IReadOnlyList<string> keys, bool isSystemRole,
        CancellationToken cancellationToken)
    {
        var wanted = keys.Distinct(StringComparer.Ordinal).ToList();
        var found = await db.Permissions.Where(p => wanted.Contains(p.Key))
            .Select(p => new { p.Key, p.Id })
            .ToListAsync(cancellationToken);
        if (found.Count != wanted.Count)
        {
            throw Invalid(nameof(RoleCreateRequest.PermissionKeys), PermissionNotFound);
        }

        if (!isSystemRole && wanted.Exists(PermissionCatalog.IsAdminOnly))
        {
            throw Invalid(nameof(RoleCreateRequest.PermissionKeys), PermissionNotGrantable);
        }

        return [.. found.Select(p => p.Id)];
    }

    /// <summary>The roles of <paramref name="ids"/>, an id given twice counted once; refuses (400) an id no role has.</summary>
    private async Task<List<Role>> ManagedRolesAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();
        var roles = await db.Roles.AsNoTracking().Where(r => wanted.Contains(r.Id)).ToListAsync(cancellationToken);
        return roles.Count == wanted.Count ? roles : throw Invalid(nameof(RoleUpdateRequest.ManagedRoleIds), RoleNotFound);
    }

    /// <summary>
    /// Refuses a name another role has, whatever the case. The unique index <c>ix_roles_name</c> is case-sensitive, so on
    /// save it only catches a parallel request with the same spelling.
    /// </summary>
    private async Task EnsureNameFreeAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var lower = name.Trim().ToLowerInvariant();
        if (await db.Roles.AnyAsync(r => r.Id != exceptId && r.Name.ToLower() == lower, cancellationToken))
        {
            throw NameTaken();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ix_roles_name",
        })
        {
            throw NameTaken();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation,
            ConstraintName: "fk_role_manageable_roles_roles_managed_role_id",
        })
        {
            // A managed role deleted after ManagedRolesAsync found it: the same answer as an id no role has.
            throw Invalid(nameof(RoleUpdateRequest.ManagedRoleIds), RoleNotFound);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "pk_role_permissions" or "pk_role_manageable_roles",
        })
        {
            // A parallel update of the same role added the same pair first: the role changed since it was read (412).
            throw new DbUpdateConcurrencyException("The role changed since it was read.", ex);
        }
    }

    private static ValidationException Invalid(string property, string code) =>
        new([new ValidationFailure(property, code) { ErrorCode = code }]);

    private static BusinessRuleException NameTaken() =>
        new(null, "role-already-exists", nameof(RoleCreateRequest.Name));

    private static BusinessRuleException RoleInUse() => new(null, "role-in-use");
}
