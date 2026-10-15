using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Identity.Account;

/// <summary>
/// GET and PATCH /api/v1/admin/me and POST /api/v1/admin/me/change-password (STF-03) against PostgreSQL and Redis,
/// with Firebase Authentication faked and the clock fixed.
/// </summary>
public class CurrentUserTests : IntegrationTestBase
{
    private const string MePath = "/api/v1/admin/me";
    private const string ChangePasswordPath = "/api/v1/admin/me/change-password";
    private const string StaffUid = "uid-staff";
    private const string StaffEmail = "uid-staff@zengear.test";
    private const string CustomerUid = "uid-customer";
    private const string NewPassword = "  bánh mì nóng 25  ";

    private Guid _staffId;

    private FakeAuthProvider Auth => Factory.Auth;

    private DateTimeOffset Now => Factory.Time.GetUtcNow();

    [SetUp]
    public async Task SeedAsync()
    {
        Auth.Reset();
        Factory.StaffLogs.Clear();
        _staffId = await TestUsers.InsertAsync(StaffUid, mustChangePassword: true);
        Auth.AddWithUid(StaffUid, StaffEmail, FakeAuthProvider.DefaultPassword);
    }

    // ---- GET / PATCH /me (R-USR-04: staff edit the display name only) ----

    [Test]
    public async Task R_USR_04_get_me_returns_an_etag_and_no_version_in_the_body()
    {
        var response = await SendAsync(HttpMethod.Get, MePath, Token(StaffUid));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{await XminAsync(_staffId)}\"");
        (await JsonAsync(response)).TryGetProperty("version", out _).ShouldBeFalse();
    }

    [Test]
    public async Task R_USR_04_staff_renames_own_account()
    {
        var staffId = await TestUsers.InsertAsync("uid-staff-2");

        var response = await RenameAsync("uid-staff-2", new { name = "  Trần Thị Bình " }, await ETagAsync("uid-staff-2"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonAsync(response);
        body.GetProperty("name").GetString().ShouldBe("Trần Thị Bình");
        response.Headers.ETag!.Tag.ShouldBe($"\"{await XminAsync(staffId)}\"");
        (await UserRowAsync(staffId)).ShouldBe(new UserRow("Trần Thị Bình", "uid-staff-2@zengear.test", staffId, Now));
    }

    [Test]
    public async Task R_USR_04_customer_renames_own_display_name()
    {
        var customerId = await TestUsers.InsertAsync(CustomerUid, isStaff: false);

        var response = await RenameAsync(CustomerUid, new { name = "Lê Văn Cường" }, await ETagAsync(CustomerUid));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await UserRowAsync(customerId)).Name.ShouldBe("Lê Văn Cường");
    }

    [Test]
    public async Task R_USR_04_email_in_the_body_is_ignored()
    {
        var staffId = await TestUsers.InsertAsync("uid-staff-2");

        var response = await RenameAsync("uid-staff-2",
            new { name = "Trần Thị Bình", email = "other@zengear.test" }, await ETagAsync("uid-staff-2"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("email").GetString().ShouldBe("uid-staff-2@zengear.test");
        (await UserRowAsync(staffId)).Email.ShouldBe("uid-staff-2@zengear.test");
    }

    [Test]
    public async Task R_USR_04_rename_without_if_match_is_428()
    {
        var staffId = await TestUsers.InsertAsync("uid-staff-2");

        var response = await RenameAsync("uid-staff-2", new { name = "Trần Thị Bình" }, ifMatch: null);

        await response.ShouldBeProblemAsync(HttpStatusCode.PreconditionRequired, "/problems/if-match-required", ruleId: null);
        (await UserRowAsync(staffId)).Name.ShouldBe("Nguyễn Văn A");
    }

    [Test]
    public async Task R_USR_04_rename_with_a_stale_etag_is_412()
    {
        var staffId = await TestUsers.InsertAsync("uid-staff-2");
        var etag = await ETagAsync("uid-staff-2");
        (await RenameAsync("uid-staff-2", new { name = "First" }, etag)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await RenameAsync("uid-staff-2", new { name = "Second" }, etag);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await UserRowAsync(staffId)).Name.ShouldBe("First");
    }

    [Test]
    public async Task R_USR_04_rename_with_a_stale_etag_and_the_same_name_is_still_412()
    {
        await TestUsers.InsertAsync("uid-staff-2");
        var etag = await ETagAsync("uid-staff-2");
        (await RenameAsync("uid-staff-2", new { name = "First" }, etag)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await RenameAsync("uid-staff-2", new { name = "First" }, etag);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }

    [TestCase("42")]
    [TestCase("W/\"42\"")]
    [TestCase("\"not-a-number\"")]
    public async Task R_USR_04_rename_with_a_malformed_if_match_is_412(string ifMatch)
    {
        var staffId = await TestUsers.InsertAsync("uid-staff-2");

        var response = await RenameAsync("uid-staff-2", new { name = "Trần Thị Bình" }, ifMatch);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        (await UserRowAsync(staffId)).Name.ShouldBe("Nguyễn Văn A");
    }

    [Test]
    public async Task R_USR_04_rename_with_if_match_star_succeeds()
    {
        var staffId = await TestUsers.InsertAsync("uid-staff-2");

        var response = await RenameAsync("uid-staff-2", new { name = "Trần Thị Bình" }, "*");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await UserRowAsync(staffId)).Name.ShouldBe("Trần Thị Bình");
    }

    [TestCase("   ")]
    [TestCase(null)]
    public async Task R_USR_04_blank_name_is_400(string? name)
    {
        await TestUsers.InsertAsync("uid-staff-2");

        var response = await RenameAsync("uid-staff-2", new { name }, await ETagAsync("uid-staff-2"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("errors").TryGetProperty("name", out _).ShouldBeTrue();
    }

    [Test]
    public async Task R_USR_04_staff_with_a_pending_password_change_cannot_rename()
    {
        var response = await RenameAsync(StaffUid, new { name = "Trần Thị Bình" }, await ETagAsync(StaffUid));

        await response.ShouldBeForbiddenProblemAsync("/problems/password-change-required", "R-USR-03");
    }

    // ---- POST /me/change-password (R-USR-04, DEC-094) ----

    [Test]
    public async Task R_USR_04_password_change_sets_firebase_password_clears_flag_and_revokes_sessions()
    {
        var token = Token(StaffUid);
        (await SendAsync(HttpMethod.Get, "/api/v1/admin/test/me", token)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var response = await ChangePasswordAsync(token, NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Auth.Find(StaffEmail)!.Password.ShouldBe(NewPassword);
        (await StaffRowAsync(_staffId)).ShouldBe(new StaffRow(MustChangePassword: false, _staffId, Now));
        Auth.Revoked.ShouldBe([StaffUid]);
        // The access cache was bumped: the token in hand reaches the other endpoints again.
        (await SendAsync(HttpMethod.Get, "/api/v1/admin/test/me", token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(await SendAsync(HttpMethod.Get, MePath, token))).GetProperty("mustChangePassword").GetBoolean()
            .ShouldBeFalse();
    }

    [Test]
    public async Task R_USR_04_self_change_without_the_flag_works()
    {
        var staffId = await TestUsers.InsertAsync("uid-staff-2");
        Auth.AddWithUid("uid-staff-2", "uid-staff-2@zengear.test", "old-password-1");

        var response = await ChangePasswordAsync(Token("uid-staff-2"), "a-brand-new-password");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Auth.Find("uid-staff-2@zengear.test")!.Password.ShouldBe("a-brand-new-password");
        (await StaffRowAsync(staffId)).MustChangePassword.ShouldBeFalse();
        Auth.Revoked.ShouldBe(["uid-staff-2"]);
    }

    [Test]
    public async Task R_USR_04_password_change_accepts_a_sign_in_exactly_5_minutes_old()
    {
        var response = await ChangePasswordAsync(Token(StaffUid, authTime: Now.AddMinutes(-5)), NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task R_USR_04_password_change_refuses_a_sign_in_older_than_5_minutes()
    {
        var response = await ChangePasswordAsync(Token(StaffUid, authTime: Now.AddMinutes(-5).AddSeconds(-1)), NewPassword);

        await response.ShouldBeForbiddenProblemAsync("/problems/recent-login-required", "R-USR-04");
        await ShouldHaveChangedNothingAsync();
    }

    [Test]
    public async Task R_USR_04_recent_sign_in_is_checked_before_the_policy()
    {
        var response = await ChangePasswordAsync(Token(StaffUid, authTime: Now.AddMinutes(-30)), "short");

        await response.ShouldBeForbiddenProblemAsync("/problems/recent-login-required", "R-USR-04");
        await ShouldHaveChangedNothingAsync();
    }

    [Test]
    public async Task R_USR_04_customer_cannot_use_the_staff_password_change()
    {
        await TestUsers.InsertAsync(CustomerUid, isStaff: false);

        var response = await ChangePasswordAsync(Token(CustomerUid), "short");

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/staff-account-required", ruleId: null);
        Auth.ChangePasswordCalls.ShouldBe(0);
    }

    [Test]
    public async Task R_USR_04_change_password_without_token_is_401()
    {
        var response = await ChangePasswordAsync(token: null, NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task R_USR_04_firebase_failure_keeps_the_flag()
    {
        Auth.FailChangePassword = true;

        var response = await ChangePasswordAsync(Token(StaffUid), NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await StaffRowAsync(_staffId)).MustChangePassword.ShouldBeTrue();
        Auth.Revoked.ShouldBeEmpty();
    }

    [Test]
    public async Task R_USR_04_revoke_failure_returns_500_after_the_password_and_flag_changed()
    {
        Auth.FailRevoke = true;

        var response = await ChangePasswordAsync(Token(StaffUid), NewPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        Auth.Find(StaffEmail)!.Password.ShouldBe(NewPassword);
        (await StaffRowAsync(_staffId)).MustChangePassword.ShouldBeFalse();
    }

    [Test]
    public async Task R_USR_04_password_never_reaches_the_logs()
    {
        (await ChangePasswordAsync(Token(StaffUid), "secret-9")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ChangePasswordAsync(Token(StaffUid), NewPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var entries = Factory.StaffLogs.Entries.Concat(Factory.SecurityLogs.Entries).ToList();
        entries.ShouldContain(e => e.Message.Contains(_staffId.ToString()));
        entries.ShouldAllBe(e => !e.Message.Contains("secret-9") && !e.Message.Contains(NewPassword.Trim()));
        entries.SelectMany(e => e.Properties.Values).ShouldAllBe(v => !$"{v}".Contains("secret-9") && !$"{v}".Contains(NewPassword.Trim()));
    }

    // ---- R-USR-11: the policy, checked before Firebase ----

    [TestCase("elevenchars")]
    [TestCase("")]
    [TestCase(FakeAuthProvider.DefaultPassword)]
    [TestCase("PASSWORD1234")]
    [TestCase("MyZenGearShop!2026")]
    public async Task R_USR_11_weak_password_is_400_and_changes_nothing(string password)
    {
        var response = await ChangePasswordAsync(Token(StaffUid), password);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "https://tools.ietf.org/html/rfc9110#section-15.5.1", "R-USR-11");
        var text = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(text).RootElement.GetProperty("errors").GetProperty("newPassword").GetArrayLength().ShouldBeGreaterThan(0);
        if (password.Length > 0)
        {
            text.ShouldNotContain(password);
        }

        await ShouldHaveChangedNothingAsync();
    }

    [Test]
    public async Task R_USR_11_a_missing_new_password_is_400_with_the_rule()
    {
        var response = await SendAsync(HttpMethod.Post, ChangePasswordPath, Token(StaffUid), new { });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "https://tools.ietf.org/html/rfc9110#section-15.5.1", "R-USR-11");
        await ShouldHaveChangedNothingAsync();
    }

    // ---- permissions of GET / PATCH /me (R-SEC-03: the web hides what the user may not do; the API still checks) ----

    [Test]
    public async Task R_SEC_03_me_lists_the_keys_of_every_role_once_in_ordinal_order()
    {
        var userId = await TestUsers.InsertAsync("uid-staff-2");
        var manager = await TestRoles.InsertAsync("Store Manager");
        var cashier = await TestRoles.InsertAsync("Cashier", scopingType: "SELF");
        await TestRoles.AssignAsync(userId, manager);
        await TestRoles.AssignAsync(userId, cashier);
        await TestRoles.GrantAsync(manager, "staff.read");
        await TestRoles.GrantAsync(manager, "brand.read");
        await TestRoles.GrantAsync(cashier, "staff.read");
        await TestRoles.GrantAsync(cashier, "order.confirm");

        var body = await JsonAsync(await SendAsync(HttpMethod.Get, MePath, Token("uid-staff-2")));

        PermissionsOf(body).ShouldBe(["brand.read", "order.confirm", "staff.read"]);
        body.EnumerateObject().Select(p => p.Name)
            .ShouldBe(["id", "name", "email", "isStaff", "mustChangePassword", "permissions"], ignoreOrder: true);
    }

    [Test]
    public async Task R_SEC_03_me_of_a_user_without_roles_lists_no_permission()
    {
        await TestUsers.InsertAsync(CustomerUid, isStaff: false);

        var body = await JsonAsync(await SendAsync(HttpMethod.Get, MePath, Token(CustomerUid)));

        body.GetProperty("permissions").ValueKind.ShouldBe(JsonValueKind.Array);
        PermissionsOf(body).ShouldBeEmpty();
    }

    [Test]
    public async Task R_SEC_03_patch_me_returns_the_same_keys_as_get()
    {
        var userId = await TestUsers.InsertAsync("uid-staff-2");
        var role = await TestRoles.InsertAsync("Store Manager");
        await TestRoles.AssignAsync(userId, role);
        await TestRoles.GrantAsync(role, "staff.read");
        await TestRoles.GrantAsync(role, "brand.update");
        var read = await SendAsync(HttpMethod.Get, MePath, Token("uid-staff-2"));

        var renamed = await RenameAsync("uid-staff-2", new { name = "Trần Thị Bình" }, read.Headers.ETag!.Tag);

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        PermissionsOf(await JsonAsync(renamed)).ShouldBe(PermissionsOf(await JsonAsync(read)));
        PermissionsOf(await JsonAsync(renamed)).ShouldBe(["brand.update", "staff.read"]);
    }

    [Test]
    public async Task R_SEC_03_me_shows_a_role_change_on_the_next_call()
    {
        var userId = await TestUsers.InsertAsync("uid-staff-2");
        var kept = await TestRoles.InsertAsync("Store Manager");
        var removed = await TestRoles.InsertAsync("Cashier");
        await TestRoles.AssignAsync(userId, kept);
        await TestRoles.AssignAsync(userId, removed);
        await TestRoles.GrantAsync(kept, "staff.read");
        await TestRoles.GrantAsync(removed, "brand.read");
        await TestRoles.GrantAsync(await TestRoles.InsertAsync("Accountant"), "staff.disable"); // the permission row
        // Two calls: the first records the sign-in and bumps the user version, the second is served from the cache.
        (await PermissionsAsync("uid-staff-2")).ShouldBe(["brand.read", "staff.read"]);
        (await PermissionsAsync("uid-staff-2")).ShouldBe(["brand.read", "staff.read"]);

        // Through EF, so AccessCacheInvalidationInterceptor bumps the user and the role after the commit (R-SEC-07).
        await Factory.WithDbAsync(async db =>
        {
            db.UserRoles.Remove(await db.UserRoles.SingleAsync(ur => ur.UserId == userId && ur.RoleId == removed));
            var role = await db.Roles.Include(r => r.RolePermissions).SingleAsync(r => r.Id == kept);
            role.GrantPermission((await db.Permissions.SingleAsync(p => p.Key == "staff.disable")).Id);
            await db.SaveChangesAsync();
        });

        (await PermissionsAsync("uid-staff-2")).ShouldBe(["staff.disable", "staff.read"]);
    }

    // ---- helpers ----

    private static string[] PermissionsOf(JsonElement body) =>
        [.. body.GetProperty("permissions").EnumerateArray().Select(key => key.GetString()!)];

    private async Task<string[]> PermissionsAsync(string uid)
    {
        var response = await SendAsync(HttpMethod.Get, MePath, Token(uid));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return PermissionsOf(await JsonAsync(response));
    }

    /// <summary>The token's lifetime follows the real clock (JWT validation); its <c>auth_time</c> the API's fake clock.</summary>
    private string Token(string uid, DateTimeOffset? authTime = null) =>
        TestTokens.EmulatorToken(uid, authTime: authTime ?? Now);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null,
        string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> RenameAsync(string uid, object body, string? ifMatch) =>
        SendAsync(HttpMethod.Patch, MePath, Token(uid), body, ifMatch);

    private Task<HttpResponseMessage> ChangePasswordAsync(string? token, string password) =>
        SendAsync(HttpMethod.Post, ChangePasswordPath, token, new { newPassword = password });

    private async Task<string> ETagAsync(string uid) =>
        (await SendAsync(HttpMethod.Get, MePath, Token(uid))).Headers.ETag!.Tag;

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task ShouldHaveChangedNothingAsync()
    {
        Auth.ChangePasswordCalls.ShouldBe(0);
        Auth.Find(StaffEmail)!.Password.ShouldBe(FakeAuthProvider.DefaultPassword);
        (await StaffRowAsync(_staffId)).MustChangePassword.ShouldBeTrue();
        Auth.Revoked.ShouldBeEmpty();
    }

    private sealed record UserRow(string Name, string? Email, Guid? UpdatedBy, DateTimeOffset UpdatedAt);

    private sealed record StaffRow(bool MustChangePassword, Guid? UpdatedBy, DateTimeOffset UpdatedAt);

    private static async Task<UserRow> UserRowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT name, email, updated_by, updated_at FROM users WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new UserRow(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2), new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero));
    }

    private static async Task<StaffRow> StaffRowAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT must_change_password, updated_by, updated_at FROM staff_profiles WHERE user_id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new StaffRow(reader.GetBoolean(0), reader.IsDBNull(1) ? null : reader.GetGuid(1),
            new DateTimeOffset(reader.GetDateTime(2), TimeSpan.Zero));
    }

    private static async Task<uint> XminAsync(Guid id)
    {
        await using var connection = await TestRoles.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT xmin::text::bigint FROM users WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        return (uint)(long)(await command.ExecuteScalarAsync())!;
    }
}
