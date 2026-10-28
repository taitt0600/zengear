using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Api.Extensions;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.ExceptionHandling;

/// <summary>
/// DEC-117 (3): the texts come from the vi-VN resources whatever language the request asks for, and every code and every
/// problem type the API can write has its own text (a missing one would answer the fallback text of
/// <c>/problems/unexpected-error</c>).
/// </summary>
public partial class ProblemTextsTests : IntegrationTestBase
{
    /// <summary>Every problem type the API writes (plan of DEC-117, section 2): the handlers' and the services'.</summary>
    private static readonly string[] ProblemTypes =
    [
        "validation", "unauthenticated", "account-not-registered", "permission-denied", "account-disabled",
        "password-change-required", "recent-login-required", "forbidden", "not-found", "method-not-allowed",
        "not-acceptable", "unsupported-media-type", "concurrency-conflict", "precondition-failed", "if-match-required",
        "internal-error", "unexpected-error",
        "staff-account-required", "email-already-registered", "auth-account-exists", "employee-code-taken", "role-required",
        "role-not-for-staff", "admin-role-not-assignable", "left-date-before-joined-date", "left-date-in-future",
        "own-account-not-lockable", "last-active-admin", "admin-account-not-lockable", "admin-account-not-restorable",
        "account-anonymized", "brand-already-exists", "product-series-already-exists", "brand-retired", "admin-role-missing",
        "admin-already-exists",
    ];

    /// <summary>The limits the validators give to the texts; never a value the user sent (DEC-117 (3)).</summary>
    private static readonly string[] Placeholders = ["MaxLength", "MinLength", "From", "To", "MinDigits", "MaxDigits", "MaxCount"];

    [Test]
    public void Every_code_an_action_declares_for_a_400_has_a_text()
    {
        var codes = DeclaredErrorCodes.All.Where(d => d.Status == StatusCodes.Status400BadRequest).SelectMany(d => d.Codes)
            .Concat(ProblemDetailsSetup.ModelBindingCodes);

        WithoutText(codes).ShouldBeEmpty();
    }

    [Test]
    public void Every_conflict_an_action_declares_for_a_field_is_a_type_with_its_texts()
    {
        var types = DeclaredErrorCodes.All.Where(d => d.Status == StatusCodes.Status409Conflict).SelectMany(d => d.Codes)
            .Select(code => code.ToLowerInvariant().Replace('_', '-')).ToList();

        types.ShouldNotBeEmpty();
        types.ShouldBeSubsetOf(ProblemTypes);
        WithoutText(types.SelectMany(type => new[] { $"{type}.title", $"{type}.detail" })).ShouldBeEmpty();
    }

    [Test]
    public void Every_code_of_every_validator_has_a_text()
    {
        ValidatorCodes.RequestTypes.Count.ShouldBeGreaterThanOrEqualTo(10);
        var codes = ValidatorCodes.RequestTypes.SelectMany(type => ValidatorCodes.Of(Factory.Services, type)).ToList();

        codes.ShouldContain("PASSWORD_TOO_COMMON");
        codes.ShouldContain("NAME_WITHOUT_LETTER_OR_DIGIT");
        WithoutText(codes).ShouldBeEmpty();
    }

    [Test]
    public void Every_problem_type_has_a_title_and_a_detail_but_the_internal_error()
    {
        WithoutText(ProblemTypes.Select(type => $"{type}.title")).ShouldBeEmpty();
        WithoutText(ProblemTypes.Where(type => type != "internal-error").Select(type => $"{type}.detail")).ShouldBeEmpty();
        WithoutText(["internal-error.detail"]).ShouldBe(["internal-error.detail"], "A 500 has no detail (DEC-117 (1)).");
    }

    [Test]
    public void Every_text_uses_only_the_placeholders_of_the_limits()
    {
        var texts = InVietnamese(localizer => localizer.GetAllStrings(includeParentCultures: false).ToList());

        texts.Count.ShouldBeGreaterThan(100);
        var unknown = texts
            .SelectMany(text => Placeholder().Matches(text.Value).Select(match => $"{text.Name}: {match.Value}"))
            .Where(found => !Placeholders.Any(name => found.EndsWith($"{{{name}}}", StringComparison.Ordinal)))
            .ToList();
        unknown.ShouldBeEmpty();
    }

    /// <summary>The keys the vi-VN resources have no text for.</summary>
    private List<string> WithoutText(IEnumerable<string> keys) =>
        InVietnamese(localizer => keys.Distinct(StringComparer.Ordinal).Where(key => localizer[key].ResourceNotFound).ToList());

    /// <summary>The texts are read in the UI culture of the caller: the test sets the one every request gets.</summary>
    private T InVietnamese<T>(Func<IStringLocalizer<ProblemTexts>, T> read)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = LocalizationSetup.DefaultUiCulture;
        try
        {
            return read(Factory.Services.GetRequiredService<IStringLocalizer<ProblemTexts>>());
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [GeneratedRegex(@"\{[^}]*\}", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex Placeholder();

    [TestCase(null)]
    [TestCase("vi-VN")]
    [TestCase("en-US,en;q=0.9")]
    public async Task Texts_are_vietnamese_for_every_accept_language(string? acceptLanguage)
    {
        var texts = await GetTextsAsync("validation", acceptLanguage);

        texts.GetProperty("title").GetString().ShouldBe("Dữ liệu không hợp lệ");
        texts.GetProperty("detail").GetString().ShouldBe("Vui lòng kiểm tra lại các thông tin đã nhập.");
    }

    [TestCase(null)]
    [TestCase("vi-VN")]
    [TestCase("en-US")]
    public async Task Formatting_culture_stays_invariant(string? acceptLanguage)
    {
        var texts = await GetTextsAsync("validation", acceptLanguage);

        texts.GetProperty("culture").GetString().ShouldBe(string.Empty);
    }

    [Test]
    public async Task A_type_without_texts_gets_the_texts_of_the_unexpected_error()
    {
        var texts = await GetTextsAsync("no-such-type", acceptLanguage: null);

        texts.GetProperty("title").GetString().ShouldBe("Không xử lý được yêu cầu");
        texts.GetProperty("detail").GetString().ShouldBe("Đã có lỗi xảy ra. Vui lòng thử lại.");
    }

    private async Task<JsonElement> GetTextsAsync(string problemType, string? acceptLanguage)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/v1/admin/test/problem-texts/{problemType}", UriKind.Relative));
        if (acceptLanguage is not null)
        {
            request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);
        }

        var response = await Client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }
}
