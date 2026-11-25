using Npgsql;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Inserts users with plain SQL: creating accounts is a feature of its own (STF-01, AUT-02),
/// so the entities offer no way to do it yet.
/// </summary>
public static class TestUsers
{
    /// <summary>
    /// The numbers of the employee codes of the users inserted here: ZG99 + 4 digits, a year no test creates an account in,
    /// so they never meet the codes the API issues (R-USR-24).
    /// </summary>
    private static int _lastNumber;

    /// <summary>A fresh employee code and username for a staff row inserted by SQL (R-USR-24: both NOT NULL or unique).</summary>
    public static (string EmployeeCode, string Username) NextIdentity()
    {
        var number = Interlocked.Increment(ref _lastNumber) % 10000;
        var code = $"ZG99{number:0000}";
        return (code, "TestA" + code);
    }

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
                """
                INSERT INTO staff_profiles (user_id, employee_code, username, must_change_password)
                VALUES (@id, @code, @username, @mustChangePassword)
                """,
                connection);
            var (code, username) = NextIdentity();
            insertStaff.Parameters.AddWithValue("id", id);
            insertStaff.Parameters.AddWithValue("code", code);
            insertStaff.Parameters.AddWithValue("username", username);
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
