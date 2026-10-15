using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Catalog.Series;

/// <summary>/api/v1/admin/product-series (PRD-03, UC-012, UC-013) against PostgreSQL, with Firebase Authentication faked.</summary>
public class ProductSeriesTests : IntegrationTestBase
{
    private const string ManagerUid = "uid-manager";
    private const string CashierUid = "uid-cashier";
    private const string ReaderUid = "uid-reader";
    private const string CreatorUid = "uid-creator";
    private const string Path = "/api/v1/admin/product-series";
    private const string ValidationType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string AlreadyExists = "/problems/product-series-already-exists";
    private const string BrandRetired = "/problems/brand-retired";

    private static readonly DateTimeOffset Now = new(2027, 2, 5, 3, 0, 0, TimeSpan.Zero);

    private Guid _managerId;
    private Guid _asus;
    private Guid _msi;

    [SetUp]
    public async Task SeedAsync()
    {
        Factory.Time.AdjustTime(Now);

        var managerRole = await TestRoles.InsertAsync("Store Manager");
        await TestRoles.GrantAsync(managerRole, PermissionKeys.ProductSeriesRead);
        await TestRoles.GrantAsync(managerRole, PermissionKeys.ProductSeriesCreate);
        await TestRoles.GrantAsync(managerRole, PermissionKeys.ProductSeriesUpdate);
        _managerId = await TestUsers.InsertAsync(ManagerUid);
        await TestRoles.AssignAsync(_managerId, managerRole);

        // A role created here that never receives a series permission.
        var cashierRole = await TestRoles.InsertAsync("Cashier");
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(CashierUid), cashierRole);

        // One series permission each: an action guarded by the wrong key would let them through.
        var readerRole = await TestRoles.InsertAsync("Series Reader");
        await TestRoles.GrantAsync(readerRole, PermissionKeys.ProductSeriesRead);
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(ReaderUid), readerRole);
        var creatorRole = await TestRoles.InsertAsync("Series Creator");
        await TestRoles.GrantAsync(creatorRole, PermissionKeys.ProductSeriesCreate);
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(CreatorUid), creatorRole);

        _asus = await InsertBrandAsync("ASUS", isActive: true);
        _msi = await InsertBrandAsync("MSI", isActive: true);
    }

    // Acceptance criterion of PRD-03: a series belongs to exactly one brand.
    [Test]
    public async Task Create_returns_201_with_slug_brand_location_and_etag()
    {
        var response = await CreateAsync("  ROG Strix ", _asus);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.ETag.ShouldNotBeNull();
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldEndWith($"{Path}/{id}");
        body.GetProperty("name").GetString().ShouldBe("ROG Strix");
        body.GetProperty("slug").GetString().ShouldBe("rog-strix");
        body.GetProperty("brandId").GetGuid().ShouldBe(_asus);
        body.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        body.TryGetProperty("version", out _).ShouldBeFalse();

        (await RowAsync(id)).ShouldBe(new SeriesRow("ROG Strix", "rog-strix", _asus, true, _managerId, _managerId));

        // The tag of the POST is the stored version: an update can send it back at once.
        response.Headers.ETag.Tag.ShouldBe(await ETagAsync(id));
        (await UpdateAsync(id, response.Headers.ETag.Tag, new SeriesUpdate("ROG Strix", _asus, false))).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Create_without_a_brand_is_400()
    {
        var response = await SendAsync(HttpMethod.Post, Path, ManagerUid, new { name = "ROG Strix" });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("brandId", out _).ShouldBeTrue();
        (await CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Create_with_an_unknown_brand_is_400()
    {
        var response = await CreateAsync("ROG Strix", Guid.CreateVersion7());

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("brandId", out _).ShouldBeTrue();
        (await CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task R_DEL_01_create_refuses_a_retired_brand()
    {
        var retired = await InsertBrandAsync("Gigabyte", isActive: false);

        var response = await CreateAsync("AORUS", retired);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, BrandRetired, "R-DEL-01");
        (await CountAsync()).ShouldBe(0);
    }

    // The slug index is global: the same name is refused in another brand as well.
    [TestCase(true, "Gaming", "GAMING")]
    [TestCase(false, "Gaming", "Gaming")]
    [TestCase(true, "TUF Gaming", "TUF-Gaming")]
    public async Task Create_refuses_a_name_whose_slug_exists(bool sameBrand, string first, string second)
    {
        (await CreateAsync(first, _msi)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await CreateAsync(second, sameBrand ? _msi : _asus);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null);
        (await CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Create_refuses_the_old_name_of_a_renamed_series()
    {
        var strix = await CreatedIdAsync("Strix", _asus);
        (await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("ROG Strix", _asus, true))).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        await (await CreateAsync("Strix", _asus)).ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null);
        await (await CreateAsync("ROG-Strix", _asus)).ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("!!!")]
    [TestCase("联想")]
    public async Task Create_refuses_invalid_input(string name)
    {
        var response = await CreateAsync(name, _asus);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("name", out _).ShouldBeTrue();
        (await CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Create_race_gives_one_series()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CreateAsync("Ventus", _msi)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null);
        }

        (await CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task R_DEL_01_retired_series_stays_readable_and_leaves_the_active_list()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        await CreatedIdAsync("TUF Gaming", _asus);

        var retired = await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("ROG Strix", _asus, IsActive: false));

        retired.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(retired)).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await SendAsync(HttpMethod.Get, $"{Path}/{strix}", ManagerUid)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var all = await ListAsync(Path);
        all.GetProperty("totalCount").GetInt32().ShouldBe(2);
        all.GetProperty("items")[0].GetProperty("isActive").GetBoolean().ShouldBeFalse();

        var active = await ListAsync($"{Path}?isActive=true");
        active.GetProperty("totalCount").GetInt32().ShouldBe(1);
        active.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("TUF Gaming");

        var inactive = await ListAsync($"{Path}?isActive=false");
        inactive.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(strix);
    }

    [Test]
    public async Task R_DEL_01_retired_series_can_be_restored()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        (await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("ROG Strix", _asus, false))).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        var restored = await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("ROG Strix", _asus, true));

        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(strix)).IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task R_DEL_01_series_cannot_be_deleted()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);

        var response = await SendAsync(HttpMethod.Delete, $"{Path}/{strix}", ManagerUid);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task R_DEL_01_database_refuses_deleting_a_brand_with_series()
    {
        await CreatedIdAsync("ROG Strix", _asus);

        var delete = ExecuteAsync("DELETE FROM brands WHERE id = @id", _asus);

        var error = await Should.ThrowAsync<PostgresException>(delete);
        error.SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);
        error.ConstraintName.ShouldBe("fk_product_series_brands_brand_id");
    }

    [Test]
    public async Task Update_renames_without_changing_the_slug()
    {
        var strix = await CreatedIdAsync("Strix", _asus);
        Factory.Time.AdjustTime(Now.AddHours(1));

        var response = await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate(" ROG Strix ", _asus, true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        var body = await JsonAsync(response);
        body.GetProperty("name").GetString().ShouldBe("ROG Strix");
        body.GetProperty("slug").GetString().ShouldBe("strix");
        (await RowAsync(strix)).ShouldBe(new SeriesRow("ROG Strix", "strix", _asus, true, _managerId, _managerId));
    }

    [Test]
    public async Task Update_may_change_the_case_of_its_own_name()
    {
        var tuf = await CreatedIdAsync("TUF GAMING", _asus);

        var response = await UpdateAsync(tuf, await ETagAsync(tuf), new SeriesUpdate("TUF Gaming", _asus, true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(tuf)).Name.ShouldBe("TUF Gaming");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Update_refuses_a_name_of_another_series(bool otherIsActive)
    {
        var ventus = await CreatedIdAsync("Ventus", _msi);
        if (!otherIsActive)
        {
            (await UpdateAsync(ventus, await ETagAsync(ventus), new SeriesUpdate("Ventus", _msi, false))).StatusCode
                .ShouldBe(HttpStatusCode.OK);
        }

        var strix = await CreatedIdAsync("ROG Strix", _asus);

        var response = await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("VENTUS", _asus, true));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null);
        (await RowAsync(strix)).Name.ShouldBe("ROG Strix");
    }

    [TestCase("")]
    [TestCase("!!!")]
    public async Task Update_refuses_invalid_input(string name)
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);

        var response = await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate(name, _asus, true));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("name", out _).ShouldBeTrue();
        (await RowAsync(strix)).Name.ShouldBe("ROG Strix");
    }

    [Test]
    public async Task Update_without_is_active_is_400_and_keeps_the_series_in_use()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        var request = Request(HttpMethod.Put, $"{Path}/{strix}", ManagerUid, new { name = "ROG", brandId = _asus });
        request.Headers.TryAddWithoutValidation("If-Match", await ETagAsync(strix));

        var response = await Client.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("isActive", out _).ShouldBeTrue();
        (await RowAsync(strix)).ShouldBe(new SeriesRow("ROG Strix", "rog-strix", _asus, true, _managerId, _managerId));
    }

    [Test]
    public async Task Update_moves_the_series_to_another_brand()
    {
        var ventus = await CreatedIdAsync("Ventus", _asus);

        var response = await UpdateAsync(ventus, await ETagAsync(ventus), new SeriesUpdate("Ventus", _msi, true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("brandId").GetGuid().ShouldBe(_msi);
        (await RowAsync(ventus)).BrandId.ShouldBe(_msi);
    }

    [Test]
    public async Task Update_with_an_unknown_brand_is_400()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        var etag = await ETagAsync(strix);

        var response = await UpdateAsync(strix, etag, new SeriesUpdate("ROG Strix", Guid.CreateVersion7(), true));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("brandId", out _).ShouldBeTrue();
        (await RowAsync(strix)).BrandId.ShouldBe(_asus);
        (await ETagAsync(strix)).ShouldBe(etag);
    }

    [Test]
    public async Task R_DEL_01_update_refuses_moving_to_a_retired_brand()
    {
        var retired = await InsertBrandAsync("Gigabyte", isActive: false);
        var strix = await CreatedIdAsync("ROG Strix", _asus);

        var response = await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("ROG Strix", retired, true));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, BrandRetired, "R-DEL-01");
        (await RowAsync(strix)).BrandId.ShouldBe(_asus);
    }

    // Retiring a brand does not touch its series: they can still be renamed or retired in that brand.
    [Test]
    public async Task Update_keeps_a_retired_brand()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        await ExecuteAsync("UPDATE brands SET is_active = false WHERE id = @id", _asus);

        var response = await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("ROG Strix II", _asus, false));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(strix)).ShouldBe(new SeriesRow("ROG Strix II", "rog-strix", _asus, false, _managerId, _managerId));
    }

    // A restored series is in use again, so its brand must be too: restore the brand first (R-DEL-01).
    [Test]
    public async Task R_DEL_01_update_refuses_restoring_a_series_of_a_retired_brand()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        (await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("ROG Strix", _asus, false))).StatusCode
            .ShouldBe(HttpStatusCode.OK);
        await ExecuteAsync("UPDATE brands SET is_active = false WHERE id = @id", _asus);

        var response = await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("ROG Strix", _asus, true));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, BrandRetired, "R-DEL-01");
        (await RowAsync(strix)).IsActive.ShouldBeFalse();
    }

    [Test]
    public async Task Update_without_if_match_is_428()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);

        var response = await UpdateAsync(strix, null, new SeriesUpdate("ROG", _asus, true));

        await response.ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired, "/problems/if-match-required", null);
    }

    [Test]
    public async Task Update_with_a_stale_etag_is_412()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        var stale = await ETagAsync(strix);
        (await UpdateAsync(strix, stale, new SeriesUpdate("ROG", _asus, true))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await UpdateAsync(strix, stale, new SeriesUpdate("Strix", _asus, true));

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await RowAsync(strix)).Name.ShouldBe("ROG");
    }

    [Test]
    public async Task Update_unknown_series_is_404() =>
        (await UpdateAsync(Guid.CreateVersion7(), "*", new SeriesUpdate("ROG Strix", _asus, true))).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

    [Test]
    public async Task Get_unknown_series_is_404() =>
        (await SendAsync(HttpMethod.Get, $"{Path}/{Guid.CreateVersion7()}", ManagerUid)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

    [Test]
    public async Task Get_returns_the_series_with_its_etag()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);

        var response = await SendAsync(HttpMethod.Get, $"{Path}/{strix}", ManagerUid);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        var body = await JsonAsync(response);
        body.GetProperty("slug").GetString().ShouldBe("rog-strix");
        body.GetProperty("brandId").GetGuid().ShouldBe(_asus);
    }

    [Test]
    public async Task List_orders_by_name_and_pages()
    {
        foreach (var name in new[] { "Zenbook", "Ấn Tượng", "Aspire", "Bảo Long" })
        {
            await CreatedIdAsync(name, _asus);
        }

        var first = await ListAsync($"{Path}?page=1&pageSize=3");
        first.GetProperty("totalCount").GetInt32().ShouldBe(4);
        first.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("name").GetString())
            .ShouldBe(["Aspire", "Ấn Tượng", "Bảo Long"]);

        var second = await ListAsync($"{Path}?page=2&pageSize=3");
        second.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ShouldBe(["Zenbook"]);
    }

    // UC-012 step 4: the series a product of a brand can take are that brand's active series (R-DEL-01).
    [Test]
    public async Task List_filters_by_brand_and_activity()
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        await CreatedIdAsync("TUF Gaming", _asus);
        await CreatedIdAsync("Ventus", _msi);
        (await UpdateAsync(strix, await ETagAsync(strix), new SeriesUpdate("ROG Strix", _asus, false))).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        var asus = await ListAsync($"{Path}?brandId={_asus}");
        asus.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("name").GetString())
            .ShouldBe(["ROG Strix", "TUF Gaming"]);

        var offered = await ListAsync($"{Path}?brandId={_asus}&isActive=true");
        offered.GetProperty("totalCount").GetInt32().ShouldBe(1);
        offered.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("TUF Gaming");
        offered.GetProperty("items")[0].GetProperty("brandId").GetGuid().ShouldBe(_asus);
    }

    [TestCase("page=0")]
    [TestCase("pageSize=101")]
    public async Task List_refuses_a_page_out_of_range(string query) =>
        (await SendAsync(HttpMethod.Get, $"{Path}?{query}", ManagerUid)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    [TestCase("GET", Path)]
    [TestCase("GET", Path + "/{0}")]
    [TestCase("POST", Path)]
    [TestCase("PUT", Path + "/{0}")]
    public async Task Endpoints_need_their_permission(string method, string path)
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        var request = Request(new HttpMethod(method), string.Format(path, strix), CashierUid,
            method == "GET" ? null : new SeriesUpdate("Ventus", _msi, true));
        request.Headers.TryAddWithoutValidation("If-Match", "*");

        var response = await Client.SendAsync(request);

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        (await CountAsync()).ShouldBe(1);
        (await RowAsync(strix)).Name.ShouldBe("ROG Strix");
    }

    [TestCase(ReaderUid, "POST", Path)]
    [TestCase(ReaderUid, "PUT", Path + "/{0}")]
    [TestCase(CreatorUid, "PUT", Path + "/{0}")]
    public async Task Each_write_needs_its_own_permission(string uid, string method, string path)
    {
        var strix = await CreatedIdAsync("ROG Strix", _asus);
        var request = Request(new HttpMethod(method), string.Format(path, strix), uid, new SeriesUpdate("Ventus", _msi, true));
        request.Headers.TryAddWithoutValidation("If-Match", "*");

        var response = await Client.SendAsync(request);

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        (await CountAsync()).ShouldBe(1);
        (await RowAsync(strix)).Name.ShouldBe("ROG Strix");
    }

    [Test]
    public async Task Anonymous_caller_is_401() =>
        (await Client.SendAsync(TestTokens.Get(Path, null))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    private Task<HttpResponseMessage> CreateAsync(string name, Guid brandId) =>
        SendAsync(HttpMethod.Post, Path, ManagerUid, new { name, brandId });

    private async Task<Guid> CreatedIdAsync(string name, Guid brandId)
    {
        var response = await CreateAsync(name, brandId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<string> ETagAsync(Guid id) =>
        (await SendAsync(HttpMethod.Get, $"{Path}/{id}", ManagerUid)).Headers.ETag!.Tag;

    private Task<HttpResponseMessage> UpdateAsync(Guid id, string? ifMatch, SeriesUpdate body)
    {
        var request = Request(HttpMethod.Put, $"{Path}/{id}", ManagerUid, body);
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return Client.SendAsync(request);
    }

    private async Task<JsonElement> ListAsync(string path)
    {
        var response = await SendAsync(HttpMethod.Get, path, ManagerUid);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await JsonAsync(response);
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

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task<Guid> InsertBrandAsync(string name, bool isActive)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO brands (name, slug, is_active) VALUES (@name, lower(@name), @active) RETURNING id", connection);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("active", isActive);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(string sql, Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync()
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM product_series", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<SeriesRow> RowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT name, slug, brand_id, is_active, created_by, updated_by FROM product_series WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return new SeriesRow(reader.GetString(0), reader.GetString(1), reader.GetGuid(2), reader.GetBoolean(3),
            reader.GetGuid(4), reader.GetGuid(5));
    }

    private sealed record SeriesUpdate(string Name, Guid? BrandId, bool? IsActive);

    private sealed record SeriesRow(string Name, string Slug, Guid BrandId, bool IsActive, Guid CreatedBy, Guid UpdatedBy);
}
