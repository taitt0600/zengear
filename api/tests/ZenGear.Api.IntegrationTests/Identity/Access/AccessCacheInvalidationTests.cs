using Microsoft.EntityFrameworkCore;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Services.Identity.Access.AccessCache;

namespace ZenGear.Api.IntegrationTests.Identity.Access;

public class AccessCacheInvalidationTests : IntegrationTestBase
{
    [Test]
    public async Task R_SEC_07_rollback_drops_pending_bumps()
    {
        var userId = await TestUsers.InsertAsync("staff-uid-1");
        var versionKey = RedisAccessCache.UserVersionKey(userId);

        await Factory.WithDbAsync(async db =>
        {
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var user = await db.Users.SingleAsync(u => u.Id == userId);
                db.Entry(user).Property(u => u.Status).CurrentValue = UserStatus.Disabled;
                await db.SaveChangesAsync();
                await transaction.RollbackAsync();
            }

            // A later commit of the same context must not carry the rolled-back bump.
            db.ChangeTracker.Clear();
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                await transaction.CommitAsync();
            }
        });

        (await ApiScope.VersionAsync(versionKey)).ShouldBe(0);
    }

    [Test]
    public async Task R_SEC_07_change_without_transaction_is_bumped_after_save()
    {
        var userId = await TestUsers.InsertAsync("staff-uid-1");

        await Factory.SetUserStatusAsync(userId, UserStatus.Disabled);

        (await ApiScope.VersionAsync(RedisAccessCache.UserVersionKey(userId))).ShouldBe(1);
    }
}
