using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.IntegrationTests.Locations.BusinessLocations;

/// <summary>
/// /api/v1/admin/business-locations and /api/v1/shop/business-locations (LOC-01, UC-070) against PostgreSQL, with
/// Firebase Authentication faked.
/// </summary>
public class BusinessLocationTests : IntegrationTestBase
{
    private const string AdminUid = "uid-admin";
    private const string CashierUid = "uid-cashier";

    /// <summary>10:00 on Friday 2027-02-05 in Asia/Ho_Chi_Minh.</summary>
    private static readonly DateTimeOffset Now = new(2027, 2, 5, 3, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2027, 2, 5);

    private Guid _adminId;
    private Guid _locationId;

    [SetUp]
    public async Task SeedAsync()
    {
        Factory.Time.AdjustTime(Now);

        var adminRole = await TestRoles.InsertAsync("Admin", isSystemRole: true);
        await TestRoles.GrantAsync(adminRole, PermissionKeys.BusinessLocationUpdate);
        _adminId = await TestUsers.InsertAsync(AdminUid);
        await TestRoles.AssignAsync(_adminId, adminRole);

        // A role created here that never receives business_location.update.
        var cashierRole = await TestRoles.InsertAsync("Cashier");
        await TestRoles.AssignAsync(await TestUsers.InsertAsync(CashierUid), cashierRole);

        _locationId = await TestLocations.InsertAsync();
    }

    [Test]
    public async Task R_LOC_01_admin_reads_the_location_with_its_etag()
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/admin/business-locations/{_locationId}", AdminUid);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        var body = await JsonAsync(response);
        body.GetProperty("id").GetGuid().ShouldBe(_locationId);
        body.GetProperty("name").GetString().ShouldBe("ZenGear Sài Gòn");
        body.GetProperty("phone").GetString().ShouldBe("02838123456");
        body.GetProperty("provinceId").GetInt32().ShouldBe(TestLocations.HoChiMinhCity);
        body.GetProperty("provinceName").GetString().ShouldBe("Thành phố Hồ Chí Minh");
        body.GetProperty("wardId").GetInt32().ShouldBe(TestLocations.SaiGonWard);
        body.GetProperty("wardName").GetString().ShouldBe("Phường Sài Gòn");
        var monday = body.GetProperty("regularHours")[0];
        monday.GetProperty("day").GetInt32().ShouldBe(1);
        monday.GetProperty("open").GetString().ShouldBe("08:30");
        monday.GetProperty("close").GetString().ShouldBe("21:30");
        body.GetProperty("specialHours").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task R_LOC_01_admin_updates_address_hotline_and_hours()
    {
        var response = await UpdateAsync(AdminUid, await ETagAsync(), ValidUpdate());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        var body = await JsonAsync(response);
        body.GetProperty("wardName").GetString().ShouldBe("Phường Bến Thành");
        body.GetProperty("specialHours")[0].GetProperty("closed").GetBoolean().ShouldBeTrue();

        var row = await TestLocations.RowAsync(_locationId);
        row.ShouldBe(row with
        {
            Name = "ZenGear Bến Thành",
            Phone = "1900636123",
            Email = "hotro@zengear.test",
            AddressDetail = "45 Lê Thánh Tôn",
            ProvinceId = TestLocations.HoChiMinhCity,
            WardId = TestLocations.BenThanhWard,
            UpdatedBy = _adminId,
        });
        // The shape of docs/DATABASE.md, as PostgreSQL prints jsonb.
        row.RegularHours.ShouldBe("""[{"day": 1, "open": "08:00", "close": "12:00"}, {"day": 1, "open": "13:00", "close": "21:00"}]""");
        row.SpecialHours.ShouldBe("""[{"date": "2027-02-06", "closed": true}, {"date": "2027-02-07", "open": "10:00", "close": "16:00", "closed": false}]""");
    }

    [Test]
    public async Task R_LOC_01_public_list_gives_the_holiday_hours_in_place_of_the_weekly_hours()
    {
        (await UpdateAsync(AdminUid, await ETagAsync(), ValidUpdate())).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await Client.SendAsync(TestTokens.Get("/api/v1/shop/business-locations?page=1&pageSize=20", null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonAsync(response);
        body.GetProperty("totalCount").GetInt32().ShouldBe(1);
        var location = body.GetProperty("items")[0];
        location.GetProperty("name").GetString().ShouldBe("ZenGear Bến Thành");
        location.GetProperty("wardName").GetString().ShouldBe("Phường Bến Thành");
        location.GetProperty("provinceName").GetString().ShouldBe("Thành phố Hồ Chí Minh");
        location.TryGetProperty("wardId", out _).ShouldBeFalse();

        // From Friday 2027-02-05: Saturday 06 is closed (holiday), Sunday 07 opens 10:00–16:00 instead of its weekly
        // hours (none), Monday 08 has its weekly hours.
        var week = location.GetProperty("weekHours");
        week.GetArrayLength().ShouldBe(7);
        week[0].GetProperty("date").GetString().ShouldBe("2027-02-05");
        week[1].GetProperty("date").GetString().ShouldBe("2027-02-06");
        week[1].GetProperty("periods").GetArrayLength().ShouldBe(0);
        week[2].GetProperty("periods")[0].GetProperty("open").GetString().ShouldBe("10:00");
        week[3].GetProperty("periods").GetArrayLength().ShouldBe(2);
        week[3].GetProperty("periods")[1].GetProperty("close").GetString().ShouldBe("21:00");
    }

    [Test]
    public async Task R_LOC_01_public_list_leaves_out_past_holidays()
    {
        // Saved while 2027-02-06 was ahead; two days later it has passed and the website no longer shows it.
        (await UpdateAsync(AdminUid, await ETagAsync(), ValidUpdate())).StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.Time.AdjustTime(Now.AddDays(2));

        var body = await JsonAsync(await Client.SendAsync(TestTokens.Get("/api/v1/shop/business-locations", null)));

        var special = body.GetProperty("items")[0].GetProperty("specialHours");
        special.GetArrayLength().ShouldBe(1);
        special[0].GetProperty("date").GetString().ShouldBe("2027-02-07");
    }

    [TestCase("POST", "/api/v1/admin/business-locations")]
    [TestCase("DELETE", "/api/v1/admin/business-locations/{0}")]
    public async Task R_LOC_01_admin_cannot_add_or_delete_a_location(string method, string path)
    {
        var response = await SendAsync(new HttpMethod(method), string.Format(path, _locationId), AdminUid,
            method == "POST" ? ValidUpdate() : null);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await TestLocations.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task UC_070_3a_ward_outside_the_province_is_400_and_changes_nothing()
    {
        var before = await TestLocations.RowAsync(_locationId);

        var response = await UpdateAsync(AdminUid, await ETagAsync(), ValidUpdate() with { WardId = TestLocations.HoanKiemWard });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "https://tools.ietf.org/html/rfc9110#section-15.5.1", null);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("wardId", out _).ShouldBeTrue();
        (await TestLocations.RowAsync(_locationId)).ShouldBe(before);
    }

    [Test]
    public async Task Unknown_province_is_400()
    {
        var response = await UpdateAsync(AdminUid, await ETagAsync(), ValidUpdate() with { ProvinceId = 99 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("provinceId", out _).ShouldBeTrue();
    }

    [Test]
    public async Task An_inactive_ward_is_refused_when_chosen_but_kept_when_unchanged()
    {
        var chosen = await UpdateAsync(AdminUid, await ETagAsync(), ValidUpdate() with { WardId = TestLocations.InactiveWard });
        chosen.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await using (var connection = await TestRoles.OpenAsync())
        await using (var command = new NpgsqlCommand($"UPDATE wards SET is_active = false WHERE id = {TestLocations.SaiGonWard}", connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        var kept = await UpdateAsync(AdminUid, await ETagAsync(), ValidUpdate() with { WardId = TestLocations.SaiGonWard });
        kept.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task R_DB_09_database_refuses_a_ward_of_another_province()
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"UPDATE business_locations SET ward_id = {TestLocations.HoanKiemWard} WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", _locationId);

        var error = await Should.ThrowAsync<PostgresException>(command.ExecuteNonQueryAsync());
        error.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        error.ConstraintName.ShouldBe("fk_business_locations_wards_ward_id_province_id");
    }

    [Test]
    public async Task R_LOC_01_a_closed_holiday_with_hours_is_400()
    {
        var update = ValidUpdate() with
        {
            SpecialHours = [new SpecialHours(new DateOnly(2027, 2, 6), true, "08:00", "12:00")],
        };

        var before = await TestLocations.RowAsync(_locationId);

        var response = await UpdateAsync(AdminUid, await ETagAsync(), update);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "https://tools.ietf.org/html/rfc9110#section-15.5.1", "R-LOC-01");
        (await TestLocations.RowAsync(_locationId)).ShouldBe(before);
    }

    [Test]
    public async Task R_LOC_01_an_unchanged_update_keeps_the_stamps_and_the_etag()
    {
        var first = await UpdateAsync(AdminUid, await ETagAsync(), ValidUpdate());
        var etag = first.Headers.ETag!.Tag;
        var stamped = await UpdatedAtAsync();

        Factory.Time.AdjustTime(Now.AddMinutes(5));
        var again = await UpdateAsync(AdminUid, etag, ValidUpdate());

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        again.Headers.ETag!.Tag.ShouldBe(etag);
        (await UpdatedAtAsync()).ShouldBe(stamped);
    }

    [Test]
    public async Task An_unchanged_update_with_a_stale_etag_is_412()
    {
        var stale = await ETagAsync();
        (await UpdateAsync(AdminUid, stale, ValidUpdate())).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await UpdateAsync(AdminUid, stale, ValidUpdate());

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }

    [Test]
    public async Task An_update_drops_holidays_before_today()
    {
        var update = ValidUpdate() with
        {
            SpecialHours = [new SpecialHours(Today.AddDays(-1), true, null, null), new SpecialHours(Today, true, null, null)],
        };

        var response = await UpdateAsync(AdminUid, await ETagAsync(), update);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TestLocations.RowAsync(_locationId)).SpecialHours.ShouldBe("""[{"date": "2027-02-05", "closed": true}]""");
    }

    [Test]
    public async Task Public_list_page_beyond_the_last_possible_page_is_400()
    {
        var response = await Client.SendAsync(TestTokens.Get(
            $"/api/v1/shop/business-locations?page={ZenGear.Services.Common.Models.PageRequest.MaxPage + 1}&pageSize=100", null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("page", out _).ShouldBeTrue();
    }

    [Test]
    public async Task Invalid_input_is_400_with_each_field()
    {
        var update = ValidUpdate() with
        {
            Name = " ",
            Phone = "abc",
            Email = "not-an-email",
            RegularHours = [new RegularHours(8, "8h", "21:00"), new RegularHours(2, "18:00", "08:00")],
        };

        var response = await UpdateAsync(AdminUid, await ETagAsync(), update);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await JsonAsync(response)).GetProperty("errors");
        foreach (var field in new[] { "name", "phone", "email", "regularHours[0].day", "regularHours[0].open", "regularHours[1]" })
        {
            errors.TryGetProperty(field, out _).ShouldBeTrue(field);
        }
    }

    [Test]
    public async Task Update_without_if_match_is_428_and_with_a_stale_etag_is_412()
    {
        var etag = await ETagAsync();
        (await UpdateAsync(AdminUid, etag, ValidUpdate())).StatusCode.ShouldBe(HttpStatusCode.OK);

        await (await UpdateAsync(AdminUid, null, ValidUpdate()))
            .ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired, "/problems/if-match-required", null);
        (await UpdateAsync(AdminUid, etag, ValidUpdate() with { Name = "Khác" })).StatusCode
            .ShouldBe(HttpStatusCode.PreconditionFailed);
        (await TestLocations.RowAsync(_locationId)).Name.ShouldBe("ZenGear Bến Thành");
    }

    [Test]
    public async Task Unknown_location_is_404()
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/admin/business-locations/{Guid.NewGuid()}", AdminUid);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task R_SEC_03_staff_without_the_permission_is_refused()
    {
        var read = await SendAsync(HttpMethod.Get, $"/api/v1/admin/business-locations/{_locationId}", CashierUid);
        await read.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");

        var update = await UpdateAsync(CashierUid, "*", ValidUpdate());
        await update.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        (await TestLocations.RowAsync(_locationId)).Name.ShouldBe("ZenGear Sài Gòn");
    }

    [Test]
    public async Task Staff_read_without_token_is_401()
    {
        var response = await Client.SendAsync(TestTokens.Get($"/api/v1/admin/business-locations/{_locationId}", null));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Public_list_page_size_above_100_is_400()
    {
        var response = await Client.SendAsync(TestTokens.Get("/api/v1/shop/business-locations?pageSize=101", null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("pageSize", out _).ShouldBeTrue();
    }

    [Test]
    public async Task R_LOC_01_admin_list_gives_the_one_location_with_its_edit_fields()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/business-locations", AdminUid);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonAsync(response);
        body.GetProperty("totalCount").GetInt32().ShouldBe(1);
        var location = body.GetProperty("items")[0];
        location.GetProperty("id").GetGuid().ShouldBe(_locationId);
        location.GetProperty("provinceId").GetInt32().ShouldBe(TestLocations.HoChiMinhCity);
        location.GetProperty("wardId").GetInt32().ShouldBe(TestLocations.SaiGonWard);
    }

    [Test]
    public async Task Admin_list_page_after_the_last_is_empty_with_the_total()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/business-locations?page=2&pageSize=1", AdminUid);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonAsync(response);
        body.GetProperty("items").GetArrayLength().ShouldBe(0);
        body.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task R_SEC_03_staff_without_the_permission_cannot_list_the_locations()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/business-locations", CashierUid);

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
    }

    [Test]
    public async Task Admin_list_page_size_above_100_is_400_without_rule()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/business-locations?pageSize=101", AdminUid);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await JsonAsync(response);
        body.GetProperty("errors").TryGetProperty("pageSize", out _).ShouldBeTrue();
        body.TryGetProperty("ruleId", out _).ShouldBeFalse();
    }

    private static Update ValidUpdate() => new(
        "ZenGear Bến Thành",
        "1900636123",
        "HoTro@ZenGear.test",
        "45 Lê Thánh Tôn",
        TestLocations.HoChiMinhCity,
        TestLocations.BenThanhWard,
        [new RegularHours(1, "13:00", "21:00"), new RegularHours(1, "08:00", "12:00")],
        [new SpecialHours(new DateOnly(2027, 2, 6), true, null, null), new SpecialHours(new DateOnly(2027, 2, 7), false, "10:00", "16:00")]);

    private async Task<DateTime> UpdatedAtAsync()
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT updated_at FROM business_locations WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", _locationId);
        return (DateTime)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> ETagAsync()
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/admin/business-locations/{_locationId}", AdminUid);
        return response.Headers.ETag!.Tag;
    }

    private Task<HttpResponseMessage> UpdateAsync(string uid, string? ifMatch, Update body)
    {
        var request = Request(HttpMethod.Put, $"/api/v1/admin/business-locations/{_locationId}", uid, body);
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

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private sealed record Update(
        string Name,
        string Phone,
        string? Email,
        string AddressDetail,
        int ProvinceId,
        int WardId,
        RegularHours[] RegularHours,
        SpecialHours[]? SpecialHours);

    private sealed record RegularHours(int Day, string Open, string Close);

    private sealed record SpecialHours(DateOnly Date, bool Closed, string? Open, string? Close);
}
