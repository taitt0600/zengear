namespace ZenGear.Api.Options;

/// <summary>Configuration section <c>Cors</c>: the origins of the web allowed to call the API (DEC-104 (9)).</summary>
public sealed class WebClientCorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Origins such as <c>http://localhost:3000</c>: lower case, without path, trailing slash or default port; never
    /// <c>*</c>. Empty (the default, and every environment until the web deploys) allows no cross-origin call.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
