using System.Net;
using System.Text.Json;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.Errors;

public class ProblemDetailsTests : IntegrationTestBase
{
    [Test]
    public async Task Business_rule_violation_is_409_with_ruleId_and_traceId()
    {
        var response = await Client.GetAsync(new Uri("/test/business-rule", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("type").GetString().ShouldBe("/problems/stock-not-available");
        problem.GetProperty("title").GetString().ShouldBe("Stock not available");
        problem.GetProperty("status").GetInt32().ShouldBe(409);
        problem.GetProperty("detail").GetString().ShouldBe("SKU L-1 has 1 unit available, 2 requested.");
        problem.GetProperty("ruleId").GetString().ShouldBe("R-INV-02");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Validation_failure_is_400_with_errors_keyed_by_camelCase_path()
    {
        var response = await Client.GetAsync(new Uri("/test/validation", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("status").GetInt32().ShouldBe(400);
        problem.GetProperty("errors").GetProperty("items[0].unitPrice")[0].GetString()
            .ShouldBe("Unit price must be greater than 0.");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Concurrency_conflict_with_If_Match_is_412()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri("/test/concurrency", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("If-Match", "\"42\"");

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("status").GetInt32().ShouldBe(412);
    }

    [Test]
    public async Task Concurrency_conflict_without_If_Match_is_409_concurrency_conflict_without_ruleId()
    {
        var response = await Client.PutAsync(new Uri("/test/concurrency", UriKind.Relative), content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("type").GetString().ShouldBe("/problems/concurrency-conflict");
        problem.TryGetProperty("ruleId", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Unexpected_exception_is_500_without_internal_detail()
    {
        var response = await Client.GetAsync(new Uri("/test/unexpected", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("Internal detail");
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Test]
    public async Task Unknown_route_for_anonymous_caller_is_401_problem_details()
    {
        var response = await Client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative));

        // The fallback policy challenges first: an anonymous caller learns nothing about which routes exist.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }
}
