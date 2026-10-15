using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using ZenGear.Api.Extensions;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Extensions;

/// <summary>
/// CORS for the web (DEC-104 (9)) against PostgreSQL and Redis: <see cref="ZenGearApiFactory.WebOrigin"/> is allowed,
/// every route answers its preflight without a token, and every answer to the web, refusals and errors included,
/// carries <c>Access-Control-Allow-Origin</c>, so the web can read the Problem Details body.
/// </summary>
public class CorsTests : IntegrationTestBase
{
    private const string Origin = ZenGearApiFactory.WebOrigin;
    private const string MePath = "/api/v1/admin/me";
    private const string Uid = "uid-cors";

    // ---- preflight ----

    [Test]
    public async Task Preflight_from_the_web_origin_is_answered_without_a_token()
    {
        var response = await PreflightAsync(MePath, "PATCH", "authorization,content-type,if-match");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
        Header(response, "Access-Control-Allow-Methods").ShouldNotBeNull().ShouldContain("PATCH");
        Header(response, "Access-Control-Allow-Headers").ShouldNotBeNull().ShouldContain("if-match", Case.Insensitive);
        Header(response, "Access-Control-Max-Age").ShouldBe(CorsSetup.PreflightMaxAge.TotalSeconds.ToString(CultureInfo.InvariantCulture));
        Header(response, "Access-Control-Allow-Credentials").ShouldBeNull();
    }

    [Test]
    public async Task Preflight_of_a_put_allows_if_match_and_idempotency_key()
    {
        var response = await PreflightAsync($"/api/v1/admin/business-locations/{Guid.NewGuid()}", "PUT",
            "authorization,content-type,if-match,idempotency-key");

        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
        Header(response, "Access-Control-Allow-Methods").ShouldNotBeNull().ShouldContain("PUT");
        Header(response, "Access-Control-Allow-Headers").ShouldNotBeNull().ShouldContain("if-match", Case.Insensitive);
        Header(response, "Access-Control-Allow-Headers").ShouldNotBeNull().ShouldContain("idempotency-key", Case.Insensitive);
    }

    [Test]
    public async Task Preflight_of_a_route_that_does_not_exist_is_answered_too()
    {
        var response = await PreflightAsync("/api/v1/admin/no-such-route", "GET", "authorization");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
    }

    [Test]
    public async Task Preflight_from_another_origin_gets_no_cors_header()
    {
        var response = await PreflightAsync(MePath, "PATCH", "authorization,if-match", origin: "https://other.example");

        Header(response, "Access-Control-Allow-Origin").ShouldBeNull();
        Header(response, "Access-Control-Allow-Methods").ShouldBeNull();
        Header(response, "Access-Control-Allow-Headers").ShouldBeNull();
    }

    [Test]
    public async Task Preflight_for_a_header_outside_the_list_does_not_allow_it()
    {
        var response = await PreflightAsync(MePath, "GET", "x-custom");

        // The answer lists the allowed headers whatever was asked; the browser refuses a header outside them.
        Header(response, "Access-Control-Allow-Headers").ShouldNotBeNull().ShouldNotContain("x-custom", Case.Insensitive);
    }

    // ---- answers ----

    [Test]
    public async Task Answer_to_the_web_origin_exposes_etag_and_location()
    {
        await TestUsers.InsertAsync(Uid);

        var response = await SendAsync(HttpMethod.Get, MePath, Token(Uid));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
        Header(response, "Access-Control-Expose-Headers").ShouldNotBeNull().ShouldContain("ETag");
        Header(response, "Access-Control-Expose-Headers").ShouldNotBeNull().ShouldContain("Location");
    }

    [Test]
    public async Task Answer_to_another_origin_has_no_cors_header()
    {
        await TestUsers.InsertAsync(Uid);

        var response = await SendAsync(HttpMethod.Get, MePath, Token(Uid), origin: "https://other.example");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Header(response, "Access-Control-Allow-Origin").ShouldBeNull();
    }

    [Test]
    public async Task Answer_401_without_a_token_carries_the_origin()
    {
        var response = await SendAsync(HttpMethod.Get, MePath, token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
    }

    [Test]
    public async Task Answer_403_account_disabled_carries_the_origin()
    {
        await TestUsers.InsertAsync(Uid, status: "DISABLED");

        var response = await SendAsync(HttpMethod.Get, MePath, Token(Uid));

        await response.ShouldBeForbiddenProblemAsync("/problems/account-disabled", "R-SEC-04");
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
    }

    [Test]
    public async Task Answer_403_password_change_required_carries_the_origin()
    {
        await TestUsers.InsertAsync(Uid, mustChangePassword: true);

        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/test/me", Token(Uid));

        await response.ShouldBeForbiddenProblemAsync("/problems/password-change-required", "R-USR-03");
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
    }

    [Test]
    public async Task Answer_403_permission_denied_carries_the_origin()
    {
        await TestUsers.InsertAsync(Uid);

        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/test/permission", Token(Uid));

        await response.ShouldBeForbiddenProblemAsync("/problems/permission-denied", "R-SEC-03");
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
    }

    [Test]
    public async Task Answer_412_of_a_stale_if_match_carries_the_origin()
    {
        await TestUsers.InsertAsync(Uid);

        var response = await SendAsync(HttpMethod.Patch, MePath, Token(Uid), new { name = "Trần Thị Bình" }, ifMatch: "\"1\"");

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
    }

    // Thrown exceptions are written by UseExceptionHandler, which clears the response first.

    [Test]
    public async Task Answer_400_of_a_thrown_validation_exception_carries_the_origin()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/test/validation", token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
    }

    [Test]
    public async Task Answer_500_of_an_unexpected_exception_carries_the_origin()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/admin/test/unexpected", token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        Header(response, "Access-Control-Allow-Origin").ShouldBe(Origin);
    }

    // ---- configuration ----

    [Test]
    public void A_wildcard_origin_fails_at_start_up()
    {
        using var factory = new ZenGearApiFactory();
        using var wildcard = factory.WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", "*"));

        Should.Throw<OptionsValidationException>(() => wildcard.CreateClient())
            .Message.ShouldContain("Cors:AllowedOrigins");
    }

    // ---- helpers ----

    private static string Token(string uid) => TestTokens.EmulatorToken(uid);

    private async Task<HttpResponseMessage> PreflightAsync(string path, string method, string headers, string origin = Origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri(path, UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null,
        string? ifMatch = null, string origin = Origin)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        request.Headers.Add("Origin", origin);
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

    /// <returns>The values of the header joined by commas; null when the answer has none.</returns>
    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
