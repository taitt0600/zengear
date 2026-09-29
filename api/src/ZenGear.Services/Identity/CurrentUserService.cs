using Microsoft.EntityFrameworkCore;
using ZenGear.Data;

namespace ZenGear.Services.Identity;

public sealed class CurrentUserService(ZenGearDbContext db)
{
    public Task<CurrentUserDto> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new CurrentUserDto(
                u.Id,
                u.Name,
                u.Email,
                u.StaffProfile != null,
                u.StaffProfile != null && u.StaffProfile.MustChangePassword))
            .SingleAsync(cancellationToken);
}
