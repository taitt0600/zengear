using System.Net;
using Shouldly;
using ZenGear.Api.IntegrationTests.Infrastructure;

namespace ZenGear.Api.IntegrationTests;

public class HealthTests : IntegrationTestBase
{
    [Test]
    public async Task Health_is_anonymous_and_reports_Healthy_when_database_is_reachable()
    {
        var response = await Client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }
}
