using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZenGear.Data.Externals.Redis;

namespace ZenGear.Services.Identity.AccessCache;

/// <summary>
/// Retries the access cache bumps that failed (R-SEC-07), every second and as soon as one is added.
/// When the Redis connection comes back, the epoch is bumped too: entries written before the outage may
/// have missed a bump, so they are all dropped.
/// </summary>
public sealed class AccessBumpRetryJob(
    RedisConnection redis, RedisAccessCache cache, PendingAccessBumps pending, ILogger<AccessBumpRetryJob> logger)
    : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        redis.ConnectionRestored += OnConnectionRestored;
        redis.TryGetDatabase(out _); // start connecting
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await pending.WaitAsync(RetryInterval, stoppingToken);
                if (!pending.HasAny || !redis.TryGetDatabase(out _))
                {
                    continue;
                }

                var bumps = pending.Snapshot();
                try
                {
                    await cache.IncrementAsync(bumps.Select(b => b.Key).ToList());
                    pending.Remove(bumps);
                    logger.LogInformation("Access cache: {Count} pending version bump(s) applied", bumps.Count);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Access cache: {Count} version bump(s) failed again", bumps.Count);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping: cancellation ends the loop normally.
        }
        finally
        {
            redis.ConnectionRestored -= OnConnectionRestored;
        }
    }

    private void OnConnectionRestored()
    {
        logger.LogWarning("Redis connection restored: bumping the access cache epoch");
        pending.Add([AccessBump.Epoch]);
    }
}
