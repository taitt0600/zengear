using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using ZenGear.Api.Extensions;
using ZenGear.Api.Options;

namespace ZenGear.Api.IntegrationTests.Extensions;

/// <summary>The origins <c>Cors:AllowedOrigins</c> accepts (DEC-104 (9)); no API, no container.</summary>
public class CorsOriginTests
{
    [TestCase("http://localhost:3000")]
    [TestCase("https://zengear.vn")]
    [TestCase("https://admin.zengear.vn:8443")]
    public void An_origin_is_accepted(string origin) => CorsSetup.IsValidOrigin(origin).ShouldBeTrue();

    [TestCase("*")]
    [TestCase("")]
    [TestCase("http://localhost:3000/")]
    [TestCase("http://localhost:3000/app")]
    [TestCase("http://localhost:3000?x=1")]
    [TestCase("localhost:3000")]
    [TestCase("ftp://x.test")]
    [TestCase("http://u@x.test")]
    [TestCase("http://LOCALHOST:3000")]
    [TestCase("https://zengear.vn:443")]
    [TestCase("http://*.zengear.vn")]
    public void Anything_but_a_lowercase_origin_without_default_port_is_refused(string origin) =>
        CorsSetup.IsValidOrigin(origin).ShouldBeFalse();

    [Test]
    public void A_missing_list_is_valid_and_allows_no_origin() =>
        Options(new Dictionary<string, string?>()).AllowedOrigins.ShouldBeEmpty();

    [Test]
    public void A_list_of_origins_is_valid() =>
        Options(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            ["Cors:AllowedOrigins:1"] = "https://zengear.vn",
        }).AllowedOrigins.ShouldBe(["http://localhost:3000", "https://zengear.vn"]);

    [Test]
    public void A_list_with_one_invalid_entry_is_refused() =>
        Should.Throw<OptionsValidationException>(() => Options(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            ["Cors:AllowedOrigins:1"] = "*",
        })).Message.ShouldContain("Cors:AllowedOrigins");

    private static WebClientCorsOptions Options(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddZenGearCors();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<WebClientCorsOptions>>().Value;
    }
}
