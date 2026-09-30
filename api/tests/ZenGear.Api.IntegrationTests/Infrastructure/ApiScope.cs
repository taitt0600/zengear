using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ZenGear.Data;
using ZenGear.Data.Entities.Identity.Enums;
using ZenGear.Services.Identity.AccessCache;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>Changes made the way services make them: through a <see cref="ZenGearDbContext"/> of the API host.</summary>
public static class ApiScope
{
    public static async Task WithDbAsync(this ZenGearApiFactory factory, Func<ZenGearDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<ZenGearDbContext>());
    }

    /// <summary>Sets <c>users.status</c> through the change tracker, as the future STF-02 service will.</summary>
    public static Task SetUserStatusAsync(this ZenGearApiFactory factory, Guid userId, UserStatus status) =>
        factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            db.Entry(user).Property(u => u.Status).CurrentValue = status;
            await db.SaveChangesAsync();
        });

    public static IAccessCache AccessCache(this ZenGearApiFactory factory) =>
        factory.Services.GetRequiredService<IAccessCache>();

    public static async Task<long> VersionAsync(RedisKey key)
    {
        var value = await RedisContainer.Database.StringGetAsync(key);
        return value.HasValue ? (long)value : 0;
    }

    /// <summary>Waits until the retry job has applied every pending bump.</summary>
    public static async Task WaitForPendingBumpsAsync(this ZenGearApiFactory factory)
    {
        var pending = factory.Services.GetRequiredService<PendingAccessBumps>();
        for (var i = 0; i < 100 && pending.HasAny; i++)
        {
            await Task.Delay(50);
        }

        if (pending.HasAny)
        {
            Assert.Fail("Pending access cache bumps were not applied within 5 seconds.");
        }
    }
}
