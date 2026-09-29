using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>Keeps every log entry in memory so a test can assert what was logged.</summary>
public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public void Clear() => _entries.Clear();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose()
    {
    }

    public sealed record LogEntry(string Category, LogLevel Level, string? EventName, string Message,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(p => p.Key, p => p.Value)
                : [];
            entries.Enqueue(new LogEntry(category, logLevel, eventId.Name, formatter(state, exception), properties));
        }
    }
}
