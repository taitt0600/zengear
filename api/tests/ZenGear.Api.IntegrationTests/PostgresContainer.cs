using Npgsql;
using Testcontainers.PostgreSql;

namespace ZenGear.Api.IntegrationTests;

/// <summary>
/// One PostgreSQL 18 container for the whole test assembly. The database <c>zengear</c> is created
/// by <c>db/zengear_01_database.sql</c> (Vietnamese ICU collation, timezone UTC), as in every environment.
/// </summary>
[SetUpFixture]
public sealed class PostgresContainer
{
    private static PostgreSqlContainer? _container;

    public static string ConnectionString { get; private set; } = string.Empty;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _container = new PostgreSqlBuilder("postgres:18").Build();
        await _container.StartAsync();

        var serverDatabase = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = "postgres" };
        await RunScriptAsync(serverDatabase.ConnectionString, Path.Combine(AppContext.BaseDirectory, "db", "zengear_01_database.sql"));

        ConnectionString = new NpgsqlConnectionStringBuilder(serverDatabase.ConnectionString) { Database = "zengear" }.ConnectionString;
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Runs each statement on its own: CREATE DATABASE and DROP DATABASE cannot run inside a
    /// multi-statement batch or a transaction.
    /// </summary>
    private static async Task RunScriptAsync(string connectionString, string path)
    {
        var lines = await File.ReadAllLinesAsync(path);
        var sql = string.Join('\n', lines.Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var statement in statements)
        {
            await using var command = new NpgsqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
