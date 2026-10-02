using System.Net;
using System.Text.Json;
using Shouldly;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

public static class ProblemAssertions
{
    public static Task ShouldBeForbiddenProblemAsync(this HttpResponseMessage response, string type, string? ruleId) =>
        response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, type, ruleId);

    public static async Task ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string type, string? ruleId)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
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
