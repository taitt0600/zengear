using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ZenGear.Api.Options;

namespace ZenGear.Api.Extensions;

/// <summary>
/// CORS for the web (DEC-104 (9), docs/API_CONVENTIONS.md, CORS): the origins of <see cref="WebClientCorsOptions"/>
/// may call every route with a Bearer token (no credentials) and read <c>ETag</c> and <c>Location</c>.
/// </summary>
public static class CorsSetup
{
    public static readonly TimeSpan PreflightMaxAge = TimeSpan.FromMinutes(10);

    private const string PolicyName = "WebClient";

    /// <summary>
    /// A missing or empty <c>Cors:AllowedOrigins</c> is valid and allows no origin (so the build-time OpenAPI
    /// generation starts without it); an entry that is not an origin (<see cref="IsValidOrigin"/>) stops the host at
    /// start-up.
    /// </summary>
    public static IServiceCollection AddZenGearCors(this IServiceCollection services)
    {
        services.AddOptions<WebClientCorsOptions>()
            .BindConfiguration(WebClientCorsOptions.SectionName)
            .Validate(o => o.AllowedOrigins.All(IsValidOrigin),
                "Cors:AllowedOrigins accepts only origins such as https://zengear.vn: http or https, lower case, "
                + "without path, trailing slash or default port, never *.")
            .ValidateOnStart();

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<WebClientCorsOptions>>((cors, web) =>
            cors.AddPolicy(PolicyName, policy => policy
                .WithOrigins(web.Value.AllowedOrigins)
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete)
                .WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType, HeaderNames.IfMatch, "Idempotency-Key")
                .WithExposedHeaders(HeaderNames.ETag, HeaderNames.Location)
                .SetPreflightMaxAge(PreflightMaxAge)));

        return services;
    }

    /// <summary>
    /// For every route, before HTTPS redirection and authentication: a preflight is answered here, never redirected
    /// and never refused for its missing token.
    /// </summary>
    public static IApplicationBuilder UseZenGearCors(this IApplicationBuilder app) => app.UseCors(PolicyName);

    /// <summary>
    /// True for an origin exactly as a browser sends it: <c>http</c> or <c>https</c>, host and port in lower case,
    /// without user, path, query, trailing slash or default port. <c>*</c> and wildcard hosts are never origins.
    /// </summary>
    public static bool IsValidOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.UserInfo.Length == 0
        && !origin.Contains('*', StringComparison.Ordinal)
        && string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal);
}
