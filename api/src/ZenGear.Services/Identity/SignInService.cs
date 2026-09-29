using Microsoft.EntityFrameworkCore;
using ZenGear.Data;

namespace ZenGear.Services.Identity;

public sealed class SignInService(ZenGearDbContext db)
{
    /// <summary>
    /// Records a new sign-in: sets <c>last_login_at</c> to <paramref name="authTime"/> only when it is
    /// later than the stored value, in one conditional UPDATE, so parallel first requests of a session
    /// record it once (R-SEC-11). <c>updated_at</c> is not touched: a sign-in is not an edit.
    /// </summary>
    /// <returns>True for the one request that recorded the sign-in.</returns>
    public async Task<bool> RecordSignInAsync(Guid userId, DateTimeOffset authTime, CancellationToken cancellationToken)
    {
        var updated = await db.Users
            .Where(u => u.Id == userId && (u.LastLoginAt == null || u.LastLoginAt < authTime))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, authTime), cancellationToken);
        return updated == 1;
    }
}
