using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Identity;

/// <summary>
/// The triggers on <c>user_roles</c> are DEFERRABLE INITIALLY DEFERRED: every test commits its
/// transaction and expects the error at COMMIT.
/// </summary>
public class UserRoleTriggerTests : IntegrationTestBase
{
    [Test]
    public async Task R_SEC_01_deleting_the_last_role_of_a_user_is_rejected()
    {
        var userId = await TestUsers.InsertAsync("uid-staff");
        var roleId = await TestRoles.InsertAsync("Cashier");
        await TestRoles.AssignAsync(userId, roleId);

        var error = await Should.ThrowAsync<PostgresException>(() => DeleteUserRolesAsync(
            "DELETE FROM user_roles WHERE user_id = @user", ("user", userId)));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.MessageText.ShouldStartWith("R-SEC-01");
    }

    [Test]
    public async Task R_SEC_01_removing_one_of_two_roles_is_allowed()
    {
        var userId = await TestUsers.InsertAsync("uid-staff");
        var cashier = await TestRoles.InsertAsync("Cashier");
        var sales = await TestRoles.InsertAsync("Sales Consultant");
        await TestRoles.AssignAsync(userId, cashier);
        await TestRoles.AssignAsync(userId, sales);

        await DeleteUserRolesAsync("DELETE FROM user_roles WHERE user_id = @user AND role_id = @role",
            ("user", userId), ("role", cashier));

        (await CountUserRolesAsync(userId)).ShouldBe(1);
    }

    [Test]
    public async Task R_SEC_01_deleting_a_user_cascades_its_roles()
    {
        // fn_user_roles_min_one checks IF FOUND: the user row is gone, so the cascade is allowed.
        var userId = await TestUsers.InsertAsync("uid-staff");
        var roleId = await TestRoles.InsertAsync("Cashier");
        await TestRoles.AssignAsync(userId, roleId);

        await DeleteUserRolesAsync("DELETE FROM users WHERE id = @user", ("user", userId));

        (await CountUserRolesAsync(userId)).ShouldBe(0);
    }

    [Test]
    public async Task R_SEC_02_removing_the_last_active_admin_is_rejected()
    {
        var adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        var cashier = await TestRoles.InsertAsync("Cashier");
        var activeAdmin = await TestUsers.InsertAsync("uid-admin");
        var disabledAdmin = await TestUsers.InsertAsync("uid-disabled-admin", status: "DISABLED");
        foreach (var user in new[] { activeAdmin, disabledAdmin })
        {
            await TestRoles.AssignAsync(user, adminRole);
            await TestRoles.AssignAsync(user, cashier);
        }

        var error = await Should.ThrowAsync<PostgresException>(() => DeleteUserRolesAsync(
            "DELETE FROM user_roles WHERE user_id = @user AND role_id = @role", ("user", activeAdmin), ("role", adminRole)));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.MessageText.ShouldStartWith("R-SEC-02");
    }

    [Test]
    public async Task R_SEC_02_removing_an_admin_while_another_active_admin_remains_is_allowed()
    {
        var adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        var cashier = await TestRoles.InsertAsync("Cashier");
        var first = await TestUsers.InsertAsync("uid-admin-1");
        var second = await TestUsers.InsertAsync("uid-admin-2");
        foreach (var user in new[] { first, second })
        {
            await TestRoles.AssignAsync(user, adminRole);
            await TestRoles.AssignAsync(user, cashier);
        }

        await DeleteUserRolesAsync("DELETE FROM user_roles WHERE user_id = @user AND role_id = @role",
            ("user", first), ("role", adminRole));

        (await CountUserRolesAsync(first)).ShouldBe(1);
    }

    private static async Task DeleteUserRolesAsync(string sql, params (string Name, Guid Value)[] parameters)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }

            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task<long> CountUserRolesAsync(Guid userId)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM user_roles WHERE user_id = @user", connection);
        command.Parameters.AddWithValue("user", userId);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
