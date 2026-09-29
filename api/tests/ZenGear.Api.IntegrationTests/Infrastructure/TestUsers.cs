using Npgsql;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Inserts users with plain SQL: creating accounts is a feature of its own (STF-01, AUT-02),
/// so the entities offer no way to do it yet.
/// </summary>
public static class TestUsers
{
    public static async Task<Guid> InsertAsync(string firebaseUid, string status = "ACTIVE", bool isStaff = true,
        bool mustChangePassword = false, DateTimeOffset? lastLoginAt = null)
    {
        await using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        await connection.OpenAsync();

        await using var insertUser = new NpgsqlCommand(
            """
            INSERT INTO users (firebase_uid, name, email, status, last_login_at)
            VALUES (@uid, 'Nguyễn Văn A', @uid || '@zengear.test', @status, @lastLoginAt)
            RETURNING id
            """,
            connection);
        insertUser.Parameters.AddWithValue("uid", firebaseUid);
        insertUser.Parameters.AddWithValue("status", status);
        insertUser.Parameters.AddWithValue("lastLoginAt", (object?)lastLoginAt ?? DBNull.Value);
        var id = (Guid)(await insertUser.ExecuteScalarAsync())!;

        if (isStaff)
        {
            await using var insertStaff = new NpgsqlCommand(
                "INSERT INTO staff_profiles (user_id, must_change_password) VALUES (@id, @mustChangePassword)",
                connection);
            insertStaff.Parameters.AddWithValue("id", id);
            insertStaff.Parameters.AddWithValue("mustChangePassword", mustChangePassword);
            await insertStaff.ExecuteNonQueryAsync();
        }

        return id;
    }

    public static async Task<DateTimeOffset?> LastLoginAtAsync(Guid id)
    {
        await using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT last_login_at FROM users WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        var value = await command.ExecuteScalarAsync();
        return value is DateTime utc ? new DateTimeOffset(utc, TimeSpan.Zero) : null;
    }
}
