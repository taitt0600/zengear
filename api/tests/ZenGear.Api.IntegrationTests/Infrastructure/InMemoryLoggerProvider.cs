using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>Keeps every log entry in memory so a test can assert what was logged.</summary>
public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly List<(Func<LogEntry, bool> Match, TaskCompletionSource Done)> _waiters = [];

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public void Clear() => _entries.Clear();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Add);

    /// <summary>Completes when an entry matching <paramref name="match"/> is logged (or already was).</summary>
    public Task WhenLoggedAsync(Func<LogEntry, bool> match)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_waiters)
        {
            if (_entries.Any(match))
            {
                return Task.CompletedTask;
            }

            _waiters.Add((match, done));
        }

        return done.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private void Add(LogEntry entry)
    {
        lock (_waiters)
        {
            _entries.Enqueue(entry);
            foreach (var waiter in _waiters.Where(w => w.Match(entry)).ToList())
            {
                waiter.Done.TrySetResult();
                _waiters.Remove(waiter);
            }
        }
    }

    public void Dispose()
    {
    }

    public sealed record LogEntry(string Category, LogLevel Level, string? EventName, string Message,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class Logger(string category, Action<LogEntry> add) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(p => p.Key, p => p.Value)
                : [];
            add(new LogEntry(category, logLevel, eventId.Name, formatter(state, exception), properties));
        }
    }
}
