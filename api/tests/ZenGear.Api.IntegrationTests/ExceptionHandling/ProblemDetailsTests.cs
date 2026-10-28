using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.ExceptionHandling;

/// <summary>
/// The shape of every error (DEC-117 (1)): a <c>/problems/…</c> type, a Vietnamese title and detail (none for a 500),
/// <c>traceId</c>, <c>ruleId</c> when one rule is broken, and <c>errors</c> items with a pointer or a parameter, a code and
/// a text.
/// </summary>
public class ProblemDetailsTests : IntegrationTestBase
{
    private const string ValidationType = ProblemAssertions.ValidationType;

    [Test]
    public async Task Business_rule_violation_is_409_with_ruleId_and_the_texts_of_its_type()
    {
        var problem = await (await GetAsync("business-rule"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/last-active-admin", "R-USR-12");

        problem.GetProperty("title").GetString().ShouldBe("Quản trị viên cuối cùng");
        problem.GetProperty("detail").GetString().ShouldBe("Không thể khóa tài khoản quản trị viên cuối cùng đang hoạt động.");
        problem.TryGetProperty("errors", out _).ShouldBeFalse();
    }

    [Test]
    public async Task Business_rule_about_a_field_has_one_error_with_its_pointer_and_the_type_as_code()
    {
        var problem = await (await GetAsync("business-rule/field")).ShouldBeProblemAsync(HttpStatusCode.Conflict,
            "/problems/email-already-registered", null, ExpectedError.At("#/email", "EMAIL_ALREADY_REGISTERED"));

        problem.GetProperty("errors").GetArrayLength().ShouldBe(1);
        problem.ErrorDetail(ExpectedError.At("#/email", "EMAIL_ALREADY_REGISTERED")).ShouldBe("Email này đã có tài khoản.");
    }

    [Test]
    public async Task Business_rule_of_a_type_without_texts_is_still_409_with_the_fallback_texts()
    {
        var response = await GetAsync("business-rule/without-texts");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await ReadProblemAsync(response);
        problem.GetProperty("type").GetString().ShouldBe("/problems/stock-not-available");
        problem.GetProperty("title").GetString().ShouldBe(ProblemAssertions.FallbackTitle);
        problem.GetProperty("detail").GetString().ShouldBe(ProblemAssertions.FallbackDetail);
        problem.GetProperty("ruleId").GetString().ShouldBe("R-INV-02");
    }

    [Test]
    public async Task Validation_failure_on_a_body_is_400_with_pointers_codes_and_filled_texts_in_order()
    {
        var response = await Client.PostAsJsonAsync(new Uri("/api/v1/admin/test/validation", UriKind.Relative), new { name = "x" });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null);
        problem.GetProperty("title").GetString().ShouldBe("Dữ liệu không hợp lệ");
        problem.GetProperty("detail").GetString().ShouldBe("Vui lòng kiểm tra lại các thông tin đã nhập.");
        var errors = problem.GetProperty("errors");
        errors.GetArrayLength().ShouldBe(2);
        errors[0].GetProperty("pointer").GetString().ShouldBe("#/items/0/unitPrice");
        errors[0].GetProperty("code").GetString().ShouldBe("NAME_TOO_LONG");
        errors[0].GetProperty("detail").GetString().ShouldBe("Tên tối đa 21.474.836 ký tự.");
        errors[0].TryGetProperty("parameter", out _).ShouldBeFalse();
        errors[1].GetProperty("pointer").GetString().ShouldBe("#/regularHours/1/day");
        errors[1].GetProperty("code").GetString().ShouldBe("DAY_REQUIRED");
    }

    [Test]
    public async Task Validation_failure_of_an_action_without_a_body_names_the_parameter()
    {
        var problem = await (await GetAsync("validation")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
            ExpectedError.OnParameter("pageSize", "PAGE_SIZE_OUT_OF_RANGE"));

        problem.ErrorDetail(ExpectedError.OnParameter("pageSize", "PAGE_SIZE_OUT_OF_RANGE"))
            .ShouldBe("Số dòng mỗi trang phải từ 1 đến 100.");
        problem.GetProperty("errors")[0].TryGetProperty("pointer", out _).ShouldBeFalse();
    }

    [Test]
    public async Task A_failure_without_a_code_of_the_api_is_VALUE_INVALID_and_a_rule_id_as_code_names_no_rule()
    {
        var problem = await (await GetAsync("validation/without-codes")).ShouldBeProblemAsync(HttpStatusCode.BadRequest,
            ValidationType, null,
            ExpectedError.OnParameter("name", "VALUE_INVALID"),
            ExpectedError.OnParameter("email", "VALUE_INVALID"),
            ExpectedError.OnParameter("phone", "VALUE_INVALID"));

        problem.ErrorCodes().ShouldBe(["VALUE_INVALID", "VALUE_INVALID", "VALUE_INVALID"]);
    }

    [Test]
    public async Task Validation_failure_names_the_rule_its_failures_share_through_their_state()
    {
        var problem = await (await GetAsync("validation-rule")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType,
            "R-USR-11",
            ExpectedError.OnParameter("newPassword", "PASSWORD_LENGTH_OUT_OF_RANGE"),
            ExpectedError.OnParameter("newPassword", "PASSWORD_TOO_COMMON"));

        problem.ErrorDetail(ExpectedError.OnParameter("newPassword", "PASSWORD_LENGTH_OUT_OF_RANGE"))
            .ShouldBe("Mật khẩu mới dài từ 12 đến 128 ký tự.");
    }

    [Test]
    public async Task Validation_failure_has_no_ruleId_when_the_failures_do_not_share_one() =>
        await (await GetAsync("validation-mixed-rules")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
            ExpectedError.OnParameter("newPassword", "PASSWORD_TOO_COMMON"),
            ExpectedError.OnParameter("name", "NAME_REQUIRED"));

    [Test]
    public async Task Bad_request_rule_names_the_rule_of_a_binding_400_with_the_code_of_the_parameter()
    {
        var problem = await (await GetAsync("bad-request-rule/binding?number=x")).ShouldBeProblemAsync(HttpStatusCode.BadRequest,
            ValidationType, TestEndpointsController.BadRequestRuleId, ExpectedError.OnParameter("number", "VALUE_INVALID"));

        problem.GetProperty("errors").GetArrayLength().ShouldBe(1);
        problem.GetRawText().ShouldNotContain("\"x\"");
    }

    [Test]
    public async Task Bad_request_rule_leaves_a_thrown_validation_400_without_a_rule_as_it_is() =>
        await (await GetAsync("bad-request-rule/validation")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
            ExpectedError.OnParameter("name", "NAME_REQUIRED"));

    [Test]
    public async Task Bad_request_rule_never_replaces_the_rule_a_400_already_names() =>
        await (await GetAsync("bad-request-rule/own-rule")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, "R-USR-11");

    [Test]
    public async Task Bad_request_rule_leaves_a_400_the_action_returns_without_a_rule() =>
        await (await GetAsync("bad-request-rule/returned")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null);

    [Test]
    public async Task A_binding_400_of_an_action_without_bad_request_rule_names_no_rule()
    {
        var response = await Client.GetAsync(new Uri("/api/v1/shop/business-locations?page=x", UriKind.Relative));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ValidationType, null,
            ExpectedError.OnParameter("page", "VALUE_INVALID"));
    }

    [Test]
    public async Task A_body_of_another_media_type_is_415_unsupported_media_type()
    {
        using var content = new StringContent("name=x", Encoding.UTF8, "text/plain");

        var problem = await (await Client.PostAsync(new Uri("/api/v1/admin/test/validation", UriKind.Relative), content))
            .ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "/problems/unsupported-media-type", null);

        problem.GetProperty("title").GetString().ShouldBe("Định dạng không được hỗ trợ");
    }

    [Test]
    public async Task Concurrency_conflict_with_If_Match_is_412_precondition_failed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri("/api/v1/admin/test/concurrency", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("If-Match", "\"42\"");

        var problem = await (await Client.SendAsync(request))
            .ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "/problems/precondition-failed", null);

        problem.GetProperty("title").GetString().ShouldBe("Dữ liệu đã thay đổi");
        problem.GetProperty("detail").GetString().ShouldBe("Dữ liệu đã được người khác thay đổi. Đang tải lại.");
    }

    [Test]
    public async Task Concurrency_conflict_without_If_Match_is_409_concurrency_conflict_without_ruleId()
    {
        var response = await Client.PutAsync(new Uri("/api/v1/admin/test/concurrency", UriKind.Relative), content: null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "/problems/concurrency-conflict", null);
    }

    [Test]
    public async Task Unexpected_exception_is_500_internal_error_without_detail_or_internal_message()
    {
        var response = await GetAsync("unexpected");

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError, "/problems/internal-error", null);
        problem.GetProperty("title").GetString().ShouldBe("Lỗi hệ thống");
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("Internal detail");
    }

    [Test]
    public async Task Unknown_route_for_anonymous_caller_is_401_unauthenticated()
    {
        var response = await Client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative));

        // The fallback policy challenges first: an anonymous caller learns nothing about which routes exist.
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "/problems/unauthenticated", null);
        problem.GetProperty("title").GetString().ShouldBe("Chưa đăng nhập");
    }

    [Test]
    public async Task Unknown_route_for_a_signed_in_user_is_404_not_found()
    {
        await TestUsers.InsertAsync("uid-problem-details");
        using var request = TestTokens.Get("/api/v1/admin/does-not-exist", TestTokens.EmulatorToken("uid-problem-details"));

        var problem = await (await Client.SendAsync(request)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "/problems/not-found", null);

        problem.GetProperty("title").GetString().ShouldBe("Không tìm thấy");
    }

    private Task<HttpResponseMessage> GetAsync(string path) =>
        Client.GetAsync(new Uri($"/api/v1/admin/test/{path}", UriKind.Relative));

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }
}
