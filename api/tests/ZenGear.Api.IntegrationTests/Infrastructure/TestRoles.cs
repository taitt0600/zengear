using Npgsql;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Inserts roles, permissions and assignments with plain SQL: role management is a feature of its own (AUT-05).
/// </summary>
public static class TestRoles
{
    public static async Task<Guid> InsertAsync(string name, string scopingType = "NONE", bool isSystemRole = false)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO roles (name, scoping_type, is_system_role) VALUES (@name, @scoping, @system) RETURNING id",
            connection);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("scoping", scopingType);
        command.Parameters.AddWithValue("system", isSystemRole);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    public static async Task AssignAsync(Guid userId, Guid roleId)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO user_roles (user_id, role_id) VALUES (@user, @role)", connection);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("role", roleId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Grants <paramref name="permissionKey"/> to the role, creating the permission when missing.</summary>
    public static async Task GrantAsync(Guid roleId, string permissionKey)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            WITH p AS (
              INSERT INTO permissions (key) VALUES (@key)
              ON CONFLICT (key) DO UPDATE SET key = EXCLUDED.key
              RETURNING id)
            INSERT INTO role_permissions (role_id, permission_id) SELECT @role, id FROM p
            """,
            connection);
        command.Parameters.AddWithValue("role", roleId);
        command.Parameters.AddWithValue("key", permissionKey);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
