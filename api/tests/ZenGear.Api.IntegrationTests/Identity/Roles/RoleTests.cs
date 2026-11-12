using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Identity.Roles;

/// <summary>
/// /api/v1/admin/roles and /api/v1/admin/permissions (AUT-05, UC-002, UC-003) against PostgreSQL, with Firebase
/// Authentication faked. Roles are inserted with SQL: the permission sync does not run in the tests.
/// </summary>
public class RoleTests : IntegrationTestBase
{
    private const string Path = "/api/v1/admin/roles";
    private const string AdminUid = "uid-admin";
    private const string CashierUid = "uid-cashier";
    private const string MemberUid = "uid-member";
    private const string ValidationType = ProblemAssertions.ValidationType;

    private static readonly string[] RoleKeys =
        [PermissionKeys.RoleRead, PermissionKeys.RoleCreate, PermissionKeys.RoleUpdate, PermissionKeys.RoleDelete];

    private Guid _adminRole;
    private Guid _customerRole;
    private Guid _managerRole;
    private Guid _cashierRole;
    private Guid _adminId;

    [SetUp]
    public async Task SeedAsync()
    {
        _adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        foreach (var key in RoleKeys.Append(PermissionKeys.BrandRead))
        {
            await TestRoles.GrantAsync(_adminRole, key);
        }

        _customerRole = await TestRoles.InsertAsync("Customer", scopingType: "SELF", isSystemRole: true);
        _managerRole = await TestRoles.InsertAsync("Store Manager");
        await TestRoles.GrantAsync(_managerRole, PermissionKeys.StaffRead);
        _cashierRole = await TestRoles.InsertAsync("Cashier");

        _adminId = await TestUsers.InsertAsync(AdminUid);
        await TestRoles.AssignAsync(_adminId, _adminRole);
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(CashierUid), _cashierRole);
    }

    [Test]
    public async Task R_SEC_03_role_endpoints_need_role_permissions()
    {
        var id = _cashierRole;
        var calls = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, Path, null),
            (HttpMethod.Get, $"{Path}/{id}", null),
            (HttpMethod.Post, Path, CreateBody("Night Shift")),
            (HttpMethod.Put, $"{Path}/{id}", UpdateBody("Cashier")),
            (HttpMethod.Delete, $"{Path}/{id}", null),
            (HttpMethod.Get, "/api/v1/admin/permissions", null),
        };

        foreach (var (method, path, body) in calls)
        {
            var request = Request(method, path, CashierUid, body);
            request.Headers.TryAddWithoutValidation("If-Match", "*");
            await (await Client.SendAsync(request)).ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        }
    }

    [Test]
    public async Task List_returns_every_role_by_name_with_its_kind()
    {
        var response = await SendAsync(HttpMethod.Get, Path, AdminUid);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await JsonAsync(response);
        page.GetProperty("totalCount").GetInt32().ShouldBe(4);
        var roles = page.GetProperty("items").EnumerateArray().ToList();
        roles.Select(r => r.GetProperty("name").GetString()).ShouldBe(["Admin", "Cashier", "Customer", "Store Manager"]);
        var admin = roles[0];
        admin.GetProperty("isSystemRole").GetBoolean().ShouldBeTrue();
        admin.GetProperty("isDefaultRole").GetBoolean().ShouldBeFalse();
        var manager = roles[3];
        manager.GetProperty("isSystemRole").GetBoolean().ShouldBeFalse();
        manager.GetProperty("isDefaultRole").GetBoolean().ShouldBeTrue();
        manager.GetProperty("maxDiscountPercent").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task List_returns_the_page_asked_for()
    {
        var page = await JsonAsync(await SendAsync(HttpMethod.Get, $"{Path}?page=2&pageSize=3", AdminUid));

        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("totalCount").GetInt32().ShouldBe(4);
        page.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("name").GetString()).ShouldBe(["Store Manager"]);
    }

    [TestCase("/api/v1/admin/roles?page=0", "page", "PAGE_OUT_OF_RANGE")]
    [TestCase("/api/v1/admin/permissions?pageSize=101", "pageSize", "PAGE_SIZE_OUT_OF_RANGE")]
    public async Task Lists_refuse_a_page_out_of_range(string path, string parameter, string code) =>
        await (await SendAsync(HttpMethod.Get, path, AdminUid))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null, ExpectedError.OnParameter(parameter, code));

    [Test]
    public async Task R_SEC_03_create_saves_role_with_permissions_and_discount()
    {
        var response = await SendAsync(HttpMethod.Post, Path, AdminUid,
            CreateBody("  Kỹ thuật viên kho ", [PermissionKeys.BrandRead, PermissionKeys.StaffRead, PermissionKeys.BrandRead], 5.5m));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.ETag.ShouldNotBeNull();
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldEndWith($"{Path}/{id}");
        body.GetProperty("name").GetString().ShouldBe("Kỹ thuật viên kho");
        body.GetProperty("isSystemRole").GetBoolean().ShouldBeFalse();
        body.GetProperty("isDefaultRole").GetBoolean().ShouldBeFalse();
        body.GetProperty("maxDiscountPercent").GetDecimal().ShouldBe(5.5m);
        Keys(body).ShouldBe([PermissionKeys.BrandRead, PermissionKeys.StaffRead]);
        body.TryGetProperty("version", out _).ShouldBeFalse();
        (await RoleRowAsync(id)).ShouldBe(new RoleRow("NONE", false, _adminId));
        response.Headers.ETag.Tag.ShouldBe(await ETagAsync(id));
    }

    [Test]
    public async Task R_SEC_12_new_role_manages_no_role()
    {
        var id = await CreatedIdAsync("Night Shift");

        var body = await JsonAsync(await SendAsync(HttpMethod.Get, $"{Path}/{id}", AdminUid));

        body.GetProperty("managedRoles").GetArrayLength().ShouldBe(0);
    }

    [TestCase("", null, null, "#/name", "NAME_REQUIRED")]
    [TestCase("   ", null, null, "#/name", "NAME_REQUIRED")]
    [TestCase("Night Shift", "unknown.key", null, "#/permissionKeys", "PERMISSION_NOT_FOUND")]
    [TestCase("Night Shift", null, "100.5", "#/maxDiscountPercent", "MAX_DISCOUNT_PERCENT_OUT_OF_RANGE")]
    [TestCase("Night Shift", null, "-1", "#/maxDiscountPercent", "MAX_DISCOUNT_PERCENT_OUT_OF_RANGE")]
    [TestCase("Night Shift", null, "5.555", "#/maxDiscountPercent", "MAX_DISCOUNT_PERCENT_TOO_PRECISE")]
    public async Task Create_refuses_invalid_input(string name, string? key, string? discount, string pointer, string code)
    {
        var response = await SendAsync(HttpMethod.Post, Path, AdminUid,
            CreateBody(name, key is null ? [] : [key], discount is null ? null : decimal.Parse(discount, System.Globalization.CultureInfo.InvariantCulture)));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null, ExpectedError.At(pointer, code));
        (await CountAsync()).ShouldBe(4);
    }

    [Test]
    public async Task Create_refuses_a_name_longer_than_100_and_a_body_without_permission_keys()
    {
        await (await SendAsync(HttpMethod.Post, Path, AdminUid, CreateBody(new string('a', 101))))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null, ExpectedError.At("#/name", "NAME_TOO_LONG"));
        await (await SendAsync(HttpMethod.Post, Path, AdminUid, new { name = "Night Shift" }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
                ExpectedError.At("#/permissionKeys", "PERMISSION_KEYS_REQUIRED"));
    }

    [Test]
    public async Task Create_refuses_a_name_another_role_has_in_any_case()
    {
        var response = await SendAsync(HttpMethod.Post, Path, AdminUid, CreateBody(" cashier "));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-already-exists", null,
            ExpectedError.At("#/name", "ROLE_ALREADY_EXISTS"));
        (await CountAsync()).ShouldBe(4);
    }

    [Test]
    public async Task R_SEC_03_role_permissions_are_not_grantable_to_other_roles()
    {
        // "Chỉ Admin được thao tác" (owner's decision): role.* stay with the Admin, whatever role holds them otherwise.
        await (await SendAsync(HttpMethod.Post, Path, AdminUid, CreateBody("Role Clerk", [PermissionKeys.RoleRead])))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
                ExpectedError.At("#/permissionKeys", "PERMISSION_NOT_GRANTABLE"));

        var response = await UpdateAsync(_cashierRole, "*", UpdateBody("Cashier", [PermissionKeys.RoleUpdate]));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
            ExpectedError.At("#/permissionKeys", "PERMISSION_NOT_GRANTABLE"));
        (await GrantsAsync(_cashierRole)).ShouldBeEmpty();
    }

    [Test]
    public async Task R_SEC_03_update_changes_permissions_and_refreshes_cached_access()
    {
        var member = await TestUsers.InsertAsync(MemberUid);
        var role = await CreatedIdAsync("Brand Viewer");
        await TestRoles.AssignAsync(member, role);
        // The first refusal caches the member's access (R-SEC-07).
        await (await SendAsync(HttpMethod.Get, "/api/v1/admin/brands", MemberUid))
            .ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");

        var response = await UpdateAsync(role, await ETagAsync(role), UpdateBody("Brand Viewer", [PermissionKeys.BrandRead]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Keys(await JsonAsync(response)).ShouldBe([PermissionKeys.BrandRead]);
        (await SendAsync(HttpMethod.Get, "/api/v1/admin/brands", MemberUid)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task R_SEC_03_update_of_permissions_only_changes_the_etag()
    {
        var role = await CreatedIdAsync("Night Shift");
        var first = await ETagAsync(role);

        var response = await UpdateAsync(role, first, UpdateBody("Night Shift", [PermissionKeys.StaffRead]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.ShouldNotBe(first);
        (await UpdateAsync(role, first, UpdateBody("Night Shift", [])))
            .StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await GrantsAsync(role)).ShouldBe([PermissionKeys.StaffRead]);
    }

    [Test]
    public async Task R_SEC_03_update_refuses_permission_change_on_system_role()
    {
        var keys = RoleKeys.Append(PermissionKeys.BrandRead).ToArray();
        var refused = new[]
        {
            UpdateBody("Admin", [PermissionKeys.BrandRead]),
            UpdateBody("Administrator", keys),
            UpdateBody("Admin", keys, managedRoleIds: [_cashierRole]),
        };

        foreach (var body in refused)
        {
            await (await UpdateAsync(_adminRole, "*", body))
                .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/system-role-not-editable", "R-SEC-03");
        }

        (await GrantsAsync(_adminRole)).Count.ShouldBe(keys.Length);
    }

    [Test]
    public async Task R_SEC_03_update_allows_discount_on_system_role()
    {
        var keys = RoleKeys.Append(PermissionKeys.BrandRead).ToArray();

        var response = await UpdateAsync(_adminRole, await ETagAsync(_adminRole), UpdateBody("Admin", keys, 10m));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("maxDiscountPercent").GetDecimal().ShouldBe(10m);
    }

    [Test]
    public async Task R_SEC_03_default_role_rename_refused()
    {
        var response = await UpdateAsync(_managerRole, "*", UpdateBody("Shop Manager", [PermissionKeys.StaffRead]));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/default-role-not-renamable", null,
            ExpectedError.At("#/name", "DEFAULT_ROLE_NOT_RENAMABLE"));
    }

    [Test]
    public async Task R_SEC_03_default_role_permissions_editable()
    {
        var response = await UpdateAsync(_managerRole, "*",
            UpdateBody("Store Manager", [PermissionKeys.BrandRead], 5m, [_cashierRole]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GrantsAsync(_managerRole)).ShouldBe([PermissionKeys.BrandRead]);
    }

    [Test]
    public async Task R_SEC_03_default_and_system_roles_are_not_deletable()
    {
        foreach (var role in new[] { _managerRole, _adminRole, _customerRole })
        {
            await (await SendAsync(HttpMethod.Delete, $"{Path}/{role}", AdminUid))
                .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-not-deletable", null);
        }

        (await CountAsync()).ShouldBe(4);
    }

    [Test]
    public async Task R_SEC_01_delete_refuses_a_role_held_by_a_user()
    {
        var role = await CreatedIdAsync("Night Shift");
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(MemberUid, status: "DISABLED"), role);

        var response = await SendAsync(HttpMethod.Delete, $"{Path}/{role}", AdminUid);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-in-use", null);
        (await CountAsync()).ShouldBe(5);
    }

    [Test]
    public async Task Delete_removes_an_unused_role_and_its_rows()
    {
        var role = await CreatedIdAsync("Night Shift", [PermissionKeys.StaffRead]);
        await TestRoles.ManageAsync(role, _cashierRole);
        await TestRoles.ManageAsync(_managerRole, role);

        var response = await SendAsync(HttpMethod.Delete, $"{Path}/{role}", AdminUid);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Get, $"{Path}/{role}", AdminUid)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ScalarAsync("SELECT count(*) FROM role_manageable_roles WHERE role_id = @id OR managed_role_id = @id", role)).ShouldBe(0);
        (await ScalarAsync("SELECT count(*) FROM role_permissions WHERE role_id = @id", role)).ShouldBe(0);
    }

    [Test]
    public async Task R_SEC_12_update_sets_managed_roles()
    {
        var role = await CreatedIdAsync("Night Shift");

        var response = await UpdateAsync(role, await ETagAsync(role),
            UpdateBody("Night Shift", [], managedRoleIds: [_managerRole, _cashierRole, _cashierRole]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("managedRoles").EnumerateArray()
            .Select(r => (r.GetProperty("id").GetGuid(), r.GetProperty("name").GetString()))
            .ShouldBe([(_cashierRole, "Cashier"), (_managerRole, "Store Manager")]);

        (await UpdateAsync(role, "*", UpdateBody("Night Shift", [], managedRoleIds: []))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ScalarAsync("SELECT count(*) FROM role_manageable_roles WHERE role_id = @id", role)).ShouldBe(0);
    }

    [Test]
    public async Task R_SEC_12_update_refuses_a_system_role_as_managed_role()
    {
        var role = await CreatedIdAsync("Night Shift");

        foreach (var system in new[] { _adminRole, _customerRole })
        {
            await (await UpdateAsync(role, "*", UpdateBody("Night Shift", [], managedRoleIds: [_cashierRole, system])))
                .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/role-not-manageable", "R-SEC-12",
                    ExpectedError.At("#/managedRoleIds", "ROLE_NOT_MANAGEABLE"));
        }

        (await ScalarAsync("SELECT count(*) FROM role_manageable_roles WHERE role_id = @id", role)).ShouldBe(0);
    }

    [Test]
    public async Task Update_refuses_an_unknown_role_or_key_and_a_body_without_managed_roles()
    {
        await (await UpdateAsync(_cashierRole, "*", UpdateBody("Cashier", [], managedRoleIds: [Guid.CreateVersion7()])))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null, ExpectedError.At("#/managedRoleIds", "ROLE_NOT_FOUND"));
        await (await UpdateAsync(_cashierRole, "*", new { name = "Cashier", permissionKeys = Array.Empty<string>() }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
                ExpectedError.At("#/managedRoleIds", "MANAGED_ROLE_IDS_REQUIRED"));
    }

    [Test]
    public async Task Update_needs_a_current_if_match_and_an_existing_role()
    {
        await (await UpdateAsync(_cashierRole, null, UpdateBody("Cashier")))
            .ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired, "/problems/if-match-required", null);
        await (await UpdateAsync(_cashierRole, "\"1\"", UpdateBody("Cashier")))
            .ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "/problems/precondition-failed", null);

        var unknown = Guid.CreateVersion7();
        (await UpdateAsync(unknown, "*", UpdateBody("Cashier"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, $"{Path}/{unknown}", AdminUid)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"{Path}/{unknown}", AdminUid)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Permissions_lists_the_catalog_by_key()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/permissions", AdminUid);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await JsonAsync(response);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        page.GetProperty("totalCount").GetInt32().ShouldBe(items.Count);
        var keys = items.Select(p => p.GetProperty("key").GetString()!).ToList();
        keys.ShouldBe(keys.Order(StringComparer.Ordinal).ToList());
        keys.ShouldContain(PermissionKeys.RoleRead);
        items.ShouldAllBe(p => !string.IsNullOrWhiteSpace(p.GetProperty("description").GetString()));
    }

    private static object CreateBody(string name, string[]? permissionKeys = null, decimal? maxDiscountPercent = null) =>
        new { name, permissionKeys = permissionKeys ?? [], maxDiscountPercent };

    private static object UpdateBody(string name, string[]? permissionKeys = null, decimal? maxDiscountPercent = null,
        Guid[]? managedRoleIds = null) =>
        new { name, permissionKeys = permissionKeys ?? [], maxDiscountPercent, managedRoleIds = managedRoleIds ?? [] };

    private async Task<Guid> CreatedIdAsync(string name, string[]? permissionKeys = null)
    {
        var response = await SendAsync(HttpMethod.Post, Path, AdminUid, CreateBody(name, permissionKeys));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<string> ETagAsync(Guid id) =>
        (await SendAsync(HttpMethod.Get, $"{Path}/{id}", AdminUid)).Headers.ETag!.Tag;

    private Task<HttpResponseMessage> UpdateAsync(Guid id, string? ifMatch, object body)
    {
        var request = Request(HttpMethod.Put, $"{Path}/{id}", AdminUid, body);
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string uid, object? body = null) =>
        Client.SendAsync(Request(method, path, uid, body));

    private static HttpRequestMessage Request(HttpMethod method, string path, string uid, object? body)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.EmulatorToken(uid));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        return request;
    }

    private static List<string> Keys(JsonElement role) =>
        [.. role.GetProperty("permissionKeys").EnumerateArray().Select(k => k.GetString()!)];

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static Task<long> CountAsync() => ScalarAsync("SELECT count(*) FROM roles", Guid.Empty);

    private static async Task<long> ScalarAsync(string sql, Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<List<string>> GrantsAsync(Guid roleId)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT p.key FROM role_permissions rp JOIN permissions p ON p.id = rp.permission_id WHERE rp.role_id = @id ORDER BY p.key",
            connection);
        command.Parameters.AddWithValue("id", roleId);
        await using var reader = await command.ExecuteReaderAsync();
        List<string> keys = [];
        while (await reader.ReadAsync())
        {
            keys.Add(reader.GetString(0));
        }

        return keys;
    }

    private static async Task<RoleRow> RoleRowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT scoping_type, is_system_role, created_by FROM roles WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return new RoleRow(reader.GetString(0), reader.GetBoolean(1), reader.GetGuid(2));
    }

    private sealed record RoleRow(string ScopingType, bool IsSystemRole, Guid CreatedBy);
}
