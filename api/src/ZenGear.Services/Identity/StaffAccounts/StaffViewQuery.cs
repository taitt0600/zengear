using ZenGear.Data;
using ZenGear.Data.Entities.Identity;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// The one projection of a staff account for the list and for its page (DEC-138: the same code for both), with the
/// PENDING facts worked out in the same SQL query, never one query per row (DEC-137 (1)): whether the viewer may approve
/// (R-USR-17, R-SEC-12) or withdraw it (DEC-140 (1)), who created it and when.
/// </summary>
public sealed class StaffViewQuery(ZenGearDbContext db)
{
    /// <param name="manageable">The viewer's manageable set (R-SEC-12), worked out once for the request; null when the
    /// viewer does not hold <c>staff.approve</c>, since then no row may be approved.</param>
    internal IQueryable<StaffRow> Project(IQueryable<User> staff, StaffViewer viewer, ManageableRoleSet? manageable)
    {
        var mayApprove = viewer.MayApprove && manageable is not null;
        var everyRole = manageable?.EveryRole ?? false;
        var managed = manageable is { EveryRole: false } set ? set.RoleIds.ToList() : [];
        var viewerId = viewer.Id;
        var mayCreate = viewer.MayCreate;

        return staff.Select(u => new StaffRow
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Username = u.StaffProfile!.Username,
            EmployeeCode = u.StaffProfile.EmployeeCode,
            JoinedDate = u.StaffProfile.JoinedDate,
            LeftDate = u.StaffProfile.LeftDate,
            WorkPhone = u.StaffProfile.WorkPhone,
            Status = u.Status,
            MustChangePassword = u.StaffProfile.MustChangePassword,
            Roles = db.Roles
                .Where(r => u.UserRoles.Any(ur => ur.RoleId == r.Id))
                .OrderBy(r => r.Name)
                .Select(r => new StaffRoleDto(r.Id, r.Name))
                .ToList(),
            CanApprove = u.Status == UserStatus.Pending
                ? mayApprove
                    && u.StaffProfile.CreatedBy != viewerId
                    && (everyRole || u.UserRoles.All(ur => managed.Contains(ur.RoleId)))
                : null,
            CanWithdraw = u.Status == UserStatus.Pending ? mayCreate && u.StaffProfile.CreatedBy == viewerId : null,
            CreatedBy = u.Status == UserStatus.Pending
                ? db.Users
                    .Where(c => c.Id == u.StaffProfile.CreatedBy && c.Status != UserStatus.Anonymized)
                    .Select(c => new StaffCreatorDto(c.Id, c.Name))
                    .FirstOrDefault()
                : null,
            CreatedAt = u.Status == UserStatus.Pending ? u.StaffProfile.CreatedAt : null,
            Version = u.Version,
        });
    }
}
