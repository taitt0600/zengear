using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Identity.StaffAccounts;

/// <summary>POST /api/v1/staff/{id}/disable and /restore (STF-02) against PostgreSQL, with Firebase Authentication faked.</summary>
public class StaffStatusTests : IntegrationTestBase
{
    private const string AdminUid = "uid-admin";
    private const string ManagerUid = "uid-manager";
    private const string StaffUid = "uid-staff";
    private static readonly DateOnly JoinedDate = new(2026, 10, 1);

    /// <summary>10:00 on 2026-11-30 in Asia/Ho_Chi_Minh: the left dates of these tests are in the past (R-USR-07).</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 30, 3, 0, 0, TimeSpan.Zero);

    private Guid _adminRole;
    private Guid _managerRole;
    private Guid _cashierRole;
    private Guid _adminId;
    private Guid _managerId;
    private Guid _staffId;

    private FakeAuthProvider Auth => Factory.Auth;

    [SetUp]
    public async Task SeedAsync()
    {
        Auth.Reset();
        Factory.StaffLogs.Clear();
        Factory.Time.AdjustTime(Now);

        _adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        _managerRole = await TestRoles.InsertAsync("Store Manager");
        _cashierRole = await TestRoles.InsertAsync("Cashier");
        foreach (var role in new[] { _adminRole, _managerRole })
        {
            await TestRoles.GrantAsync(role, PermissionKeys.StaffDisable);
            await TestRoles.GrantAsync(role, PermissionKeys.StaffRead);
        }

        _adminId = await TestUsers.InsertAsync(AdminUid);
        await TestRoles.AssignAsync(_adminId, _adminRole);
        _managerId = await TestUsers.InsertAsync(ManagerUid);
        await TestRoles.AssignAsync(_managerId, _managerRole);
        _staffId = await TestUsers.InsertAsync(StaffUid);
        await TestRoles.AssignAsync(_staffId, _cashierRole);
        await ExecuteAsync("UPDATE staff_profiles SET joined_date = @joined WHERE user_id = @id", _staffId, JoinedDate);
    }

    [Test]
    public async Task R_USR_07_admin_disables_with_left_date_in_one_operation()
    {
        var response = await DisableAsync(AdminUid, _staffId, new { leftDate = "2026-10-31" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonAsync(response);
        body.GetProperty("id").GetGuid().ShouldBe(_staffId);
        body.GetProperty("status").GetString().ShouldBe("DISABLED");
        body.GetProperty("leftDate").GetString().ShouldBe("2026-10-31");
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("DISABLED", new DateOnly(2026, 10, 31), _adminId));
        Auth.Revoked.ShouldBe([StaffUid]);
    }

    [Test]
    public async Task R_USR_07_left_date_before_joined_date_is_409_and_changes_nothing()
    {
        var response = await DisableAsync(AdminUid, _staffId, new { leftDate = "2026-09-30" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/left-date-before-joined-date", "R-USR-07");
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("ACTIVE", null, null));
        Auth.Revoked.ShouldBeEmpty();
    }

    [Test]
    public async Task R_USR_07_database_check_refuses_left_date_before_joined_date()
    {
        var update = ExecuteAsync("UPDATE staff_profiles SET left_date = @date WHERE user_id = @id", _staffId,
            JoinedDate.AddDays(-1));

        var error = await Should.ThrowAsync<PostgresException>(update);
        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe("ck_staff_profiles_left_date");
    }

    [Test]
    public async Task R_SEC_04_disable_without_left_date_keeps_left_date_null_and_revokes_the_sessions()
    {
        var response = await DisableAsync(AdminUid, _staffId, new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("leftDate").ValueKind.ShouldBe(JsonValueKind.Null);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("DISABLED", null, _adminId));
        Auth.Revoked.ShouldBe([StaffUid]);
    }

    [Test]
    public async Task R_SEC_04_disable_without_a_body_disables_and_revokes()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/staff/{_staffId}/disable", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.EmulatorToken(AdminUid));

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("DISABLED", null, _adminId));
        Auth.Revoked.ShouldBe([StaffUid]);
    }

    [Test]
    public async Task R_SEC_04_a_repeated_disable_revokes_again_and_writes_nothing()
    {
        (await DisableAsync(AdminUid, _staffId, new { leftDate = "2026-10-31" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var version = await VersionAsync(_staffId);

        var response = await DisableAsync(ManagerUid, _staffId, new { leftDate = "2026-10-31" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await VersionAsync(_staffId)).ShouldBe(version);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("DISABLED", new DateOnly(2026, 10, 31), _adminId));
        Auth.Revoked.ShouldBe([StaffUid, StaffUid]);
    }

    [Test]
    public async Task R_SEC_04_disabled_account_is_refused_on_its_next_request()
    {
        (await MeAsync(StaffUid)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await DisableAsync(AdminUid, _staffId, new { })).StatusCode.ShouldBe(HttpStatusCode.OK);

        await (await MeAsync(StaffUid)).ShouldBeForbiddenProblemAsync("/problems/account-disabled", "R-SEC-04");
    }

    [Test]
    public async Task R_SEC_04_a_failed_revoke_is_500_and_a_repeat_revokes()
    {
        Auth.FailRevoke = true;

        var failed = await DisableAsync(AdminUid, _staffId, new { leftDate = "2026-10-31" });

        failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await RowAsync(_staffId)).Status.ShouldBe("DISABLED");
        Auth.FailRevoke = false;

        var repeated = await DisableAsync(AdminUid, _staffId, new { });

        repeated.StatusCode.ShouldBe(HttpStatusCode.OK);
        Auth.Revoked.ShouldBe([StaffUid]);
        (await RowAsync(_staffId)).LeftDate.ShouldBe(new DateOnly(2026, 10, 31));
    }

    [Test]
    public async Task R_USR_03_disable_twice_records_the_later_left_date()
    {
        (await DisableAsync(AdminUid, _staffId, new { })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await DisableAsync(AdminUid, _staffId, new { leftDate = "2026-11-15" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("DISABLED", new DateOnly(2026, 11, 15), _adminId));
        Auth.Revoked.ShouldBe([StaffUid, StaffUid]);
    }

    [Test]
    public async Task R_USR_03_admin_restores_a_disabled_account_and_it_is_accepted_again()
    {
        (await DisableAsync(AdminUid, _staffId, new { leftDate = "2026-10-31" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await RestoreAsync(AdminUid, _staffId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonAsync(response);
        body.GetProperty("status").GetString().ShouldBe("ACTIVE");
        body.GetProperty("leftDate").ValueKind.ShouldBe(JsonValueKind.Null);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("ACTIVE", null, _adminId));
        (await MeAsync(StaffUid)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task R_USR_03_restore_of_an_active_account_returns_it_unchanged()
    {
        var response = await RestoreAsync(AdminUid, _staffId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("ACTIVE", null, null));
    }

    [Test]
    public async Task R_USR_03_anonymized_account_cannot_be_restored_409()
    {
        var anonymizedId = await TestUsers.InsertAsync("uid-gone", status: "ANONYMIZED");

        var response = await RestoreAsync(AdminUid, anonymizedId);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/account-anonymized", "R-USR-03");
        (await RowAsync(anonymizedId)).Status.ShouldBe("ANONYMIZED");
    }

    [Test]
    public async Task R_USR_03_anonymized_account_cannot_be_disabled_409()
    {
        var anonymizedId = await TestUsers.InsertAsync("uid-gone", status: "ANONYMIZED");

        var response = await DisableAsync(AdminUid, anonymizedId, new { leftDate = "2026-10-31" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/account-anonymized", "R-USR-03");
        (await RowAsync(anonymizedId)).ShouldBe(new StatusRow("ANONYMIZED", null, null));
        Auth.Revoked.ShouldBeEmpty();
    }

    [Test]
    public async Task R_TX_11_caller_without_admin_cannot_disable_an_admin_account_409()
    {
        var response = await DisableAsync(ManagerUid, _adminId, new { leftDate = "2026-10-31" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/admin-account-not-lockable", "R-TX-11");
        (await RowAsync(_adminId)).ShouldBe(new StatusRow("ACTIVE", null, null));
        Auth.Revoked.ShouldBeEmpty();
    }

    [Test]
    public async Task R_TX_11_caller_without_admin_can_disable_other_staff()
    {
        var response = await DisableAsync(ManagerUid, _staffId, new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("DISABLED", null, _managerId));
    }

    [Test]
    public async Task R_TX_11_admin_can_disable_another_admin()
    {
        var secondAdmin = await TestUsers.InsertAsync("uid-admin-2");
        await TestRoles.AssignAsync(secondAdmin, _adminRole);

        var response = await DisableAsync(AdminUid, secondAdmin, new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Auth.Revoked.ShouldBe(["uid-admin-2"]);
    }

    [Test]
    public async Task R_USR_07_left_date_after_today_is_409_and_changes_nothing()
    {
        var response = await DisableAsync(AdminUid, _staffId, new { leftDate = "2026-12-01" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/left-date-in-future", "R-USR-07");
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("ACTIVE", null, null));
        Auth.Revoked.ShouldBeEmpty();
    }

    [Test]
    public async Task R_USR_07_left_date_of_today_in_ho_chi_minh_is_accepted()
    {
        // 01:00 on 2026-12-01 in Asia/Ho_Chi_Minh, still 2026-11-30 in UTC.
        Factory.Time.AdjustTime(new DateTimeOffset(2026, 11, 30, 18, 0, 0, TimeSpan.Zero));

        var response = await DisableAsync(AdminUid, _staffId, new { leftDate = "2026-12-01" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("DISABLED", new DateOnly(2026, 12, 1), _adminId));
    }

    [Test]
    public async Task R_USR_12_caller_cannot_disable_own_account_409()
    {
        var secondAdmin = await TestUsers.InsertAsync("uid-admin-2");
        await TestRoles.AssignAsync(secondAdmin, _adminRole);

        var admin = await DisableAsync(AdminUid, _adminId, new { leftDate = "2026-11-30" });
        var manager = await DisableAsync(ManagerUid, _managerId, new { });

        await admin.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/own-account-not-lockable", "R-USR-12");
        await manager.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/own-account-not-lockable", "R-USR-12");
        (await RowAsync(_adminId)).ShouldBe(new StatusRow("ACTIVE", null, null));
        (await RowAsync(_managerId)).ShouldBe(new StatusRow("ACTIVE", null, null));
        Auth.Revoked.ShouldBeEmpty();
    }

    [Test]
    public async Task R_USR_12_last_active_admin_cannot_be_disabled_when_another_admin_disables_the_caller_meanwhile()
    {
        var secondAdmin = await TestUsers.InsertAsync("uid-admin-2");
        await TestRoles.AssignAsync(secondAdmin, _adminRole);
        // Records the sign-in of the caller first, so its request below writes nothing to its own row.
        (await MeAsync(AdminUid)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The second Admin disables the caller and holds the row lock: the caller's request waits on the check of the last
        // ACTIVE Admin, then finds itself DISABLED, which leaves the second Admin as the last one.
        await using var other = await TestRoles.OpenAsync();
        await using var transaction = await other.BeginTransactionAsync();
        await using (var disable = new NpgsqlCommand("UPDATE users SET status = 'DISABLED' WHERE id = @id", other, transaction))
        {
            disable.Parameters.AddWithValue("id", _adminId);
            await disable.ExecuteNonQueryAsync();
        }

        var request = DisableAsync(AdminUid, secondAdmin, new { });
        await WaitForLockWaitAsync();
        await transaction.CommitAsync();
        var response = await request;

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/last-active-admin", "R-USR-12");
        (await RowAsync(secondAdmin)).Status.ShouldBe("ACTIVE");
        Auth.Revoked.ShouldBeEmpty();
    }

    [Test]
    public async Task R_USR_12_two_admins_disabling_each_other_at_once_keep_one_active_admin()
    {
        var secondAdmin = await TestUsers.InsertAsync("uid-admin-2");
        await TestRoles.AssignAsync(secondAdmin, _adminRole);
        (await MeAsync(AdminUid)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await MeAsync("uid-admin-2")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var responses = await Task.WhenAll(
            DisableAsync(AdminUid, secondAdmin, new { }),
            DisableAsync("uid-admin-2", _adminId, new { }));

        // One wins; the other is refused as the last ACTIVE Admin, or as DISABLED when its request starts after the commit.
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(r => r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Forbidden).ShouldBe(1);
        var statuses = new[] { (await RowAsync(_adminId)).Status, (await RowAsync(secondAdmin)).Status };
        statuses.ShouldBe(["ACTIVE", "DISABLED"], ignoreOrder: true);
    }

    [Test]
    public async Task R_USR_12_caller_without_admin_cannot_restore_an_admin_account_409()
    {
        var secondAdmin = await TestUsers.InsertAsync("uid-admin-2");
        await TestRoles.AssignAsync(secondAdmin, _adminRole);
        (await DisableAsync(AdminUid, secondAdmin, new { leftDate = "2026-11-30" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await RestoreAsync(ManagerUid, secondAdmin);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/admin-account-not-restorable", "R-USR-12");
        (await RowAsync(secondAdmin)).ShouldBe(new StatusRow("DISABLED", new DateOnly(2026, 11, 30), _adminId));
    }

    [Test]
    public async Task R_USR_12_admin_restores_an_admin_account()
    {
        var secondAdmin = await TestUsers.InsertAsync("uid-admin-2");
        await TestRoles.AssignAsync(secondAdmin, _adminRole);
        (await DisableAsync(AdminUid, secondAdmin, new { })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await RestoreAsync(AdminUid, secondAdmin);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(secondAdmin)).ShouldBe(new StatusRow("ACTIVE", null, _adminId));
    }

    [Test]
    public async Task R_USR_12_caller_without_admin_restores_other_staff()
    {
        (await DisableAsync(AdminUid, _staffId, new { })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await RestoreAsync(ManagerUid, _staffId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("ACTIVE", null, _managerId));
    }

    [Test]
    public async Task Concurrent_change_of_the_account_is_409_concurrency_conflict()
    {
        // Another transaction changes the row and holds its lock; the request reads the old version, waits on its UPDATE,
        // then finds the version changed once the other transaction commits.
        await using var other = await TestRoles.OpenAsync();
        await using var transaction = await other.BeginTransactionAsync();
        await using (var update = new NpgsqlCommand("UPDATE users SET name = 'Đổi tên' WHERE id = @id", other, transaction))
        {
            update.Parameters.AddWithValue("id", _staffId);
            await update.ExecuteNonQueryAsync();
        }

        var request = DisableAsync(AdminUid, _staffId, new { });
        await WaitForLockWaitAsync();
        await transaction.CommitAsync();
        var response = await request;

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/concurrency-conflict", null);
        (await RowAsync(_staffId)).Status.ShouldBe("ACTIVE");
        Auth.Revoked.ShouldBeEmpty();
    }

    [Test]
    public async Task R_USR_07_a_left_date_only_disable_conflicts_with_a_concurrent_restore()
    {
        (await DisableAsync(AdminUid, _staffId, new { })).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A restore commits while the disable that only records a left date waits: without the version of users, the
        // account would end ACTIVE with a left date.
        await using var other = await TestRoles.OpenAsync();
        await using var transaction = await other.BeginTransactionAsync();
        await using (var restore = new NpgsqlCommand(
            """
            UPDATE users SET status = 'ACTIVE' WHERE id = @id;
            UPDATE staff_profiles SET left_date = NULL WHERE user_id = @id;
            """, other, transaction))
        {
            restore.Parameters.AddWithValue("id", _staffId);
            await restore.ExecuteNonQueryAsync();
        }

        var request = DisableAsync(AdminUid, _staffId, new { leftDate = "2026-11-15" });
        await WaitForLockWaitAsync();
        await transaction.CommitAsync();
        var response = await request;

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/concurrency-conflict", null);
        (await RowAsync(_staffId)).ShouldBe(new StatusRow("ACTIVE", null, _adminId));
    }

    [Test]
    public async Task Caller_without_staff_disable_is_403()
    {
        var cashierId = await TestUsers.InsertAsync("uid-cashier");
        await TestRoles.AssignAsync(cashierId, _cashierRole);

        await (await DisableAsync("uid-cashier", _staffId, new { }))
            .ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        await (await RestoreAsync("uid-cashier", _staffId))
            .ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        (await RowAsync(_staffId)).Status.ShouldBe("ACTIVE");
    }

    [Test]
    public async Task Unknown_or_customer_id_is_404()
    {
        var customerId = await TestUsers.InsertAsync("uid-customer", isStaff: false);

        (await DisableAsync(AdminUid, Guid.NewGuid(), new { })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await DisableAsync(AdminUid, customerId, new { })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RestoreAsync(AdminUid, Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RestoreAsync(AdminUid, customerId)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Invalid_left_date_is_400()
    {
        var response = await DisableAsync(AdminUid, _staffId, new { leftDate = "31/10/2026" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowAsync(_staffId)).Status.ShouldBe("ACTIVE");
    }

    [Test]
    public async Task No_token_is_401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/staff/{_staffId}/restore", UriKind.Relative));

        (await Client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task The_log_line_carries_ids_only()
    {
        await DisableAsync(AdminUid, _staffId, new { leftDate = "2026-10-31" });

        var line = Factory.StaffLogs.Entries.ShouldHaveSingleItem();
        line.Properties["UserId"].ShouldBe(_staffId);
        line.Properties["ActorId"].ShouldBe(_adminId);
        line.Message.ShouldNotContain("@zengear.test");
        line.Message.ShouldNotContain(StaffUid);
    }

    private async Task<HttpResponseMessage> DisableAsync(string uid, Guid id, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/staff/{id}/disable", UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.EmulatorToken(uid));
        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> RestoreAsync(string uid, Guid id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/staff/{id}/restore", UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.EmulatorToken(uid));
        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> MeAsync(string uid)
    {
        using var request = TestTokens.Get("/api/v1/me", TestTokens.EmulatorToken(uid));
        return await Client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private sealed record StatusRow(string Status, DateOnly? LeftDate, Guid? UpdatedBy);

    /// <summary><c>updated_by</c> of the user row: the seed rows of <see cref="TestUsers"/> have none.</summary>
    private static async Task<StatusRow> RowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT u.status, s.left_date, u.updated_by FROM users u JOIN staff_profiles s ON s.user_id = u.id WHERE u.id = @id",
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return new StatusRow(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetFieldValue<DateOnly>(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2));
    }

    /// <summary><c>xmin</c> of the user and of its staff profile: unchanged when nothing was written.</summary>
    private static async Task<string> VersionAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT u.xmin::text || '/' || s.xmin::text FROM users u JOIN staff_profiles s ON s.user_id = u.id WHERE u.id = @id",
            connection);
        command.Parameters.AddWithValue("id", id);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(string sql, Guid id, DateOnly date)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue(sql.Contains("@joined", StringComparison.Ordinal) ? "joined" : "date", date);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Waits until a session of the API waits on a row lock.</summary>
    private static async Task WaitForLockWaitAsync()
    {
        await using var connection = await TestRoles.OpenAsync();
        for (var i = 0; i < 100; i++)
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND datname = current_database()",
                connection);
            if ((long)(await command.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("The request did not wait on the row lock within 5 seconds.");
    }
}
