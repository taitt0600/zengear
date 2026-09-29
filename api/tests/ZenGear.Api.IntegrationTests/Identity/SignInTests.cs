using System.Net;
using System.Text.Json;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Identity;

public class SignInTests : IntegrationTestBase
{
    private const string Uid = "staff-uid-1";

    private static readonly DateTimeOffset SignedInAt = WholeSeconds(DateTimeOffset.UtcNow.AddMinutes(-10));

    [SetUp]
    public void ClearSecurityLog() => Factory.SecurityLogs.Clear();

    [Test]
    public async Task R_SEC_04_active_user_gets_me()
    {
        var id = await TestUsers.InsertAsync(Uid);

        var response = await SendAsync("/api/v1/me", Token());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await JsonAsync(response);
        body.GetProperty("id").GetGuid().ShouldBe(id);
        body.GetProperty("isStaff").GetBoolean().ShouldBeTrue();
        body.GetProperty("mustChangePassword").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task R_SEC_04_disabled_user_is_403_with_rule_id()
    {
        await TestUsers.InsertAsync(Uid, status: "DISABLED");

        var response = await SendAsync("/api/v1/me", Token());

        await ShouldBeProblemAsync(response, "/problems/account-disabled", "R-SEC-04");
    }

    [Test]
    public async Task R_SEC_04_anonymized_user_is_403()
    {
        await TestUsers.InsertAsync(Uid, status: "ANONYMIZED", isStaff: false);

        var response = await SendAsync("/api/v1/me", Token());

        await ShouldBeProblemAsync(response, "/problems/account-disabled", "R-SEC-04");
    }

    [Test]
    public async Task R_SEC_04_disabled_user_is_rejected_on_anonymous_endpoint()
    {
        await TestUsers.InsertAsync(Uid, status: "DISABLED");

        var response = await SendAsync("/test/anonymous", Token());

        await ShouldBeProblemAsync(response, "/problems/account-disabled", "R-SEC-04");
    }

    [Test]
    public async Task R_SEC_11_rejected_request_logs_REJECTED()
    {
        var id = await TestUsers.InsertAsync(Uid, status: "DISABLED");

        await SendAsync("/api/v1/me", Token());

        var entry = Factory.SecurityLogs.Entries.ShouldHaveSingleItem();
        entry.EventName.ShouldBe("LoginRejected");
        entry.Properties["Result"].ShouldBe("REJECTED");
        entry.Properties["UserId"].ShouldBe(id);
        (await TestUsers.LastLoginAtAsync(id)).ShouldBeNull();
    }

    [Test]
    public async Task R_SEC_11_first_request_of_session_sets_last_login_at_and_logs_SUCCESS()
    {
        var id = await TestUsers.InsertAsync(Uid);

        using var request = TestTokens.Get("/api/v1/me", Token());
        request.Headers.UserAgent.ParseAdd("ZenGearTest/1.0");
        (await Client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await TestUsers.LastLoginAtAsync(id)).ShouldBe(SignedInAt);
        var entry = Factory.SecurityLogs.Entries.ShouldHaveSingleItem();
        entry.EventName.ShouldBe("LoginSucceeded");
        entry.Properties["Result"].ShouldBe("SUCCESS");
        entry.Properties["UserId"].ShouldBe(id);
        entry.Properties["AuthTime"].ShouldBe(SignedInAt);
        entry.Properties["UserAgent"].ShouldBe("ZenGearTest/1.0");
    }

    [Test]
    public async Task R_SEC_11_later_request_same_session_does_not_update_or_log()
    {
        var id = await TestUsers.InsertAsync(Uid, lastLoginAt: SignedInAt);

        (await SendAsync("/api/v1/me", Token())).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await SendAsync("/test/me", Token())).StatusCode.ShouldBe(HttpStatusCode.OK);

        Factory.SecurityLogs.Entries.ShouldBeEmpty();
        (await TestUsers.LastLoginAtAsync(id)).ShouldBe(SignedInAt);
    }

    [Test]
    public async Task R_SEC_11_new_sign_in_with_later_auth_time_updates_again()
    {
        var id = await TestUsers.InsertAsync(Uid, lastLoginAt: SignedInAt);
        var nextSignIn = SignedInAt.AddMinutes(5);

        await SendAsync("/api/v1/me", Token(nextSignIn));

        (await TestUsers.LastLoginAtAsync(id)).ShouldBe(nextSignIn);
        Factory.SecurityLogs.Entries.ShouldHaveSingleItem().EventName.ShouldBe("LoginSucceeded");
    }

    [Test]
    public async Task R_SEC_11_parallel_first_requests_log_one_SUCCESS()
    {
        var id = await TestUsers.InsertAsync(Uid);
        var token = Token();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => SendAsync("/test/me", token)));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        Factory.SecurityLogs.Entries.Count(e => e.EventName == "LoginSucceeded").ShouldBe(1);
        (await TestUsers.LastLoginAtAsync(id)).ShouldBe(SignedInAt);
    }

    [Test]
    public async Task R_SEC_11_security_log_never_contains_the_token()
    {
        await TestUsers.InsertAsync(Uid);
        await TestUsers.InsertAsync("disabled-uid", status: "DISABLED");
        var token = Token();
        var disabledToken = TestTokens.EmulatorToken("disabled-uid", authTime: SignedInAt);

        await SendAsync("/api/v1/me", token);
        await SendAsync("/api/v1/me", disabledToken);

        Factory.SecurityLogs.Entries.Count.ShouldBe(2);
        foreach (var entry in Factory.SecurityLogs.Entries)
        {
            entry.Message.ShouldNotContain(token);
            entry.Message.ShouldNotContain(disabledToken);
            entry.Properties.Values.ShouldAllBe(v => v == null || (v.ToString() != token && v.ToString() != disabledToken));
        }
    }

    [Test]
    public async Task R_USR_03_password_change_pending_blocks_other_endpoints_403()
    {
        await TestUsers.InsertAsync(Uid, mustChangePassword: true);

        var response = await SendAsync("/test/me", Token());

        await ShouldBeProblemAsync(response, "/problems/password-change-required", "R-USR-03");
    }

    [Test]
    public async Task R_USR_03_me_is_allowed_while_password_change_pending()
    {
        await TestUsers.InsertAsync(Uid, mustChangePassword: true);

        var response = await SendAsync("/api/v1/me", Token());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await JsonAsync(response)).GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task R_USR_03_anonymous_endpoint_is_not_blocked_while_password_change_pending()
    {
        await TestUsers.InsertAsync(Uid, mustChangePassword: true);

        var response = await SendAsync("/test/anonymous", Token());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Unregistered_firebase_uid_is_403_account_not_registered()
    {
        var response = await SendAsync("/test/me", Token());

        await ShouldBeProblemAsync(response, "/problems/account-not-registered", ruleId: null);
        Factory.SecurityLogs.Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task Unregistered_firebase_uid_is_403_on_authorize_endpoint()
    {
        var response = await SendAsync("/test/authorize", Token());

        await ShouldBeProblemAsync(response, "/problems/account-not-registered", ruleId: null);
    }

    [Test]
    public async Task Unregistered_firebase_uid_passes_anonymous_endpoint()
    {
        var response = await SendAsync("/test/anonymous", Token());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Registered_user_passes_authorize_endpoint()
    {
        await TestUsers.InsertAsync(Uid);

        var response = await SendAsync("/test/authorize", Token());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("DISABLED", false, "/api/v1/me")]
    [TestCase(null, false, "/test/me")]
    [TestCase("ACTIVE", true, "/test/me")]
    public async Task Middleware_403_is_problem_json_with_traceId(string? status, bool mustChangePassword, string path)
    {
        if (status is not null)
        {
            await TestUsers.InsertAsync(Uid, status: status, mustChangePassword: mustChangePassword);
        }

        var response = await SendAsync(path, Token());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await JsonAsync(response)).GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
    }

    private static string Token(DateTimeOffset? authTime = null) =>
        TestTokens.EmulatorToken(Uid, authTime: authTime ?? SignedInAt);

    private static DateTimeOffset WholeSeconds(DateTimeOffset value) =>
        DateTimeOffset.FromUnixTimeSeconds(value.ToUnixTimeSeconds());

    private async Task<HttpResponseMessage> SendAsync(string path, string token)
    {
        using var request = TestTokens.Get(path, token);
        return await Client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, string type, string? ruleId)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await JsonAsync(response);
        body.GetProperty("type").GetString().ShouldBe(type);
        if (ruleId is null)
        {
            body.TryGetProperty("ruleId", out _).ShouldBeFalse();
        }
        else
        {
            body.GetProperty("ruleId").GetString().ShouldBe(ruleId);
        }
    }
}
