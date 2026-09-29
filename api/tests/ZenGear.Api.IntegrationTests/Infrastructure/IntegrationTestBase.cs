using Npgsql;
using Respawn;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One API instance per test class. The database (migrated once by <see cref="PostgresContainer"/>) is emptied
/// by Respawn and Redis is flushed before every test.
/// </summary>
public abstract class IntegrationTestBase
{
    private Respawner? _respawner;

    protected ZenGearApiFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    /// <summary>Override to start the API with another factory (e.g. Redis unreachable).</summary>
    protected virtual ZenGearApiFactory CreateFactory() => new();

    [OneTimeSetUp]
    public void StartApi()
    {
        Factory = CreateFactory();
        Client = Factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task StopApiAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    [SetUp]
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        await connection.OpenAsync();

        _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
        await _respawner.ResetAsync(connection);
        await RedisContainer.FlushAsync();
    }
}
