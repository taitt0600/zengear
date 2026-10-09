using System.Net;
using System.Text.Json;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Identity;

/// <summary>GET /api/v1/staff (STF-05, DEC-105) against PostgreSQL with the Vietnamese ICU collation.</summary>
public class StaffListTests : IntegrationTestBase
{
    private const string Path = "/api/v1/staff";
    private const string AdminUid = "uid-admin";
    private const string ManagerUid = "uid-manager";
    private const string CashierUid = "uid-cashier";
    private const string ValidationType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private Guid _adminRole;
    private Guid _cashierRole;
    private Guid _salesRole;
    private Guid _adminId;
    private Guid _managerId;

    [SetUp]
    public async Task SeedAsync()
    {
        _adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        var managerRole = await TestRoles.InsertAsync("Store Manager");
        _cashierRole = await TestRoles.InsertAsync("Cashier");
        _salesRole = await TestRoles.InsertAsync("Sales Consultant");
        await TestRoles.InsertAsync("Customer", scopingType: "SELF", isSystemRole: true);
        foreach (var role in new[] { _adminRole, managerRole })
        {
            await TestRoles.GrantAsync(role, PermissionKeys.StaffRead);
        }

        _adminId = await InsertStaffAsync(new Staff("Quản trị", "AD01", Uid: AdminUid), _adminRole);
        _managerId = await InsertStaffAsync(new Staff("Quản lý", "QL01", Uid: ManagerUid), managerRole);
    }

    [Test]
    public async Task R_USR_13_lists_staff_accounts_only_with_their_list_fields()
    {
        var active = await InsertStaffAsync(new Staff("Bình", "NV01", new DateOnly(2026, 1, 5)), _cashierRole);
        var disabled = await InsertStaffAsync(
            new Staff("Dũng", "NV02", new DateOnly(2026, 2, 1), new DateOnly(2026, 9, 30), "DISABLED"), _cashierRole, _salesRole);
        await InsertStaffAsync(new Staff("Ẩn danh", "NV03", Status: "ANONYMIZED"), _salesRole);
        await TestUsers.InsertAsync("uid-customer", isStaff: false);

        var page = await ListAsync(AdminUid, "");

        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(20);
        page.GetProperty("totalCount").GetInt32().ShouldBe(4);
        Ids(page).ShouldBe([active, disabled, _managerId, _adminId]);

        var row = Items(page).Single(i => i.GetProperty("id").GetGuid() == disabled);
        row.GetProperty("name").GetString().ShouldBe("Dũng");
        row.GetProperty("email").GetString().ShouldBe("nv02@zengear.test");
        row.GetProperty("employeeCode").GetString().ShouldBe("NV02");
        row.GetProperty("joinedDate").GetString().ShouldBe("2026-02-01");
        row.GetProperty("leftDate").GetString().ShouldBe("2026-09-30");
        row.GetProperty("status").GetString().ShouldBe("DISABLED");
        RoleNames(row).ShouldBe(["Cashier", "Sales Consultant"]);
        row.TryGetProperty("workPhone", out _).ShouldBeFalse();
        row.TryGetProperty("mustChangePassword", out _).ShouldBeFalse();
        Items(page).Single(i => i.GetProperty("id").GetGuid() == active)
            .GetProperty("leftDate").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task R_USR_13_filters_by_status()
    {
        var disabled = await InsertStaffAsync(new Staff("Dũng", "NV02", Status: "DISABLED"), _cashierRole);

        Ids(await ListAsync(AdminUid, "?status=DISABLED")).ShouldBe([disabled]);
        Ids(await ListAsync(AdminUid, "?status=ACTIVE")).ShouldBe([_managerId, _adminId]);
    }

    [Test]
    public async Task R_USR_13_filters_by_role_and_keeps_every_role_of_the_row()
    {
        var both = await InsertStaffAsync(new Staff("Bình", "NV01"), _cashierRole, _salesRole);
        var cashier = await InsertStaffAsync(new Staff("Dũng", "NV02"), _cashierRole);
        var sales = await InsertStaffAsync(new Staff("Đức", "NV03"), _salesRole);

        var cashiers = await ListAsync(AdminUid, $"?roleId={_cashierRole}");
        Ids(cashiers).ShouldBe([both, cashier]);
        RoleNames(Items(cashiers)[0]).ShouldBe(["Cashier", "Sales Consultant"]);
        Ids(await ListAsync(AdminUid, $"?roleId={_salesRole}")).ShouldBe([both, sales]);
    }

    [Test]
    public async Task R_USR_13_combines_status_and_role_filters()
    {
        await InsertStaffAsync(new Staff("Bình", "NV01"), _cashierRole);
        var disabledCashier = await InsertStaffAsync(new Staff("Dũng", "NV02", Status: "DISABLED"), _cashierRole);
        await InsertStaffAsync(new Staff("Đức", "NV03", Status: "DISABLED"), _salesRole);

        Ids(await ListAsync(AdminUid, $"?status=DISABLED&roleId={_cashierRole}")).ShouldBe([disabledCashier]);
    }

    [Test]
    public async Task R_USR_13_an_unknown_role_returns_an_empty_page()
    {
        var page = await ListAsync(AdminUid, $"?roleId={Guid.NewGuid()}");

        Items(page).ShouldBeEmpty();
        page.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Test]
    public async Task R_USR_13_sorts_by_name_in_Vietnamese_order_by_default()
    {
        foreach (var (name, i) in new[] { "Bình", "Ánh", "Đức", "Dũng", "Ân" }.Select((n, i) => (n, i)))
        {
            await InsertStaffAsync(new Staff(name, $"NV0{i}"), _cashierRole);
        }

        // Byte order would put Bình and Dũng before Ánh and Ân, and Đức after Quản.
        string[] expected = ["Ánh", "Ân", "Bình", "Dũng", "Đức", "Quản lý", "Quản trị"];
        Names(await ListAsync(AdminUid, "")).ShouldBe(expected);
        Names(await ListAsync(AdminUid, "?sort=name")).ShouldBe(expected);
        Names(await ListAsync(AdminUid, "?sort=-name")).ShouldBe(expected.Reverse());
    }

    [Test]
    public async Task R_USR_13_sorts_by_employee_code_and_joined_date_with_empty_values_last()
    {
        await InsertStaffAsync(new Staff("Bình", "NV10", new DateOnly(2026, 3, 1)), _cashierRole);
        await InsertStaffAsync(new Staff("Dũng", null, new DateOnly(2025, 6, 1)), _cashierRole);
        await InsertStaffAsync(new Staff("Đức", "NV02", new DateOnly(2026, 1, 15)), _cashierRole);

        Codes(await ListAsync(AdminUid, "?sort=employeeCode")).ShouldBe(["AD01", "NV02", "NV10", "QL01", null]);
        Codes(await ListAsync(AdminUid, "?sort=-employeeCode")).ShouldBe(["QL01", "NV10", "NV02", "AD01", null]);
        Dates(await ListAsync(AdminUid, "?sort=joinedDate")).ShouldBe(["2025-06-01", "2026-01-15", "2026-03-01", null, null]);
        Dates(await ListAsync(AdminUid, "?sort=-joinedDate")).ShouldBe(["2026-03-01", "2026-01-15", "2025-06-01", null, null]);
    }

    [Test]
    public async Task R_USR_13_pages_are_stable_when_names_are_equal()
    {
        for (var i = 0; i < 5; i++)
        {
            await InsertStaffAsync(new Staff("Trùng tên", $"NV0{i}"), _cashierRole);
        }

        var all = Ids(await ListAsync(AdminUid, "?pageSize=100"));
        var paged = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var body = await ListAsync(AdminUid, $"?page={page}&pageSize=2");
            body.GetProperty("totalCount").GetInt32().ShouldBe(7);
            paged.AddRange(Ids(body));
        }

        all.Count.ShouldBe(7);
        paged.ShouldBe(all);
    }

    [Test]
    public async Task R_USR_13_a_name_email_or_phone_in_the_query_string_filters_nothing()
    {
        await InsertStaffAsync(new Staff("Bình", "NV01"), _cashierRole);

        // Values that match no row: any search would empty the list. No personal data goes in a URL, even in a test.
        var all = await ListAsync(AdminUid, "");
        var queried = await ListAsync(AdminUid, "?q=no-match&name=no-match&email=no-match&phone=no-match");

        queried.GetRawText().ShouldBe(all.GetRawText());
    }

    [TestCase("status=ANONYMIZED", "status")]
    [TestCase("status=active", "status")]
    [TestCase("sort=email", "sort")]
    [TestCase("page=0", "page")]
    [TestCase("pageSize=101", "pageSize")]
    [TestCase("roleId=abc", "roleId")]
    [TestCase("page=x", "page")]
    [TestCase("pageSize=x", "pageSize")]
    public async Task R_USR_13_an_invalid_parameter_is_400_naming_the_rule(string query, string field)
    {
        var response = await GetAsync(AdminUid, $"{Path}?{query}");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, "R-USR-13");
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }

    [Test]
    public async Task R_USR_13_a_value_that_does_not_bind_is_not_written_back()
    {
        var response = await GetAsync(AdminUid, $"{Path}?roleId=not-a-uuid-marker");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, "R-USR-13");
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("not-a-uuid-marker");
    }

    [Test]
    public async Task R_SEC_03_store_manager_sees_accounts_holding_the_admin_role()
    {
        var page = await ListAsync(ManagerUid, "");

        var admin = Items(page).Single(i => i.GetProperty("id").GetGuid() == _adminId);
        RoleNames(admin).ShouldBe(["Admin"]);
        Ids(await ListAsync(ManagerUid, $"?roleId={_adminRole}")).ShouldBe([_adminId]);
    }

    [Test]
    public async Task R_SEC_03_list_without_staff_read_is_403()
    {
        await InsertStaffAsync(new Staff("Thu ngân", "TN01", Uid: CashierUid), _cashierRole);

        var response = await GetAsync(CashierUid, Path);

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
    }

    /// <summary>A staff row; the email is <c>&lt;employee code or uid&gt;@zengear.test</c> in lower case.</summary>
    private sealed record Staff(
        string Name, string? EmployeeCode, DateOnly? JoinedDate = null, DateOnly? LeftDate = null, string Status = "ACTIVE",
        string? Uid = null);

    private static async Task<Guid> InsertStaffAsync(Staff staff, params Guid[] roleIds)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            WITH u AS (
              INSERT INTO users (firebase_uid, name, email, status) VALUES (@uid, @name, @email, @status) RETURNING id
            ), s AS (
              INSERT INTO staff_profiles (user_id, employee_code, joined_date, left_date, work_phone)
              SELECT id, @code, @joined, @left, '0901234567' FROM u
            )
            SELECT id FROM u
            """,
            connection);
        var local = staff.EmployeeCode ?? staff.Uid ?? Guid.NewGuid().ToString("N");
        command.Parameters.AddWithValue("uid", (object?)staff.Uid ?? DBNull.Value);
        command.Parameters.AddWithValue("name", staff.Name);
        command.Parameters.AddWithValue("email", $"{local.ToLowerInvariant()}@zengear.test");
        command.Parameters.AddWithValue("status", staff.Status);
        command.Parameters.AddWithValue("code", (object?)staff.EmployeeCode ?? DBNull.Value);
        command.Parameters.AddWithValue("joined", (object?)staff.JoinedDate ?? DBNull.Value);
        command.Parameters.AddWithValue("left", (object?)staff.LeftDate ?? DBNull.Value);
        var id = (Guid)(await command.ExecuteScalarAsync())!;

        foreach (var roleId in roleIds)
        {
            await TestRoles.AssignAsync(id, roleId);
        }

        return id;
    }

    private async Task<HttpResponseMessage> GetAsync(string uid, string path) =>
        await Client.SendAsync(TestTokens.Get(path, TestTokens.EmulatorToken(uid)));

    private async Task<JsonElement> ListAsync(string uid, string query)
    {
        var response = await GetAsync(uid, Path + query);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await JsonAsync(response);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static List<JsonElement> Items(JsonElement page) => [.. page.GetProperty("items").EnumerateArray()];

    private static List<Guid> Ids(JsonElement page) => [.. Items(page).Select(i => i.GetProperty("id").GetGuid())];

    private static List<string?> Names(JsonElement page) => [.. Items(page).Select(i => i.GetProperty("name").GetString())];

    private static List<string?> Codes(JsonElement page) =>
        [.. Items(page).Select(i => i.GetProperty("employeeCode").GetString())];

    private static List<string?> Dates(JsonElement page) => [.. Items(page).Select(i => i.GetProperty("joinedDate").GetString())];

    private static List<string?> RoleNames(JsonElement row) =>
        [.. row.GetProperty("roles").EnumerateArray().Select(r => r.GetProperty("name").GetString())];
}
