using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;

namespace ZenGear.Api.IntegrationTests.Infrastructure;

/// <summary>An item of <c>errors</c> a problem must hold (DEC-117 (1)): a body field's pointer or a parameter, and the code.</summary>
public sealed record ExpectedError(string? Pointer, string? Parameter, string Code)
{
    /// <summary>A field of the body: <c>#/name</c>, <c>#/regularHours/0/day</c>.</summary>
    public static ExpectedError At(string pointer, string code) => new(pointer, null, code);

    /// <summary>A query or route parameter: <c>page</c>, <c>roleId</c>.</summary>
    public static ExpectedError OnParameter(string parameter, string code) => new(null, parameter, code);
}

public static partial class ProblemAssertions
{
    public const string ValidationType = "/problems/validation";

    /// <summary>The texts of <c>/problems/unexpected-error</c>: what a type or a code without its own text gets.</summary>
    public const string FallbackTitle = "Không xử lý được yêu cầu";

    public const string FallbackDetail = "Đã có lỗi xảy ra. Vui lòng thử lại.";

    public const string UnexpectedErrorType = "/problems/unexpected-error";

    public static Task<JsonElement> ShouldBeForbiddenProblemAsync(this HttpResponseMessage response, string type, string? ruleId) =>
        response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, type, ruleId);

    /// <summary>
    /// The status, the media type, the type and the <c>ruleId</c> (none when null), a title of its own (a type without
    /// a text would get the fallback title), a detail except for a 500, and every item of <c>errors</c> well formed;
    /// <paramref name="errors"/> are items the problem must hold among its others.
    /// </summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string type,
        string? ruleId, params ExpectedError[] errors)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("type").GetString().ShouldBe(type);
        body.GetProperty("status").GetInt32().ShouldBe((int)status);
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrEmpty();
        if (ruleId is null)
        {
            body.TryGetProperty("ruleId", out _).ShouldBeFalse();
        }
        else
        {
            body.GetProperty("ruleId").GetString().ShouldBe(ruleId);
        }

        var isFallback = type == UnexpectedErrorType;
        var title = body.GetProperty("title").GetString();
        title.ShouldNotBeNullOrWhiteSpace();
        if (!isFallback)
        {
            title.ShouldNotBe(FallbackTitle, $"{type} has no title of its own.");
        }

        if (status == HttpStatusCode.InternalServerError)
        {
            body.TryGetProperty("detail", out _).ShouldBeFalse();
        }
        else
        {
            var detail = body.GetProperty("detail").GetString();
            detail.ShouldNotBeNullOrWhiteSpace();
            if (!isFallback)
            {
                detail.ShouldNotBe(FallbackDetail, $"{type} has no detail of its own.");
            }
        }

        var items = body.TryGetProperty("errors", out var found) ? found.EnumerateArray().ToList() : [];
        if (status == HttpStatusCode.Conflict && errors.Length == 0)
        {
            // Only a conflict about one field of the request names it (DEC-117 (1)).
            items.ShouldBeEmpty($"{type} names a field: {found}");
        }

        foreach (var item in items)
        {
            ShouldBeWellFormed(item);
        }

        foreach (var expected in errors)
        {
            items.ShouldContain(item => Matches(item, expected), $"errors holds no {expected}: {found}");
        }

        return body;
    }

    /// <summary>The codes of <c>errors</c>, in order.</summary>
    public static IReadOnlyList<string> ErrorCodes(this JsonElement problem) =>
    [
        .. problem.GetProperty("errors").EnumerateArray().Select(item => item.GetProperty("code").GetString()!),
    ];

    /// <summary>The detail of the item at <paramref name="expected"/>.</summary>
    public static string ErrorDetail(this JsonElement problem, ExpectedError expected) =>
        problem.GetProperty("errors").EnumerateArray().Single(item => Matches(item, expected)).GetProperty("detail").GetString()!;

    private static void ShouldBeWellFormed(JsonElement item)
    {
        var hasPointer = item.TryGetProperty("pointer", out var pointer);
        var hasParameter = item.TryGetProperty("parameter", out var parameter);
        (hasPointer ^ hasParameter).ShouldBeTrue($"An item names a pointer or a parameter, not both or neither: {item}");
        if (hasPointer)
        {
            pointer.GetString()!.ShouldStartWith("#");
        }
        else
        {
            parameter.GetString().ShouldNotBeNullOrWhiteSpace();
        }

        CodePattern().IsMatch(item.GetProperty("code").GetString()!).ShouldBeTrue(item.ToString());
        var detail = item.GetProperty("detail").GetString();
        detail.ShouldNotBeNullOrWhiteSpace();
        detail.ShouldNotBe(FallbackDetail, $"The code has no text of its own: {item}");
        detail.ShouldNotContain("{", customMessage: $"A placeholder was left unfilled: {item}");
    }

    private static bool Matches(JsonElement item, ExpectedError expected) =>
        item.GetProperty("code").GetString() == expected.Code
        && StringOf(item, "pointer") == expected.Pointer
        && StringOf(item, "parameter") == expected.Parameter;

    private static string? StringOf(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) ? value.GetString() : null;

    [GeneratedRegex(@"^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex CodePattern();
}
