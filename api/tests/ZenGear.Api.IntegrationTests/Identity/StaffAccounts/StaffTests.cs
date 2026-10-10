using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Identity.StaffAccounts;

/// <summary>POST and GET /api/v1/staff (STF-01) against PostgreSQL, with Firebase Authentication faked.</summary>
public class StaffTests : IntegrationTestBase
{
    private const string Path = "/api/v1/staff";
    private const string AdminUid = "uid-admin";
    private const string ManagerUid = "uid-manager";

    private Guid _adminRole;
    private Guid _managerRole;
    private Guid _cashierRole;
    private Guid _salesRole;
    private Guid _customerRole;
    private Guid _adminId;

    private FakeAuthProvider Auth => Factory.Auth;

    [SetUp]
    public async Task SeedAsync()
    {
        Auth.Reset();
        Factory.StaffLogs.Clear();

        _adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        _managerRole = await TestRoles.InsertAsync("Store Manager");
        _cashierRole = await TestRoles.InsertAsync("Cashier");
        _salesRole = await TestRoles.InsertAsync("Sales Consultant");
        _customerRole = await TestRoles.InsertAsync("Customer", scopingType: "SELF", isSystemRole: true);
        foreach (var role in new[] { _adminRole, _managerRole })
        {
            await TestRoles.GrantAsync(role, PermissionKeys.StaffCreate);
            await TestRoles.GrantAsync(role, PermissionKeys.StaffRead);
        }

        _adminId = await TestUsers.InsertAsync(AdminUid);
        await TestRoles.AssignAsync(_adminId, _adminRole);
        var managerId = await TestUsers.InsertAsync(ManagerUid);
        await TestRoles.AssignAsync(managerId, _managerRole);
    }

    [Test]
    public async Task R_USR_01_admin_creates_a_staff_account_with_profile_and_several_roles()
    {
        var response = await PostAsync(AdminUid, Body("  An.Nguyen@ZenGear.test ", "NV0012", _cashierRole, _salesRole));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldEndWith($"{Path}/{id}");
        body.GetProperty("email").GetString().ShouldBe("an.nguyen@zengear.test");
        body.GetProperty("name").GetString().ShouldBe("Nguyễn Văn An");
        body.GetProperty("employeeCode").GetString().ShouldBe("NV0012");
        body.GetProperty("joinedDate").GetString().ShouldBe("2026-10-01");
        body.GetProperty("workPhone").GetString().ShouldBe("0901234567");
        body.GetProperty("status").GetString().ShouldBe("ACTIVE");
        body.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        body.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("name").GetString())
            .ShouldBe(["Cashier", "Sales Consultant"]);

        var account = Auth.Find("an.nguyen@zengear.test").ShouldNotBeNull();
        account.Password.ShouldBe(FakeAuthProvider.DefaultPassword);
        var row = await StaffRowAsync(id);
        row.ShouldBe(new StaffRow(account.Uid, "ACTIVE", "NV0012", new DateOnly(2026, 10, 1), "0901234567", true, _adminId, 1));
        (await UserRoleIdsAsync(id)).ShouldBe([_cashierRole, _salesRole], ignoreOrder: true);
    }

    [Test]
    public async Task R_USR_01_customer_role_cannot_be_given_to_a_staff_account()
    {
        var response = await PostAsync(AdminUid, Body("a@zengear.test", "NV0001", _cashierRole, _customerRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-not-for-staff", "R-USR-01");
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task R_SEC_01_creating_a_staff_account_without_a_role_is_409()
    {
        var response = await PostAsync(AdminUid, Body("a@zengear.test", "NV0001"));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-required", "R-SEC-01");
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task R_SEC_01_a_body_without_role_ids_is_400()
    {
        var response = await PostAsync(AdminUid, new
        {
            email = "a@zengear.test", name = "A", employeeCode = "NV0001", joinedDate = "2026-10-01",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task R_TX_11_caller_without_the_admin_role_cannot_assign_admin()
    {
        var response = await PostAsync(ManagerUid, Body("a@zengear.test", "NV0001", _cashierRole, _adminRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/admin-role-not-assignable", "R-TX-11");
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task R_TX_11_admin_can_assign_the_admin_role()
    {
        var response = await PostAsync(AdminUid, Body("second.admin@zengear.test", "NV0002", _adminRole));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await UserRoleIdsAsync((await JsonAsync(response)).GetProperty("id").GetGuid())).ShouldBe([_adminRole]);
    }

    [Test]
    public async Task R_TX_11_caller_without_the_admin_role_can_assign_other_roles()
    {
        var response = await PostAsync(ManagerUid, Body("cashier@zengear.test", "NV0003", _cashierRole));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task Caller_without_staff_create_is_403()
    {
        var cashierId = await TestUsers.InsertAsync("uid-cashier");
        await TestRoles.AssignAsync(cashierId, _cashierRole);

        var response = await PostAsync("uid-cashier", Body("a@zengear.test", "NV0001", _cashierRole));

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        await ShouldHaveCreatedNothingAsync(extraUsers: 1);
    }

    [TestCase("not-an-email", "0901234567", "email")]
    [TestCase("a@localhost", "0901234567", "email")]
    [TestCase("a b@zengear.test", "0901234567", "email")]
    [TestCase("a@zengear.test", "090-123", "workPhone")]
    public async Task Invalid_input_is_400(string email, string workPhone, string field)
    {
        var response = await PostAsync(AdminUid, Body(email, "NV0001", workPhone, _cashierRole));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task Unknown_role_is_400()
    {
        var response = await PostAsync(AdminUid, Body("a@zengear.test", "NV0001", Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("roleIds", out _).ShouldBeTrue();
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task Email_already_registered_is_409_whatever_its_case()
    {
        // TestUsers stores <uid>@zengear.test.
        var response = await PostAsync(AdminUid, Body("UID-Manager@ZenGear.test", "NV0001", _cashierRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/email-already-registered", null);
        Auth.Accounts.ShouldBeEmpty();
    }

    [Test]
    public async Task Employee_code_taken_is_409()
    {
        (await PostAsync(AdminUid, Body("first@zengear.test", "NV0001", _cashierRole))).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await PostAsync(AdminUid, Body("second@zengear.test", "NV0001", _cashierRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/employee-code-taken", null);
        Auth.Accounts.Select(a => a.Email).ShouldBe(["first@zengear.test"]);
    }

    [Test]
    public async Task An_existing_firebase_account_is_refused_and_left_untouched()
    {
        var existing = Auth.Add("taken@zengear.test");

        var response = await PostAsync(AdminUid, Body("taken@zengear.test", "NV0001", _cashierRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/auth-account-exists", null);
        Auth.Accounts.ShouldBe([existing]);
        Auth.Deleted.ShouldBeEmpty();
        await ShouldHaveCreatedNothingAsync(firebaseAccounts: 1);
    }

    [Test]
    public async Task Parallel_requests_for_one_email_create_one_account()
    {
        var responses = await Task.WhenAll(
            PostAsync(AdminUid, Body("same@zengear.test", "NV0001", _cashierRole)),
            PostAsync(AdminUid, Body("same@zengear.test", "NV0002", _cashierRole)));

        responses.Select(r => r.StatusCode).ShouldBe([HttpStatusCode.Created, HttpStatusCode.Conflict], ignoreOrder: true);
        await ShouldKeepOneLinkedAccountAsync();
    }

    [Test]
    public async Task Parallel_requests_for_one_employee_code_create_one_account()
    {
        var responses = await Task.WhenAll(
            PostAsync(AdminUid, Body("one@zengear.test", "NV0001", _cashierRole)),
            PostAsync(AdminUid, Body("two@zengear.test", "NV0001", _cashierRole)));

        responses.Select(r => r.StatusCode).ShouldBe([HttpStatusCode.Created, HttpStatusCode.Conflict], ignoreOrder: true);
        await ShouldKeepOneLinkedAccountAsync();
    }

    [Test]
    public async Task A_database_failure_deletes_the_firebase_account_the_request_created()
    {
        Auth.AfterCreate = _ => InsertStaffWithEmployeeCode("NV0001");

        var response = await PostAsync(AdminUid, Body("late@zengear.test", "NV0001", _cashierRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/employee-code-taken", null);
        Auth.Accounts.ShouldBeEmpty();
        Auth.Deleted.Count.ShouldBe(1);
    }

    [Test]
    public async Task A_cancelled_request_still_stores_the_account_it_created()
    {
        using var cancel = new CancellationTokenSource();
        Auth.AfterCreate = _ => cancel.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            PostAsync(AdminUid, Body("gone@zengear.test", "NV0001", _cashierRole), cancel.Token));

        var account = Auth.Find("gone@zengear.test").ShouldNotBeNull();
        await Factory.StaffLogs.WhenLoggedAsync(e => e.Message.StartsWith("Staff account", StringComparison.Ordinal));
        (await ScalarAsync("SELECT count(*) FROM users WHERE firebase_uid = @uid", account.Uid)).ShouldBe(1);
        Auth.Deleted.ShouldBeEmpty();
    }

    [Test]
    public async Task A_cancelled_request_that_fails_in_the_database_still_deletes_its_account()
    {
        using var cancel = new CancellationTokenSource();
        Auth.AfterCreate = _ =>
        {
            InsertStaffWithEmployeeCode("NV0001");
            cancel.Cancel();
        };

        await Should.ThrowAsync<OperationCanceledException>(() =>
            PostAsync(AdminUid, Body("gone@zengear.test", "NV0001", _cashierRole), cancel.Token));

        await Auth.WhenDeletedAsync();
        Auth.Deleted.Count.ShouldBe(1);
        Auth.Accounts.ShouldBeEmpty();
    }

    [Test]
    public async Task The_new_staff_member_must_change_the_password_first()
    {
        await PostAsync(AdminUid, Body("new@zengear.test", "NV0001", _cashierRole));
        var uid = Auth.Find("new@zengear.test")!.Uid;

        using var request = TestTokens.Get("/api/v1/me", TestTokens.EmulatorToken(uid));
        var me = await Client.SendAsync(request);

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(me)).GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task The_log_line_carries_ids_and_no_personal_data()
    {
        var response = await PostAsync(AdminUid, Body("private@zengear.test", "NV0099", _cashierRole));

        var id = (await JsonAsync(response)).GetProperty("id").GetGuid();
        var line = Factory.StaffLogs.Entries.ShouldHaveSingleItem();
        line.Properties["UserId"].ShouldBe(id);
        line.Properties["ActorId"].ShouldBe(_adminId);
        line.Message.ShouldNotContain("private@zengear.test");
        line.Message.ShouldNotContain("Nguyễn");
        line.Message.ShouldNotContain("0901234567");
    }

    [Test]
    public async Task Get_returns_the_staff_account()
    {
        var created = await JsonAsync(await PostAsync(AdminUid, Body("get@zengear.test", "NV0001", _cashierRole)));
        var id = created.GetProperty("id").GetGuid();

        var response = await GetAsync(ManagerUid, $"{Path}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe(created.GetRawText());
    }

    [Test]
    public async Task Get_of_an_unknown_id_or_a_customer_is_404()
    {
        var customerId = await TestUsers.InsertAsync("uid-customer", isStaff: false);

        (await GetAsync(AdminUid, $"{Path}/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetAsync(AdminUid, $"{Path}/{customerId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Get_without_staff_read_is_403()
    {
        var cashierId = await TestUsers.InsertAsync("uid-cashier");
        await TestRoles.AssignAsync(cashierId, _cashierRole);

        var response = await GetAsync("uid-cashier", $"{Path}/{_adminId}");

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
    }

    private static object Body(string email, string employeeCode, params Guid[] roleIds) =>
        Body(email, employeeCode, "0901234567", roleIds);

    private static object Body(string email, string employeeCode, string workPhone, params Guid[] roleIds) => new
    {
        email,
        name = "Nguyễn Văn An",
        employeeCode,
        joinedDate = "2026-10-01",
        workPhone,
        roleIds,
    };

    private async Task<HttpResponseMessage> PostAsync(string uid, object body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Path, UriKind.Relative))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.EmulatorToken(uid));
        return await Client.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> GetAsync(string uid, string path)
    {
        using var request = TestTokens.Get(path, TestTokens.EmulatorToken(uid));
        return await Client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Inserts, outside the request, another staff member holding <paramref name="employeeCode"/>.</summary>
    private static void InsertStaffWithEmployeeCode(string employeeCode)
    {
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand(
            """
            WITH u AS (INSERT INTO users (firebase_uid, name, email) VALUES ('uid-other', 'B', 'other@zengear.test') RETURNING id)
            INSERT INTO staff_profiles (user_id, employee_code) SELECT id, @code FROM u
            """,
            connection);
        command.Parameters.AddWithValue("code", employeeCode);
        command.ExecuteNonQuery();
    }

    private sealed record StaffRow(string FirebaseUid, string Status, string? EmployeeCode, DateOnly? JoinedDate,
        string? WorkPhone, bool MustChangePassword, Guid? CreatedBy, int Profiles);

    private static async Task<StaffRow> StaffRowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT u.firebase_uid, u.status, s.employee_code, s.joined_date, s.work_phone, s.must_change_password, u.created_by,
                   (SELECT count(*)::int FROM staff_profiles WHERE user_id = u.id)
            FROM users u JOIN staff_profiles s ON s.user_id = u.id WHERE u.id = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return new StaffRow(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3), reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetBoolean(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetInt32(7));
    }

    private static async Task<List<Guid>> UserRoleIdsAsync(Guid userId)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT role_id FROM user_roles WHERE user_id = @id", connection);
        command.Parameters.AddWithValue("id", userId);
        await using var reader = await command.ExecuteReaderAsync();
        var ids = new List<Guid>();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static async Task<long> ScalarAsync(string sql, string? uid = null)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        if (uid is not null)
        {
            command.Parameters.AddWithValue("uid", uid);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Only the two users of the set-up (plus <paramref name="extraUsers"/>), and no Firebase account created.</summary>
    private async Task ShouldHaveCreatedNothingAsync(int extraUsers = 0, int firebaseAccounts = 0)
    {
        (await ScalarAsync("SELECT count(*) FROM users")).ShouldBe(2 + extraUsers);
        Auth.Accounts.Count.ShouldBe(firebaseAccounts);
        Auth.Deleted.ShouldBeEmpty();
    }

    /// <summary>One user was created, and the only Firebase account left is the one it holds.</summary>
    private async Task ShouldKeepOneLinkedAccountAsync()
    {
        (await ScalarAsync("SELECT count(*) FROM users WHERE created_by IS NOT NULL")).ShouldBe(1);
        var account = Auth.Accounts.ShouldHaveSingleItem();
        (await ScalarAsync("SELECT count(*) FROM users WHERE firebase_uid = @uid", account.Uid)).ShouldBe(1);
    }
}
