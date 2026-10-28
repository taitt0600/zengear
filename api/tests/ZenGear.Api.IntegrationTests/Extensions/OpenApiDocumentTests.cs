using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Shouldly;
using ZenGear.Api.Extensions;
using ZenGear.Api.IntegrationTests.Infrastructure;
using ZenGear.Services.Catalog.Brands;
using ZenGear.Services.Catalog.Series;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Account;
using ZenGear.Services.Identity.StaffAccounts;
using ZenGear.Services.Locations.BusinessLocations;

namespace ZenGear.Api.IntegrationTests.Extensions;

/// <summary>
/// The committed contract <c>api/openapi/ZenGear.Api.json</c> (the CI job api-test proves it is the generated one):
/// what the web reads from it beyond the controllers' attributes (DEC-104 (9)), and the codes of <c>errors</c> of each
/// operation (DEC-117 (2)), checked against the validators the test host resolves.
/// </summary>
public class OpenApiDocumentTests : IntegrationTestBase
{
    private const string Schemas = "#/components/schemas/";

    private static readonly JsonElement Document = ReadDocument();

    /// <summary>
    /// Every operation that answers a 400, with the request its validator checks (none when only model binding refuses
    /// its input): the enum of its codes must hold every code of that validator.
    /// </summary>
    private static readonly (string Method, string Path, Type? Request)[] BadRequests =
    [
        ("GET", "/api/v1/admin/brands", typeof(PageRequest)),
        ("POST", "/api/v1/admin/brands", typeof(BrandCreateRequest)),
        ("PUT", "/api/v1/admin/brands/{id}", typeof(BrandUpdateRequest)),
        ("GET", "/api/v1/admin/product-series", typeof(PageRequest)),
        ("POST", "/api/v1/admin/product-series", typeof(ProductSeriesCreateRequest)),
        ("PUT", "/api/v1/admin/product-series/{id}", typeof(ProductSeriesUpdateRequest)),
        ("PATCH", "/api/v1/admin/me", typeof(CurrentUserUpdateRequest)),
        ("POST", "/api/v1/admin/me/change-password", typeof(CurrentUserPasswordChangeRequest)),
        ("POST", "/api/v1/admin/staff", typeof(StaffCreateRequest)),
        ("GET", "/api/v1/admin/staff", typeof(StaffListRequest)),
        ("POST", "/api/v1/admin/staff/{id}/disable", null),
        ("GET", "/api/v1/admin/business-locations", typeof(PageRequest)),
        ("PUT", "/api/v1/admin/business-locations/{id}", typeof(BusinessLocationUpdateRequest)),
        ("GET", "/api/v1/shop/business-locations", typeof(PageRequest)),
    ];

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

        ParameterEnumOf(parameters, "status").ShouldBe([.. StaffListRequestValidator.Statuses.Keys.Order(StringComparer.Ordinal)]);
        ParameterEnumOf(parameters, "sort").ShouldBe([.. StaffListRequestValidator.Sorts.Order(StringComparer.Ordinal)]);
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

    [Test]
    public void Every_operation_with_a_400_is_in_the_table_of_its_validator()
    {
        Operations().Where(o => o.Responses.TryGetProperty("400", out _)).Select(o => $"{o.Method} {o.Path}")
            .ShouldBe(BadRequests.Select(b => $"{b.Method} {b.Path}"), ignoreOrder: true);
    }

    [Test]
    public void Every_400_lists_the_declared_codes_and_the_model_binding_codes()
    {
        foreach (var operation in Operations().Where(o => o.Responses.TryGetProperty("400", out _)))
        {
            var (name, codes) = CodesOf(operation.Responses.GetProperty("400"));
            var declared = DeclaredErrorCodes.All
                .SingleOrDefault(d => d.Status == StatusCodes.Status400BadRequest && d.EnumName == name)?.Codes ?? new HashSet<string>();

            codes.ShouldBe([.. declared.Concat(ProblemDetailsSetup.ModelBindingCodes).Distinct().Order(StringComparer.Ordinal)],
                $"{operation.Method} {operation.Path} ({name})");
        }
    }

    [TestCaseSource(nameof(ValidatedOperations))]
    public void The_400_enum_holds_every_code_of_the_validator(string method, string path, Type request)
    {
        var (_, codes) = CodesOf(Document.GetProperty("paths").GetProperty(path).GetProperty(method.ToLowerInvariant())
            .GetProperty("responses").GetProperty("400"));

        var missing = ValidatorCodes.Of(Factory.Services, request).Except(codes).ToList();

        missing.ShouldBeEmpty($"{method} {path}");
    }

    [Test]
    public void Every_409_that_names_a_field_lists_its_declared_codes()
    {
        var conflicts = DeclaredErrorCodes.All.Where(d => d.Status == StatusCodes.Status409Conflict).ToList();
        conflicts.ShouldNotBeEmpty();
        foreach (var declared in conflicts)
        {
            EnumOf(declared.EnumName).ShouldBe([.. declared.Codes.Order(StringComparer.Ordinal)], declared.EnumName);
        }

        foreach (var operation in Operations().Where(o => o.Responses.TryGetProperty("409", out _)))
        {
            var response = operation.Responses.GetProperty("409");
            if (Schema(response).TryGetProperty("allOf", out _))
            {
                var (name, _) = CodesOf(response);
                conflicts.ShouldContain(d => d.EnumName == name, $"{operation.Method} {operation.Path}");
            }
            else
            {
                Schema(response).GetProperty("$ref").GetString().ShouldBe(Schemas + "ProblemDetails");
            }
        }
    }

    [Test]
    public void Every_412_is_a_problem_details()
    {
        var preconditions = Operations().Where(o => o.Responses.TryGetProperty("412", out _)).ToList();

        preconditions.Count.ShouldBeGreaterThanOrEqualTo(4);
        preconditions.ShouldAllBe(o => Schema(o.Responses.GetProperty("412")).GetProperty("$ref").GetString() == Schemas + "ProblemDetails");
    }

    private static IEnumerable<TestCaseData> ValidatedOperations() =>
        BadRequests.Where(b => b.Request is not null)
            .Select(b => new TestCaseData(b.Method, b.Path, b.Request).SetArgDisplayNames(b.Method, b.Path));

    private sealed record OperationEntry(string Method, string Path, JsonElement Responses);

    private static IEnumerable<OperationEntry> Operations() =>
        from path in Document.GetProperty("paths").EnumerateObject()
        from operation in path.Value.EnumerateObject()
        select new OperationEntry(operation.Name.ToUpperInvariant(), path.Name, operation.Value.GetProperty("responses"));

    private static JsonElement Schema(JsonElement response) =>
        response.GetProperty("content").GetProperty("application/problem+json").GetProperty("schema");

    /// <summary><c>allOf: [ValidationProblem, {errors: {items: {code: $ref enum}}}]</c>: the name of the enum and its values.</summary>
    private static (string Name, string[] Codes) CodesOf(JsonElement response)
    {
        var allOf = Schema(response).GetProperty("allOf");
        allOf[0].GetProperty("$ref").GetString().ShouldBe(Schemas + "ValidationProblem");
        var reference = allOf[1].GetProperty("properties").GetProperty("errors").GetProperty("items").GetProperty("properties")
            .GetProperty("code").GetProperty("$ref").GetString()!;
        var name = reference[Schemas.Length..];
        return (name, EnumOf(name));
    }

    private static string[] EnumOf(string component) =>
    [
        .. Document.GetProperty("components").GetProperty("schemas").GetProperty(component).GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString()!),
    ];

    private static string?[] ParameterEnumOf(JsonElement parameters, string name) =>
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
