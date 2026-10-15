using System.Text.Json;
using Shouldly;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Api.IntegrationTests.Extensions;

/// <summary>
/// The committed contract <c>api/openapi/ZenGear.Api.json</c> (the CI job api-test proves it is the generated one):
/// what the web reads from it beyond the controllers' attributes (DEC-104 (9)).
/// </summary>
public class OpenApiDocumentTests
{
    private static readonly JsonElement Document = ReadDocument();

    [Test]
    public void Etag_is_declared_on_every_answer_of_a_versioned_resource_and_nowhere_else()
    {
        var withETag =
            from path in Document.GetProperty("paths").EnumerateObject()
            from operation in path.Value.EnumerateObject()
            from response in operation.Value.GetProperty("responses").EnumerateObject()
            where response.Value.TryGetProperty("headers", out var headers) && headers.TryGetProperty("ETag", out _)
            select $"{operation.Name.ToUpperInvariant()} {path.Name} {response.Name}";

        withETag.ShouldBe(
        [
            "GET /api/v1/admin/business-locations/{id} 200",
            "PUT /api/v1/admin/business-locations/{id} 200",
            "GET /api/v1/admin/me 200",
            "PATCH /api/v1/admin/me 200",
            "POST /api/v1/admin/brands 201",
            "GET /api/v1/admin/brands/{id} 200",
            "PUT /api/v1/admin/brands/{id} 200",
            "POST /api/v1/admin/product-series 201",
            "GET /api/v1/admin/product-series/{id} 200",
            "PUT /api/v1/admin/product-series/{id} 200",
        ], ignoreOrder: true);
    }

    [Test]
    public void Etag_header_is_a_string()
    {
        var header = Document.GetProperty("paths").GetProperty("/api/v1/admin/me").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("headers").GetProperty("ETag");

        header.GetProperty("schema").GetProperty("type").GetString().ShouldBe("string");
    }

    [Test]
    public void Staff_list_status_and_sort_are_enums_of_the_validator_values()
    {
        var parameters = Document.GetProperty("paths").GetProperty("/api/v1/admin/staff").GetProperty("get")
            .GetProperty("parameters");

        EnumOf(parameters, "status").ShouldBe([.. StaffListRequestValidator.Statuses.Keys.Order(StringComparer.Ordinal)]);
        EnumOf(parameters, "sort").ShouldBe([.. StaffListRequestValidator.Sorts.Order(StringComparer.Ordinal)]);
    }

    [Test]
    public void Current_user_requires_its_permissions_as_strings()
    {
        var schema = Document.GetProperty("components").GetProperty("schemas").GetProperty("CurrentUserDto");

        schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()).ShouldContain("permissions");
        var permissions = schema.GetProperty("properties").GetProperty("permissions");
        permissions.GetProperty("type").GetString().ShouldBe("array");
        permissions.GetProperty("items").GetProperty("type").GetString().ShouldBe("string");
    }

    private static string?[] EnumOf(JsonElement parameters, string name) =>
    [
        .. parameters.EnumerateArray().Single(p => p.GetProperty("name").GetString() == name)
            .GetProperty("schema").GetProperty("enum").EnumerateArray().Select(value => value.GetString()),
    ];

    private static JsonElement ReadDocument()
    {
        // The second copy of the root search of ZenGear.ArchitectureTests (RepositoryLayout): the test projects do not
        // reference each other.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "api", "ZenGear.slnx")))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "api", "openapi", "ZenGear.Api.json")));
                return document.RootElement.Clone();
            }
        }

        throw new InvalidOperationException($"Repository root (api/ZenGear.slnx) not found above {AppContext.BaseDirectory}.");
    }
}
