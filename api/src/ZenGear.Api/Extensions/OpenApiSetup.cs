using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using ZenGear.Api.Controllers.Identity;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Api.Extensions;

public static class OpenApiSetup
{
    public const string DocumentName = "v1";

    private const string BearerScheme = "Bearer";

    /// <summary>The property of a DTO that holds its <c>xmin</c>, never serialized.</summary>
    private const string VersionProperty = "Version";

    /// <summary>
    /// The OpenAPI document written to api/openapi/ZenGear.Api.json at build time — the contract of web/.
    /// </summary>
    public static IServiceCollection AddZenGearOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options => options
            .AddDocumentTransformer(TransformDocument)
            .AddSchemaTransformer(TransformProblemDetailsSchema)
            .AddOperationTransformer(DeclareETagHeader)
            .AddOperationTransformer(DeclareStaffListValues));

    /// <summary>
    /// The <c>ETag</c> response header on every 200 and 201 whose body is a versioned resource: a DTO with a
    /// <c>[JsonIgnore] uint Version</c> property, which its action sends with <see cref="ETagExtensions.SetETag"/>
    /// (docs/API_CONVENTIONS.md, Concurrency and idempotency). The web reads it to send <c>If-Match</c>.
    /// </summary>
    private static Task DeclareETagHeader(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        foreach (var responseType in context.Description.SupportedResponseTypes)
        {
            if (responseType.StatusCode is StatusCodes.Status200OK or StatusCodes.Status201Created
                && IsVersioned(responseType.Type)
                && operation.Responses is { } responses
                && responses.TryGetValue(responseType.StatusCode.ToString(CultureInfo.InvariantCulture), out var found)
                && found is OpenApiResponse response)
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                response.Headers[HeaderNames.ETag] = new OpenApiHeader
                {
                    Description = "Version of the resource; send it back in If-Match to change it.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                };
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsVersioned(Type? type) =>
        type?.GetProperty(VersionProperty) is { } version
        && version.PropertyType == typeof(uint)
        && version.IsDefined(typeof(JsonIgnoreAttribute), inherit: false);

    /// <summary>
    /// The values <c>status</c> and <c>sort</c> of <c>GET /api/v1/admin/staff</c> accept, taken from
    /// <see cref="StaffListRequestValidator"/> in ordinal order, so the document is the same at every build. The
    /// parameters stay strings in C#: the validator answers a wrong value with 400 and <c>ruleId</c> R-USR-13.
    /// </summary>
    private static Task DeclareStaffListValues(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor { ActionName: nameof(StaffController.List) } action
            && action.ControllerTypeInfo.AsType() == typeof(StaffController))
        {
            SetEnum(operation, "status", StaffListRequestValidator.Statuses.Keys);
            SetEnum(operation, "sort", StaffListRequestValidator.Sorts);
        }

        return Task.CompletedTask;
    }

    private static void SetEnum(OpenApiOperation operation, string parameterName, IEnumerable<string> values)
    {
        var schema = operation.Parameters?.SingleOrDefault(p => p.Name == parameterName)?.Schema as OpenApiSchema
            ?? throw new InvalidOperationException($"The staff list has no query parameter {parameterName} to describe.");
        schema.Enum = [.. values.Order(StringComparer.Ordinal).Select(value => (JsonNode)JsonValue.Create(value))];
    }

    /// <summary>
    /// The extension members every ZenGear Problem Details carries (docs/API_CONVENTIONS.md, Errors),
    /// so the web gets them as typed fields.
    /// </summary>
    private static Task TransformProblemDetailsSchema(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!typeof(ProblemDetails).IsAssignableFrom(context.JsonTypeInfo.Type))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties["ruleId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Description = "Business rule that was violated, e.g. R-SEC-04; absent when there is none.",
        };
        schema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "W3C trace id of the request, for support.",
        };

        return Task.CompletedTask;
    }

    private static Task TransformDocument(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "ZenGear API";
        document.Info.Version = DocumentName;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Firebase ID token.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
        });

        return Task.CompletedTask;
    }
}
