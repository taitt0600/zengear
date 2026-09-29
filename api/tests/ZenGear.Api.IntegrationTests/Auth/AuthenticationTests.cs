using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Auth;

public class AuthenticationTests : IntegrationTestBase
{
    private const string Uid = "firebase-uid-1";

    [Test]
    public async Task Emulator_token_with_project_issuer_and_audience_is_accepted()
    {
        var response = await GetMeAsync(EmulatorToken(ZenGearApiFactory.FirebaseProjectId, DateTime.UtcNow.AddHours(1)));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("uid").GetString().ShouldBe(Uid);
    }

    [Test]
    public async Task Request_without_token_is_401_problem_details()
    {
        var response = await GetMeAsync(token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Test]
    public async Task Token_for_another_project_is_401()
    {
        var response = await GetMeAsync(EmulatorToken("demo-other-project", DateTime.UtcNow.AddHours(1)));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Expired_token_is_401()
    {
        var response = await GetMeAsync(EmulatorToken(ZenGearApiFactory.FirebaseProjectId, DateTime.UtcNow.AddMinutes(-5)));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public void Emulator_mode_outside_Development_and_Testing_fails_at_start_up()
    {
        using var factory = new ZenGearApiFactory("Production");

        Should.Throw<OptionsValidationException>(() => factory.CreateClient())
            .Message.ShouldContain("UseEmulator");
    }

    private async Task<HttpResponseMessage> GetMeAsync(string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/test/me", UriKind.Relative));
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await Client.SendAsync(request);
    }

    /// <summary>An unsigned ID token (alg "none") shaped like the ones of the Firebase Auth Emulator.</summary>
    private static string EmulatorToken(string projectId, DateTime expires)
    {
        var issuedAt = expires.AddHours(-1);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = $"https://securetoken.google.com/{projectId}",
            Audience = projectId,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Uid,
                ["auth_time"] = EpochTime.GetIntDate(issuedAt),
            },
        });
    }
}
