using System.Collections.Concurrent;

namespace ZenGear.Services.Identity.Access.AccessCache;

/// <summary>
/// Bumps that could not reach Redis. While any is pending, <see cref="UserAccessReader"/> reads PostgreSQL
/// only, so this instance never serves an entry that should have been invalidated; <c>AccessBumpRetryJob</c>
/// retries them every second and when the connection comes back.
/// A bump stays pending until its INCR has succeeded: removing it before would open a window where the
/// cache is read although the version is still old.
/// </summary>
public sealed class PendingAccessBumps
{
    // Value = generation: a bump added again while a retry is in flight is not removed by that retry.
    private readonly ConcurrentDictionary<AccessBump, long> _bumps = new();
    private readonly SemaphoreSlim _signal = new(0);
    private long _generation;

    public bool HasAny => !_bumps.IsEmpty;

    public void Add(IEnumerable<AccessBump> bumps)
    {
        var generation = Interlocked.Increment(ref _generation);
        foreach (var bump in bumps)
        {
            _bumps[bump] = generation;
        }

        _signal.Release();
    }

    /// <summary>The pending bumps, left in place until <see cref="Remove"/>.</summary>
    public IReadOnlyList<KeyValuePair<AccessBump, long>> Snapshot() => [.. _bumps];

    /// <summary>Removes the bumps applied from <paramref name="applied"/>, unless added again since.</summary>
    public void Remove(IEnumerable<KeyValuePair<AccessBump, long>> applied)
    {
        foreach (var bump in applied)
        {
            _bumps.TryRemove(bump);
        }
    }

    /// <summary>Waits until a bump is added or <paramref name="timeout"/> passes.</summary>
    public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _signal.WaitAsync(timeout, cancellationToken);
}
