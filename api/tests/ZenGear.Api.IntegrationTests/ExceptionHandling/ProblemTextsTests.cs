using System.Net;
using System.Text.Json;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests.ExceptionHandling;

/// <summary>DEC-117 (3): the texts come from the vi-VN resources whatever language the request asks for.</summary>
public class ProblemTextsTests : IntegrationTestBase
{
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
