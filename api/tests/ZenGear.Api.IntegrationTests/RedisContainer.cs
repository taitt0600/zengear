using StackExchange.Redis;
using Testcontainers.Redis;

namespace ZenGear.Api.IntegrationTests;

/// <summary>One Redis container for the whole test assembly, run with the eviction policy required by the access cache.</summary>
[SetUpFixture]
public sealed class RedisContainer
{
    private static Testcontainers.Redis.RedisContainer? _container;
    private static ConnectionMultiplexer? _admin;

    public static string ConnectionString { get; private set; } = string.Empty;

    /// <summary>A direct connection for tests that read or write cache keys.</summary>
    public static IDatabase Database => _admin!.GetDatabase();

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _container = new RedisBuilder("redis:7.4-alpine")
            .WithCommand("--maxmemory-policy", "volatile-lru")
            .Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        _admin = await ConnectionMultiplexer.ConnectAsync($"{ConnectionString},allowAdmin=true");
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (_admin is not null)
        {
            await _admin.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public static Task FlushAsync() => _admin!.GetServers()[0].FlushDatabaseAsync();
}
