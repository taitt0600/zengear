using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using ZenGear.Api.Attributes;
using ZenGear.Api.Controllers.Identity;
using ZenGear.Api.ExceptionHandling;
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
            .AddOperationTransformer(DeclareStaffListValues)
            .AddOperationTransformer(DeclareErrorCodes));

    /// <summary>
    /// The codes of <c>errors</c> each operation can answer (DEC-117 (2)), so the web gets a union per endpoint: for the
    /// 400 its <see cref="ErrorCodesAttribute"/> codes and the model-binding codes, for a 409 that names a field its
    /// codes. Each becomes the enum component <c>&lt;Controller&gt;&lt;Action&gt;ErrorCode</c> (or <c>…ConflictCode</c>),
    /// in ordinal order so the document is the same at every build, and the response schema becomes
    /// <see cref="ValidationProblem"/> with <c>errors[].code</c> narrowed to it.
    /// </summary>
    private static Task DeclareErrorCodes(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Responses is not { } responses || context.Document is not { } document
            || context.Description.ActionDescriptor is not ControllerActionDescriptor action)
        {
            return Task.CompletedTask;
        }

        var declared = action.EndpointMetadata.OfType<ErrorCodesAttribute>().ToList();
        foreach (var (status, suffix) in new[] { (StatusCodes.Status400BadRequest, "ErrorCode"), (StatusCodes.Status409Conflict, "ConflictCode") })
        {
            var codes = declared.Where(a => a.Status == status).SelectMany(a => a.Codes)
                .Concat(status == StatusCodes.Status400BadRequest ? ProblemDetailsSetup.ModelBindingCodes : [])
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            if (codes.Count == 0
                || !responses.TryGetValue(status.ToString(CultureInfo.InvariantCulture), out var found)
                || found is not OpenApiResponse { Content: { } content }
                || !content.TryGetValue("application/problem+json", out var media))
            {
                continue;
            }

            if (media.Schema is not OpenApiSchemaReference { Reference.Id: nameof(ValidationProblem) } problem)
            {
                throw new InvalidOperationException(
                    $"{action.DisplayName} declares error codes for {status}, but its response is not a {nameof(ValidationProblem)}.");
            }

            var enumName = action.ControllerName + action.ActionName + suffix;
            document.AddComponent<IOpenApiSchema>(enumName, new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [.. codes.Select(code => (JsonNode)JsonValue.Create(code))],
            });
            media.Schema = new OpenApiSchema
            {
                AllOf =
                [
                    problem,
                    new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["errors"] = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Array,
                                Items = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.Object,
                                    Properties = new Dictionary<string, IOpenApiSchema>
                                    {
                                        ["code"] = new OpenApiSchemaReference(enumName, document),
                                    },
                                },
                            },
                        },
                    },
                ],
            };
        }

        return Task.CompletedTask;
    }

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
        // An item names a pointer or a parameter, the other one is left out (never null).
        if (context.JsonTypeInfo.Type == typeof(ProblemError))
        {
            foreach (var place in new[] { "pointer", "parameter" })
            {
                schema.Required?.Remove(place);
                if (schema.Properties is { } properties && properties.TryGetValue(place, out var found)
                    && found is OpenApiSchema property)
                {
                    property.Type = JsonSchemaType.String;
                }
            }

            return Task.CompletedTask;
        }

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
