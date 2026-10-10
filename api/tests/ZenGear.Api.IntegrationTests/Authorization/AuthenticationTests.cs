using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Authorization;

public class AuthenticationTests : IntegrationTestBase
{
    private const string Uid = "firebase-uid-1";

    [Test]
    public async Task Emulator_token_with_project_issuer_and_audience_is_accepted()
    {
        await TestUsers.InsertAsync(Uid);

        var response = await GetMeAsync(TestTokens.EmulatorToken(Uid));

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
        var response = await GetMeAsync(TestTokens.EmulatorToken(Uid, projectId: "demo-other-project"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Expired_token_is_401()
    {
        var response = await GetMeAsync(TestTokens.EmulatorToken(Uid, DateTime.UtcNow.AddMinutes(-5)));

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
        using var request = TestTokens.Get("/api/v1/admin/test/me", token);
        return await Client.SendAsync(request);
    }
}
