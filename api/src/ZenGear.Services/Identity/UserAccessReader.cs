using Microsoft.EntityFrameworkCore;
using ZenGear.Data;

namespace ZenGear.Services.Identity;

public sealed class UserAccessReader(ZenGearDbContext db) : IUserAccessReader
{
    public Task<UserAccess?> GetAsync(string firebaseUid, CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.FirebaseUid == firebaseUid)
            .Select(u => new UserAccess(
                u.Id,
                u.Status,
                u.LastLoginAt,
                u.StaffProfile != null && u.StaffProfile.MustChangePassword))
            .SingleOrDefaultAsync(cancellationToken);
}
