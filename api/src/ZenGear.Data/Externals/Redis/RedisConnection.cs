using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ZenGear.Data.Externals.Redis;

/// <summary>
/// The one Redis connection of the process. It connects in the background on first use and never
/// blocks a caller: until connected, <see cref="TryGetDatabase"/> returns false and callers use
/// PostgreSQL. Nothing connects when the service is registered, so the build-time OpenAPI runs without Redis.
/// </summary>
public sealed class RedisConnection : IAsyncDisposable
{
    private readonly string? _connectionString;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisConnection> _logger;
    private readonly Lock _lock = new();
    private Task<IConnectionMultiplexer?>? _connecting;

    public RedisConnection(IConfiguration configuration, IOptions<RedisOptions> options, ILogger<RedisConnection> logger)
    {
        _connectionString = configuration.GetConnectionString("Redis");
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Raised when the connection comes back after a failure.</summary>
    public event Action? ConnectionRestored;

    /// <summary>Starts connecting if not started yet; returns false until connected.</summary>
    public bool TryGetDatabase(out IDatabase database)
    {
        var connecting = EnsureConnecting();
        if (connecting.IsCompletedSuccessfully && connecting.Result is { IsConnected: true } multiplexer)
        {
            database = multiplexer.GetDatabase();
            return true;
        }

        database = null!;
        return false;
    }

    /// <summary>For tests: behave as if the connection had just been restored.</summary>
    public void RaiseConnectionRestored() => ConnectionRestored?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_connecting is { IsCompletedSuccessfully: true } connecting && connecting.Result is { } multiplexer)
        {
            await multiplexer.DisposeAsync();
        }
    }

    private Task<IConnectionMultiplexer?> EnsureConnecting()
    {
        lock (_lock)
        {
            return _connecting ??= ConnectAsync();
        }
    }

    private async Task<IConnectionMultiplexer?> ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogWarning("ConnectionStrings:Redis is not set: the access cache is off and every request reads PostgreSQL");
            return null;
        }

        try
        {
            var configuration = ConfigurationOptions.Parse(_connectionString);
            configuration.AbortOnConnectFail = false;
            configuration.ConnectTimeout = _options.ConnectTimeoutMs;
            configuration.SyncTimeout = _options.CommandTimeoutMs;
            configuration.AsyncTimeout = _options.CommandTimeoutMs;

            var multiplexer = await ConnectionMultiplexer.ConnectAsync(configuration);
            multiplexer.ConnectionRestored += (_, _) => ConnectionRestored?.Invoke();
            if (!multiplexer.IsConnected)
            {
                // AbortOnConnectFail = false returns a client that keeps reconnecting: say so, without the connection
                // string, or the cache stays off without a trace.
                _logger.LogWarning("Redis is not reachable: the access cache is off and every request reads PostgreSQL until it connects");
            }

            return multiplexer;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Redis connection could not be created: the access cache is off");
            return null;
        }
    }
}
