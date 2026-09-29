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
        services.AddOpenApi(DocumentName, options => options.AddDocumentTransformer(TransformDocument));

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
