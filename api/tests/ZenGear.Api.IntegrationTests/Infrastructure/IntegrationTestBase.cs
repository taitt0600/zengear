using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using ZenGear.Data;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// One API instance per test class; the database is migrated once and emptied by Respawn before every test.
/// </summary>
public abstract class IntegrationTestBase
{
    private Respawner? _respawner;

    protected ZenGearApiFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task StartApiAsync()
    {
        Factory = new ZenGearApiFactory();
        Client = Factory.CreateClient();

        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ZenGearDbContext>().Database.MigrateAsync();
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

        // Respawn fails when there is no table to clear: skip until the first migration adds one.
        if (_respawner is null && !await HasBusinessTablesAsync(connection))
        {
            return;
        }

        _respawner ??= await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
        await _respawner.ResetAsync(connection);
    }

    private static async Task<bool> HasBusinessTablesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory')",
            connection);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
