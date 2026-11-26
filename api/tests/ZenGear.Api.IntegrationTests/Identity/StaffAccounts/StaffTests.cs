using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Identity.StaffAccounts;

/// <summary>POST and GET /api/v1/admin/staff (STF-01) against PostgreSQL, with Firebase Authentication faked.</summary>
public class StaffTests : IntegrationTestBase
{
    private const string Path = "/api/v1/admin/staff";
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

        // The Admin holds the whole catalog, staff.approve included, so it creates ACTIVE accounts; the Store Manager does
        // not, so its accounts wait for approval (R-USR-17, STF-09).
        await TestRoles.GrantAsync(_adminRole, PermissionKeys.StaffApprove);

        // The Store Manager's default set covers the default roles (R-SEC-12, DEC-128); the tests insert it, no sync runs.
        await TestRoles.ManageAsync(_managerRole, _managerRole, _cashierRole, _salesRole);

        _adminId = await TestUsers.InsertAsync(AdminUid);
        await TestRoles.AssignAsync(_adminId, _adminRole);
        var managerId = await TestUsers.InsertAsync(ManagerUid);
        await TestRoles.AssignAsync(managerId, _managerRole);
    }

    [Test]
    public async Task R_SEC_12_create_staff_refuses_role_outside_scope()
    {
        var nightShift = await TestRoles.InsertAsync("Night Shift");

        var response = await PostAsync(ManagerUid, Body(_cashierRole, nightShift));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-out-of-scope", "R-SEC-12",
            ExpectedError.At("#/roleIds", "ROLE_OUT_OF_SCOPE"));
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task R_SEC_12_create_staff_accepts_role_in_scope()
    {
        var response = await PostAsync(ManagerUid, Body(_salesRole, _cashierRole));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task R_SEC_12_admin_manages_every_role()
    {
        var nightShift = await TestRoles.InsertAsync("Night Shift");

        var response = await PostAsync(AdminUid, Body(nightShift));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task R_SEC_12_manageable_set_is_union_of_held_roles()
    {
        var hiring = await TestRoles.InsertAsync("Hiring Cashiers");
        var hiringSales = await TestRoles.InsertAsync("Hiring Sales");
        await TestRoles.GrantAsync(hiring, PermissionKeys.StaffCreate);
        await TestRoles.ManageAsync(hiring, _cashierRole);
        await TestRoles.ManageAsync(hiringSales, _salesRole);
        var recruiter = await TestUsers.InsertAsync("uid-recruiter");
        await TestRoles.AssignAsync(recruiter, hiring);
        await TestRoles.AssignAsync(recruiter, hiringSales);

        var response = await PostAsync("uid-recruiter", Body(_cashierRole, _salesRole));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task R_SEC_12_assignable_roles_lists_the_manageable_set()
    {
        await TestRoles.InsertAsync("Night Shift");

        var manager = await GetAsync(ManagerUid, $"{Path}/assignable-roles");
        var admin = await GetAsync(AdminUid, $"{Path}/assignable-roles");

        manager.StatusCode.ShouldBe(HttpStatusCode.OK);
        var managerPage = await JsonAsync(manager);
        managerPage.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("name").GetString())
            .ShouldBe(["Cashier", "Sales Consultant", "Store Manager"]);
        managerPage.GetProperty("totalCount").GetInt32().ShouldBe(3);
        // The Admin manages every staff role, the Admin role included; Customer is never a staff role (R-USR-01).
        (await JsonAsync(admin)).GetProperty("items").EnumerateArray().Select(r => r.GetProperty("name").GetString())
            .ShouldBe(["Admin", "Cashier", "Night Shift", "Sales Consultant", "Store Manager"]);
    }

    [Test]
    public async Task Assignable_roles_need_staff_create()
    {
        var cashierId = await TestUsers.InsertAsync("uid-cashier-reader");
        await TestRoles.AssignAsync(cashierId, _cashierRole);

        await (await GetAsync("uid-cashier-reader", $"{Path}/assignable-roles"))
            .ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
    }

    [Test]
    public async Task R_USR_01_admin_creates_a_staff_account_with_profile_and_several_roles()
    {
        var response = await PostAsync(AdminUid, Body(_cashierRole, _salesRole));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldEndWith($"{Path}/{id}");
        body.GetProperty("name").GetString().ShouldBe("Nguyễn Văn An");
        body.GetProperty("joinedDate").GetString().ShouldBe("2026-10-01");
        body.GetProperty("workPhone").GetString().ShouldBe("0901234567");
        body.GetProperty("status").GetString().ShouldBe("ACTIVE");
        body.GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
        body.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("name").GetString())
            .ShouldBe(["Cashier", "Sales Consultant"]);

        var account = Auth.Find(FirstEmail).ShouldNotBeNull();
        account.Password.ShouldBe(FakeAuthProvider.DefaultPassword);
        var row = await StaffRowAsync(id);
        row.ShouldBe(new StaffRow(account.Uid, "ACTIVE", "ZG260001", "AnNVZG260001", new DateOnly(2026, 10, 1), "0901234567",
            true, _adminId, 1));
        (await UserRoleIdsAsync(id)).ShouldBe([_cashierRole, _salesRole], ignoreOrder: true);
    }

    [Test]
    public async Task R_USR_24_creates_the_generated_identity()
    {
        var body = await JsonAsync(await PostAsync(AdminUid, Body(_cashierRole)));

        body.GetProperty("employeeCode").GetString().ShouldBe("ZG260001");
        body.GetProperty("username").GetString().ShouldBe("AnNVZG260001");
        body.GetProperty("email").GetString().ShouldBe(FirstEmail);
        body.GetProperty("name").GetString().ShouldBe("Nguyễn Văn An");
        Auth.Find(FirstEmail).ShouldNotBeNull();
        (await ScalarObjectAsync($"SELECT email FROM users WHERE id = '{body.GetProperty("id").GetGuid()}'")).ShouldBe(FirstEmail);
    }

    [Test]
    public async Task R_USR_24_code_takes_the_year_of_the_joined_date_and_numbers_follow_per_year()
    {
        var first = await JsonAsync(await PostAsync(AdminUid, Body(_cashierRole)));
        var second = await JsonAsync(await PostAsync(AdminUid, Body("Lê Thu Hà", "2026-03-02", _cashierRole)));
        var earlier = await JsonAsync(await PostAsync(AdminUid, Body("Phạm Quốc Bảo", "2025-12-31", _cashierRole)));

        first.GetProperty("employeeCode").GetString().ShouldBe("ZG260001");
        second.GetProperty("employeeCode").GetString().ShouldBe("ZG260002");
        second.GetProperty("username").GetString().ShouldBe("HaLTZG260002");
        earlier.GetProperty("employeeCode").GetString().ShouldBe("ZG250001");
        earlier.GetProperty("email").GetString().ShouldBe("baopqzg250001@zengear.test");
        (await ScalarAsync("SELECT last_value FROM employee_code_sequences WHERE year = 2026")).ShouldBe(2);
        (await ScalarAsync("SELECT last_value FROM employee_code_sequences WHERE year = 2025")).ShouldBe(1);
    }

    [Test]
    public async Task R_USR_24_parallel_creations_get_distinct_numbers()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => PostAsync(AdminUid, Body(_cashierRole))));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
        var codes = await Task.WhenAll(responses.Select(async r => (await JsonAsync(r)).GetProperty("employeeCode").GetString()));
        codes.ShouldBe(["ZG260001", "ZG260002", "ZG260003", "ZG260004", "ZG260005"], ignoreOrder: true);
        Auth.Accounts.Count.ShouldBe(5);
    }

    [Test]
    public async Task R_USR_24_a_refused_request_takes_no_number()
    {
        var nightShift = await TestRoles.InsertAsync("Night Shift");
        (await PostAsync(ManagerUid, Body(_cashierRole, nightShift))).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var created = await JsonAsync(await PostAsync(ManagerUid, Body(_cashierRole)));

        created.GetProperty("employeeCode").GetString().ShouldBe("ZG260001");
    }

    [Test]
    public async Task R_USR_24_a_failed_creation_issues_no_number()
    {
        // Firebase fails after it created the account (a timeout after the write): the request fails, its transaction
        // rolls back and the number is not issued. The account it left is found by its email and deleted, since the
        // rollback issues the same number, hence the same email, to the next person with the same abbreviation.
        string? leftUid = null;
        Auth.AfterCreate = account =>
        {
            leftUid = account.Uid;
            throw new InvalidOperationException("Firebase failed");
        };
        (await PostAsync(AdminUid, Body(_cashierRole))).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        Auth.AfterCreate = null;

        Auth.Deleted.ShouldBe([leftUid.ShouldNotBeNull()]);
        Auth.Find(FirstEmail).ShouldBeNull();
        var created = await JsonAsync(await PostAsync(AdminUid, Body(_cashierRole)));

        created.GetProperty("employeeCode").GetString().ShouldBe("ZG260001");
        created.GetProperty("email").GetString().ShouldBe(FirstEmail);
    }

    [Test]
    public async Task R_USR_24_the_compensation_never_deletes_the_account_of_a_request_waiting_for_the_number()
    {
        // A double submit: request A fails in Firebase before it writes while request B waits on the year's counter
        // row. A looks up the email of its number while it still holds the lock, so B, which then takes the same
        // number and email, keeps the Firebase account it creates.
        Task<HttpResponseMessage>? second = null;
        Auth.BeforeCreate = async _ =>
        {
            Auth.BeforeCreate = null;
            second = PostAsync(AdminUid, Body(_cashierRole));
            await WaitUntilAsync(async () => await ScalarAsync(
                "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%employee_code_sequences%'") > 0);
            throw new InvalidOperationException("Firebase failed before writing");
        };

        (await PostAsync(AdminUid, Body(_cashierRole))).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var created = await JsonAsync(await second.ShouldNotBeNull());

        created.GetProperty("email").GetString().ShouldBe(FirstEmail);
        var account = Auth.Find(FirstEmail).ShouldNotBeNull();
        (await ScalarAsync("SELECT count(*) FROM users WHERE firebase_uid = @uid", account.Uid)).ShouldBe(1);
        Auth.Deleted.ShouldBeEmpty();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The second request never waited on the counter row.");
    }

    [Test]
    public async Task R_USR_24_exhausted_year_is_refused()
    {
        await ExecuteAsync("INSERT INTO employee_code_sequences (year, last_value) VALUES (2026, 9999)");

        var response = await PostAsync(AdminUid, Body(_cashierRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/employee-codes-exhausted", "R-USR-24");
        (await ScalarAsync("SELECT count(*) FROM users")).ShouldBe(2);
        Auth.Accounts.ShouldBeEmpty();
        (await ScalarAsync("SELECT last_value FROM employee_code_sequences WHERE year = 2026")).ShouldBe(9999);
    }

    [Test]
    public async Task R_USR_24_username_unique_case_insensitive()
    {
        await PostAsync(AdminUid, Body(_cashierRole));

        var error = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
            """
            WITH u AS (INSERT INTO users (firebase_uid, name, email) VALUES ('uid-x', 'X', 'x@zengear.test') RETURNING id)
            INSERT INTO staff_profiles (user_id, employee_code, username) SELECT id, 'ZG269998', 'annvzg260001' FROM u
            """));

        error.ConstraintName.ShouldBe("ix_staff_profiles_username");
    }

    [TestCase("NV0012")]
    [TestCase("ZG26001")]
    [TestCase("zg260001")]
    public async Task R_USR_24_employee_code_form(string code)
    {
        var error = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(
            $"""
             WITH u AS (INSERT INTO users (firebase_uid, name, email) VALUES ('uid-x', 'X', 'x@zengear.test') RETURNING id)
             INSERT INTO staff_profiles (user_id, employee_code, username) SELECT id, '{code}', 'XZG260001' FROM u
             """));

        error.ConstraintName.ShouldBe("ck_staff_profiles_employee_code");
    }

    [Test]
    public async Task R_USR_01_customer_role_cannot_be_given_to_a_staff_account()
    {
        var response = await PostAsync(AdminUid, Body(_cashierRole, _customerRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-not-for-staff", "R-USR-01",
            ExpectedError.At("#/roleIds", "ROLE_NOT_FOR_STAFF"));
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task R_SEC_01_creating_a_staff_account_without_a_role_is_409()
    {
        var response = await PostAsync(AdminUid, Body());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-required", "R-SEC-01",
            ExpectedError.At("#/roleIds", "ROLE_REQUIRED"));
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task R_SEC_01_a_body_without_role_ids_is_400()
    {
        var response = await PostAsync(AdminUid, new
        {
            name = "Nguyễn Văn An", joinedDate = "2026-10-01",
        });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ProblemAssertions.ValidationType, null,
            ExpectedError.At("#/roleIds", "ROLE_IDS_REQUIRED"));
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task R_TX_11_caller_without_the_admin_role_cannot_assign_admin()
    {
        var response = await PostAsync(ManagerUid, Body(_cashierRole, _adminRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/admin-role-not-assignable", "R-TX-11",
            ExpectedError.At("#/roleIds", "ADMIN_ROLE_NOT_ASSIGNABLE"));
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task R_TX_11_admin_can_assign_the_admin_role()
    {
        var response = await PostAsync(AdminUid, Body(_adminRole));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await UserRoleIdsAsync((await JsonAsync(response)).GetProperty("id").GetGuid())).ShouldBe([_adminRole]);
    }

    [Test]
    public async Task R_TX_11_caller_without_the_admin_role_can_assign_other_roles()
    {
        var response = await PostAsync(ManagerUid, Body(_cashierRole));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Test]
    public async Task Caller_without_staff_create_is_403()
    {
        var cashierId = await TestUsers.InsertAsync("uid-cashier");
        await TestRoles.AssignAsync(cashierId, _cashierRole);

        var response = await PostAsync("uid-cashier", Body(_cashierRole));

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        await ShouldHaveCreatedNothingAsync(extraUsers: 1);
    }

    [Test]
    public async Task Invalid_work_phone_is_400()
    {
        var response = await PostAsync(AdminUid, Body("Nguyễn Văn An", "2026-10-01", "090-123", _cashierRole));

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ProblemAssertions.ValidationType, null,
            ExpectedError.At("#/workPhone", "WORK_PHONE_INVALID"));
        problem.GetProperty("errors").GetArrayLength().ShouldBe(1);
        problem.GetRawText().ShouldNotContain("090-123");
        problem.ErrorDetail(ExpectedError.At("#/workPhone", "WORK_PHONE_INVALID"))
            .ShouldBe("Số điện thoại công việc gồm 8 đến 15 chữ số, có thể bắt đầu bằng dấu +.");
        await ShouldHaveCreatedNothingAsync();
    }

    /// <summary>
    /// DEC-156 (4): the code keeps 2 digits of the year, so a year 100 years off would take the codes of the real one and
    /// block its counter; refused before any number is taken.
    /// </summary>
    [TestCase("1926-03-02")]
    [TestCase("2126-03-02")]
    public async Task R_USR_24_a_joined_year_outside_2000_to_2099_is_400(string joinedDate)
    {
        var response = await PostAsync(AdminUid, Body("Nguyễn Văn An", joinedDate, "0901234567", _cashierRole));

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ProblemAssertions.ValidationType, null,
            ExpectedError.At("#/joinedDate", "JOINED_DATE_OUT_OF_RANGE"));
        problem.ErrorCodes().ShouldBe(["JOINED_DATE_OUT_OF_RANGE"]);
        problem.ErrorDetail(ExpectedError.At("#/joinedDate", "JOINED_DATE_OUT_OF_RANGE"))
            .ShouldBe("Năm vào làm phải từ 2000 đến 2099.");
        await ShouldHaveCreatedNothingAsync();
    }

    /// <summary>
    /// A missing or blank value reaches the validator (the request properties are nullable), not model binding, so each
    /// gets its own code; a name without a letter gives no username (R-USR-24).
    /// </summary>
    [TestCase(null, "NAME_REQUIRED")]
    [TestCase("   ", "NAME_REQUIRED")]
    [TestCase("12345", "NAME_NO_LETTER")]
    public async Task R_USR_24_a_name_that_gives_no_username_is_400(string? name, string code)
    {
        var body = new Dictionary<string, object?>
        {
            ["joinedDate"] = "2026-10-01",
            ["roleIds"] = new[] { _cashierRole },
        };
        if (name is not null)
        {
            body["name"] = name;
        }

        var problem = await (await PostAsync(AdminUid, body)).ShouldBeProblemAsync(HttpStatusCode.BadRequest,
            ProblemAssertions.ValidationType, null, ExpectedError.At("#/name", code));

        problem.ErrorCodes().ShouldBe([code]);
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task A_value_of_the_wrong_type_is_BODY_INVALID_at_its_field()
    {
        var response = await PostAsync(AdminUid, new
        {
            name = "Nguyễn Văn An", joinedDate = "not-a-date", roleIds = new[] { _cashierRole },
        });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ProblemAssertions.ValidationType, null,
            ExpectedError.At("#/joinedDate", "BODY_INVALID"));
        problem.GetRawText().ShouldNotContain("not-a-date");
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task Unknown_role_is_400()
    {
        var unknown = Guid.NewGuid();
        var response = await PostAsync(AdminUid, Body(unknown));

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ProblemAssertions.ValidationType, null,
            ExpectedError.At("#/roleIds", "ROLE_NOT_FOUND"));
        problem.GetRawText().ShouldNotContain(unknown.ToString());
        await ShouldHaveCreatedNothingAsync();
    }

    [Test]
    public async Task Generated_email_held_by_a_customer_is_409()
    {
        await ExecuteAsync($"INSERT INTO users (firebase_uid, name, email) VALUES ('uid-mai', 'Mai', '{FirstEmail}')");

        var response = await PostAsync(AdminUid, Body(_cashierRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/email-already-registered", null);
        Auth.Accounts.ShouldBeEmpty();
        Auth.Deleted.Count.ShouldBe(1);
    }

    [Test]
    public async Task Generated_employee_code_taken_is_409_and_deletes_the_firebase_account()
    {
        // A row holding the next code, as a counter set back or a row written by hand would give.
        await InsertStaffWithEmployeeCodeAsync("ZG260001");

        var response = await PostAsync(AdminUid, Body(_cashierRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/employee-code-taken", null);
        Auth.Accounts.ShouldBeEmpty();
        Auth.Deleted.Count.ShouldBe(1);
    }

    [Test]
    public async Task An_existing_firebase_account_of_the_generated_email_is_refused_and_left_untouched()
    {
        var existing = Auth.Add(FirstEmail);

        var response = await PostAsync(AdminUid, Body(_cashierRole));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/auth-account-exists", null);
        Auth.Accounts.ShouldBe([existing]);
        Auth.Deleted.ShouldBeEmpty();
        await ShouldHaveCreatedNothingAsync(firebaseAccounts: 1);
        Factory.StaffLogs.Entries.ShouldContain(e => e.Message.Contains(existing.Uid));
    }

    [Test]
    public async Task A_cancelled_request_still_stores_the_account_it_created()
    {
        using var cancel = new CancellationTokenSource();
        Auth.AfterCreate = _ => cancel.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            PostAsync(AdminUid, Body(_cashierRole), cancel.Token));

        var account = Auth.Find(FirstEmail).ShouldNotBeNull();
        await Factory.StaffLogs.WhenLoggedAsync(e => e.Message.StartsWith("Staff account", StringComparison.Ordinal));
        (await ScalarAsync("SELECT count(*) FROM users WHERE firebase_uid = @uid", account.Uid)).ShouldBe(1);
        Auth.Deleted.ShouldBeEmpty();
    }

    [Test]
    public async Task A_cancelled_request_that_fails_in_the_database_still_deletes_its_account()
    {
        await InsertStaffWithEmployeeCodeAsync("ZG260001");
        using var cancel = new CancellationTokenSource();
        Auth.AfterCreate = _ => cancel.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            PostAsync(AdminUid, Body(_cashierRole), cancel.Token));

        await Auth.WhenDeletedAsync();
        Auth.Deleted.Count.ShouldBe(1);
        Auth.Accounts.ShouldBeEmpty();
    }

    [Test]
    public async Task The_new_staff_member_must_change_the_password_first()
    {
        await PostAsync(AdminUid, Body(_cashierRole));
        var uid = Auth.Find(FirstEmail)!.Uid;

        using var request = TestTokens.Get("/api/v1/admin/me", TestTokens.EmulatorToken(uid));
        var me = await Client.SendAsync(request);

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(me)).GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task The_log_line_carries_ids_and_no_personal_data()
    {
        var response = await PostAsync(AdminUid, Body(_cashierRole));

        var id = (await JsonAsync(response)).GetProperty("id").GetGuid();
        var line = Factory.StaffLogs.Entries.ShouldHaveSingleItem();
        line.Properties["UserId"].ShouldBe(id);
        line.Properties["ActorId"].ShouldBe(_adminId);
        line.Message.ShouldNotContain(FirstEmail);
        line.Message.ShouldNotContain("AnNV");
        line.Message.ShouldNotContain("Nguyễn");
        line.Message.ShouldNotContain("0901234567");
    }

    [Test]
    public async Task Get_returns_the_staff_account()
    {
        var created = await JsonAsync(await PostAsync(AdminUid, Body(_cashierRole)));
        var id = created.GetProperty("id").GetGuid();

        var response = await GetAsync(ManagerUid, $"{Path}/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe(created.GetRawText());
    }

    [Test]
    public async Task Get_of_an_unknown_id_or_a_customer_is_404()
    {
        var customerId = await TestUsers.InsertAsync("uid-customer", isStaff: false);

        await (await GetAsync(AdminUid, $"{Path}/{Guid.NewGuid()}")).ShouldBeProblemAsync(HttpStatusCode.NotFound, "/problems/not-found", null);
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

    /// <summary>The email R-USR-24 gives Nguyễn Văn An as the first account of 2026.</summary>
    private const string FirstEmail = "annvzg260001@zengear.test";

    private static object Body(params Guid[] roleIds) => Body("Nguyễn Văn An", "2026-10-01", roleIds);

    private static object Body(string name, string joinedDate, params Guid[] roleIds) =>
        Body(name, joinedDate, "0901234567", roleIds);

    private static object Body(string name, string joinedDate, string workPhone, params Guid[] roleIds) => new
    {
        name,
        joinedDate,
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
    private static Task InsertStaffWithEmployeeCodeAsync(string employeeCode) => ExecuteAsync(
        $"""
         WITH u AS (INSERT INTO users (firebase_uid, name, email) VALUES ('uid-other', 'B', 'other@zengear.test') RETURNING id)
         INSERT INTO staff_profiles (user_id, employee_code, username) SELECT id, '{employeeCode}', 'OtherB{employeeCode}' FROM u
         """);

    private static async Task ExecuteAsync(string sql)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record StaffRow(string FirebaseUid, string Status, string? EmployeeCode, string Username, DateOnly? JoinedDate,
        string? WorkPhone, bool MustChangePassword, Guid? CreatedBy, int Profiles);

    private static async Task<StaffRow> StaffRowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT u.firebase_uid, u.status, s.employee_code, s.username, s.joined_date, s.work_phone, s.must_change_password,
                   u.created_by,
                   (SELECT count(*)::int FROM staff_profiles WHERE user_id = u.id)
            FROM users u JOIN staff_profiles s ON s.user_id = u.id WHERE u.id = @id
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return new StaffRow(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
            reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetBoolean(6), reader.IsDBNull(7) ? null : reader.GetGuid(7),
            reader.GetInt32(8));
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

    private static async Task<object?> ScalarObjectAsync(string sql)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
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
        (await ScalarAsync("SELECT coalesce(sum(last_value), 0) FROM employee_code_sequences")).ShouldBe(0);
        Auth.Accounts.Count.ShouldBe(firebaseAccounts);
        Auth.Deleted.ShouldBeEmpty();
    }
}
