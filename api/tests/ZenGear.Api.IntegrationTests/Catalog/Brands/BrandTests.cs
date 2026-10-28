using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Catalog.Brands;

/// <summary>/api/v1/admin/brands (PRD-02, UC-010, UC-011) against PostgreSQL, with Firebase Authentication faked.</summary>
public class BrandTests : IntegrationTestBase
{
    private const string ManagerUid = "uid-manager";
    private const string CashierUid = "uid-cashier";
    private const string ReaderUid = "uid-reader";
    private const string CreatorUid = "uid-creator";
    private const string ValidationType = ProblemAssertions.ValidationType;
    private const string AlreadyExists = "/problems/brand-already-exists";

    private static readonly ExpectedError NameTaken = ExpectedError.At("#/name", "BRAND_ALREADY_EXISTS");

    private static readonly DateTimeOffset Now = new(2027, 2, 5, 3, 0, 0, TimeSpan.Zero);

    private Guid _managerId;

    [SetUp]
    public async Task SeedAsync()
    {
        Factory.Time.AdjustTime(Now);

        var managerRole = await TestRoles.InsertAsync("Store Manager");
        await TestRoles.GrantAsync(managerRole, PermissionKeys.BrandRead);
        await TestRoles.GrantAsync(managerRole, PermissionKeys.BrandCreate);
        await TestRoles.GrantAsync(managerRole, PermissionKeys.BrandUpdate);
        _managerId = await TestUsers.InsertAsync(ManagerUid);
        await TestRoles.AssignAsync(_managerId, managerRole);

        // A role created here that never receives a brand permission.
        var cashierRole = await TestRoles.InsertAsync("Cashier");
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(CashierUid), cashierRole);

        // One brand permission each: an action guarded by the wrong key would let them through.
        var readerRole = await TestRoles.InsertAsync("Brand Reader");
        await TestRoles.GrantAsync(readerRole, PermissionKeys.BrandRead);
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(ReaderUid), readerRole);
        var creatorRole = await TestRoles.InsertAsync("Brand Creator");
        await TestRoles.GrantAsync(creatorRole, PermissionKeys.BrandCreate);
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(CreatorUid), creatorRole);
    }

    [Test]
    public async Task Create_returns_201_with_slug_location_and_etag()
    {
        var response = await CreateAsync("  Cooler Master ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.ETag.ShouldNotBeNull();
        var body = await JsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/admin/brands/{id}");
        body.GetProperty("name").GetString().ShouldBe("Cooler Master");
        body.GetProperty("slug").GetString().ShouldBe("cooler-master");
        body.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        body.TryGetProperty("version", out _).ShouldBeFalse();

        var row = await RowAsync(id);
        row.ShouldBe(new BrandRow("Cooler Master", "cooler-master", true, _managerId, _managerId));

        // The tag of the POST is the stored version: an update can send it back at once.
        response.Headers.ETag.Tag.ShouldBe(await ETagAsync(id));
        (await UpdateAsync(id, response.Headers.ETag.Tag, new BrandUpdate("Cooler Master", false))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("ASUS", "Asus")]
    [TestCase("G.Skill", "G Skill")]
    public async Task Create_refuses_a_name_whose_slug_exists(string first, string second)
    {
        (await CreateAsync(first)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await CreateAsync(second);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null, NameTaken);
        (await CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Create_refuses_the_old_name_of_a_renamed_brand()
    {
        var wd = await CreatedIdAsync("WD");
        (await UpdateAsync(wd, await ETagAsync(wd), new BrandUpdate("Western Digital", true))).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The renamed brand still holds the slug wd: the seed finds it by that slug at the next deploy.
        await (await CreateAsync("WD")).ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null, NameTaken);
    }

    [Test]
    public async Task Create_refuses_a_name_that_slugs_like_another_name()
    {
        var wd = await CreatedIdAsync("WD");
        (await UpdateAsync(wd, await ETagAsync(wd), new BrandUpdate("Western Digital", true))).StatusCode.ShouldBe(HttpStatusCode.OK);

        await (await CreateAsync("Western-Digital")).ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null, NameTaken);
    }

    [TestCase("", "NAME_REQUIRED")]
    [TestCase("   ", "NAME_REQUIRED")]
    [TestCase("!!!", "NAME_WITHOUT_LETTER_OR_DIGIT")]
    [TestCase("联想", "NAME_WITHOUT_LETTER_OR_DIGIT")]
    public async Task Create_refuses_invalid_input(string name, string code)
    {
        var response = await CreateAsync(name);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null, ExpectedError.At("#/name", code));
        problem.GetProperty("errors").GetArrayLength().ShouldBe(1);
        (await CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Create_refuses_a_name_over_100_characters_with_the_limit_in_the_text()
    {
        var response = await CreateAsync(new string('a', 101));

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
            ExpectedError.At("#/name", "NAME_TOO_LONG"));
        problem.ErrorDetail(ExpectedError.At("#/name", "NAME_TOO_LONG")).ShouldBe("Tên tối đa 100 ký tự.");
        (await CountAsync()).ShouldBe(0);
    }

    /// <summary>A missing or null name reaches the validator (the request property is nullable), not model binding.</summary>
    [Test]
    public async Task Create_without_a_name_is_NAME_REQUIRED()
    {
        await (await SendAsync(HttpMethod.Post, "/api/v1/admin/brands", ManagerUid, new { }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null, ExpectedError.At("#/name", "NAME_REQUIRED"));
        await (await SendAsync(HttpMethod.Post, "/api/v1/admin/brands", ManagerUid, new { name = (string?)null }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null, ExpectedError.At("#/name", "NAME_REQUIRED"));
        (await CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Create_with_a_broken_json_body_is_BODY_INVALID()
    {
        var request = Request(HttpMethod.Post, "/api/v1/admin/brands", ManagerUid, body: null);
        request.Content = new StringContent("{\"name\": \"ASUS\"", Encoding.UTF8, "application/json");

        var problem = await (await Client.SendAsync(request)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null);

        problem.ErrorCodes().ShouldBe(["BODY_INVALID"]);
        problem.GetProperty("errors")[0].GetProperty("pointer").GetString()!.ShouldStartWith("#");
        problem.GetRawText().ShouldNotContain("ASUS");
        (await CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task Create_without_a_body_is_BODY_REQUIRED()
    {
        var request = Request(HttpMethod.Post, "/api/v1/admin/brands", ManagerUid, body: null);
        request.Content = new StringContent("", Encoding.UTF8, "application/json");

        await (await Client.SendAsync(request)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
            ExpectedError.At("#", "BODY_REQUIRED"));
    }

    [Test]
    public async Task Create_race_gives_one_brand()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CreateAsync("Lian Li")));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null, NameTaken);
        }

        (await CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task R_DEL_01_retired_brand_stays_readable_and_leaves_the_active_list()
    {
        var asus = await CreatedIdAsync("ASUS");
        await CreatedIdAsync("Dell");

        var retired = await UpdateAsync(asus, await ETagAsync(asus), new BrandUpdate("ASUS", IsActive: false));

        retired.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(retired)).GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await SendAsync(HttpMethod.Get, $"/api/v1/admin/brands/{asus}", ManagerUid)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var all = await ListAsync("/api/v1/admin/brands");
        all.GetProperty("totalCount").GetInt32().ShouldBe(2);
        all.GetProperty("items")[0].GetProperty("isActive").GetBoolean().ShouldBeFalse();

        var active = await ListAsync("/api/v1/admin/brands?isActive=true");
        active.GetProperty("totalCount").GetInt32().ShouldBe(1);
        active.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Dell");

        var inactive = await ListAsync("/api/v1/admin/brands?isActive=false");
        inactive.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(asus);
    }

    [Test]
    public async Task R_DEL_01_retired_brand_can_be_restored()
    {
        var asus = await CreatedIdAsync("ASUS");
        (await UpdateAsync(asus, await ETagAsync(asus), new BrandUpdate("ASUS", false))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var restored = await UpdateAsync(asus, await ETagAsync(asus), new BrandUpdate("ASUS", true));

        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(asus)).IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task R_DEL_01_brand_cannot_be_deleted()
    {
        var asus = await CreatedIdAsync("ASUS");

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/admin/brands/{asus}", ManagerUid);

        await response.ShouldBeProblemAsync(HttpStatusCode.MethodNotAllowed, "/problems/method-not-allowed", null);
        (await CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task Update_renames_without_changing_the_slug()
    {
        var wd = await CreatedIdAsync("WD");
        Factory.Time.AdjustTime(Now.AddHours(1));

        var response = await UpdateAsync(wd, await ETagAsync(wd), new BrandUpdate(" Western Digital ", true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        var body = await JsonAsync(response);
        body.GetProperty("name").GetString().ShouldBe("Western Digital");
        body.GetProperty("slug").GetString().ShouldBe("wd");
        (await RowAsync(wd)).ShouldBe(new BrandRow("Western Digital", "wd", true, _managerId, _managerId));
    }

    [Test]
    public async Task Update_may_change_the_case_of_its_own_name()
    {
        var asus = await CreatedIdAsync("ASUS");

        var response = await UpdateAsync(asus, await ETagAsync(asus), new BrandUpdate("Asus", true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync(asus)).Name.ShouldBe("Asus");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Update_refuses_a_name_of_another_brand(bool otherIsActive)
    {
        var dell = await CreatedIdAsync("Dell");
        if (!otherIsActive)
        {
            (await UpdateAsync(dell, await ETagAsync(dell), new BrandUpdate("Dell", false))).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var hp = await CreatedIdAsync("HP");

        var response = await UpdateAsync(hp, await ETagAsync(hp), new BrandUpdate("DELL", true));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, AlreadyExists, null, NameTaken);
        (await RowAsync(hp)).Name.ShouldBe("HP");
    }

    [TestCase("", "NAME_REQUIRED")]
    [TestCase("!!!", "NAME_WITHOUT_LETTER_OR_DIGIT")]
    public async Task Update_refuses_invalid_input(string name, string code)
    {
        var asus = await CreatedIdAsync("ASUS");

        var response = await UpdateAsync(asus, await ETagAsync(asus), new BrandUpdate(name, true));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null, ExpectedError.At("#/name", code));
        (await RowAsync(asus)).Name.ShouldBe("ASUS");
    }

    [Test]
    public async Task Update_without_is_active_is_400_and_keeps_the_brand_in_use()
    {
        var asus = await CreatedIdAsync("ASUS");
        var request = Request(HttpMethod.Put, $"/api/v1/admin/brands/{asus}", ManagerUid, new { name = "ASUS ROG" });
        request.Headers.TryAddWithoutValidation("If-Match", await ETagAsync(asus));

        var response = await Client.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
            ExpectedError.At("#/isActive", "IS_ACTIVE_REQUIRED"));
        (await RowAsync(asus)).ShouldBe(new BrandRow("ASUS", "asus", true, _managerId, _managerId));
    }

    [Test]
    public async Task Update_without_if_match_is_428()
    {
        var asus = await CreatedIdAsync("ASUS");

        var response = await UpdateAsync(asus, null, new BrandUpdate("ASUS ROG", true));

        await response.ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired, "/problems/if-match-required", null);
    }

    [Test]
    public async Task Update_with_a_stale_etag_is_412()
    {
        var asus = await CreatedIdAsync("ASUS");
        var stale = await ETagAsync(asus);
        (await UpdateAsync(asus, stale, new BrandUpdate("ASUS ROG", true))).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await UpdateAsync(asus, stale, new BrandUpdate("ASUS TUF", true));

        await response.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "/problems/precondition-failed", null);
        (await RowAsync(asus)).Name.ShouldBe("ASUS ROG");
    }

    [Test]
    public async Task Update_unknown_brand_is_404() =>
        await (await UpdateAsync(Guid.CreateVersion7(), "*", new BrandUpdate("ASUS", true)))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "/problems/not-found", null);

    [Test]
    public async Task Get_unknown_brand_is_404() =>
        await (await SendAsync(HttpMethod.Get, $"/api/v1/admin/brands/{Guid.CreateVersion7()}", ManagerUid))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "/problems/not-found", null);

    [Test]
    public async Task Get_returns_the_brand_with_its_etag()
    {
        var asus = await CreatedIdAsync("ASUS");

        var response = await SendAsync(HttpMethod.Get, $"/api/v1/admin/brands/{asus}", ManagerUid);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        (await JsonAsync(response)).GetProperty("slug").GetString().ShouldBe("asus");
    }

    [Test]
    public async Task List_orders_by_name_and_pages()
    {
        foreach (var name in new[] { "Zotac", "Ấn Tượng", "Acer", "Bảo Long" })
        {
            await CreatedIdAsync(name);
        }

        var first = await ListAsync("/api/v1/admin/brands?page=1&pageSize=3");
        first.GetProperty("totalCount").GetInt32().ShouldBe(4);
        first.GetProperty("items").EnumerateArray().Select(b => b.GetProperty("name").GetString())
            .ShouldBe(["Acer", "Ấn Tượng", "Bảo Long"]);

        var second = await ListAsync("/api/v1/admin/brands?page=2&pageSize=3");
        second.GetProperty("items").EnumerateArray().Select(b => b.GetProperty("name").GetString()).ShouldBe(["Zotac"]);
    }

    [TestCase("page=0", "page", "PAGE_OUT_OF_RANGE")]
    [TestCase("pageSize=101", "pageSize", "PAGE_SIZE_OUT_OF_RANGE")]
    public async Task List_refuses_a_page_out_of_range(string query, string parameter, string code) =>
        await (await SendAsync(HttpMethod.Get, $"/api/v1/admin/brands?{query}", ManagerUid))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null, ExpectedError.OnParameter(parameter, code));

    [TestCase("GET", "/api/v1/admin/brands")]
    [TestCase("GET", "/api/v1/admin/brands/{0}")]
    [TestCase("POST", "/api/v1/admin/brands")]
    [TestCase("PUT", "/api/v1/admin/brands/{0}")]
    public async Task Endpoints_need_their_permission(string method, string path)
    {
        var asus = await CreatedIdAsync("ASUS");
        var request = Request(new HttpMethod(method), string.Format(path, asus), CashierUid,
            method == "GET" ? null : new BrandUpdate("Dell", true));
        request.Headers.TryAddWithoutValidation("If-Match", "*");

        var response = await Client.SendAsync(request);

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        (await CountAsync()).ShouldBe(1);
        (await RowAsync(asus)).Name.ShouldBe("ASUS");
    }

    [TestCase(ReaderUid, "POST", "/api/v1/admin/brands")]
    [TestCase(ReaderUid, "PUT", "/api/v1/admin/brands/{0}")]
    [TestCase(CreatorUid, "PUT", "/api/v1/admin/brands/{0}")]
    public async Task Each_write_needs_its_own_permission(string uid, string method, string path)
    {
        var asus = await CreatedIdAsync("ASUS");
        var request = Request(new HttpMethod(method), string.Format(path, asus), uid, new BrandUpdate("Dell", true));
        request.Headers.TryAddWithoutValidation("If-Match", "*");

        var response = await Client.SendAsync(request);

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        (await CountAsync()).ShouldBe(1);
        (await RowAsync(asus)).Name.ShouldBe("ASUS");
    }

    [Test]
    public async Task Anonymous_caller_is_401() =>
        (await Client.SendAsync(TestTokens.Get("/api/v1/admin/brands", null))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

    private Task<HttpResponseMessage> CreateAsync(string name) =>
        SendAsync(HttpMethod.Post, "/api/v1/admin/brands", ManagerUid, new { name });

    private async Task<Guid> CreatedIdAsync(string name)
    {
        var response = await CreateAsync(name);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await JsonAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<string> ETagAsync(Guid id) =>
        (await SendAsync(HttpMethod.Get, $"/api/v1/admin/brands/{id}", ManagerUid)).Headers.ETag!.Tag;

    private Task<HttpResponseMessage> UpdateAsync(Guid id, string? ifMatch, BrandUpdate body)
    {
        var request = Request(HttpMethod.Put, $"/api/v1/admin/brands/{id}", ManagerUid, body);
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

    private static async Task<long> CountAsync()
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM brands", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<BrandRow> RowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT name, slug, is_active, created_by, updated_by FROM brands WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).ShouldBeTrue();
        return new BrandRow(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetGuid(3), reader.GetGuid(4));
    }

    private sealed record BrandUpdate(string Name, bool? IsActive);

    private sealed record BrandRow(string Name, string Slug, bool IsActive, Guid CreatedBy, Guid UpdatedBy);
}
