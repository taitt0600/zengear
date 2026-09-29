using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ZenGear.Api.OpenApi;

public static class OpenApiSetup
{
    public const string DocumentName = "v1";

    private const string BearerScheme = "Bearer";

    /// <summary>
    /// The OpenAPI document written to api/openapi/ZenGear.Api.json at build time — the contract of web/.
    /// </summary>
    public static IServiceCollection AddZenGearOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options => options
            .AddDocumentTransformer(TransformDocument)
            .AddSchemaTransformer(TransformProblemDetailsSchema));

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
