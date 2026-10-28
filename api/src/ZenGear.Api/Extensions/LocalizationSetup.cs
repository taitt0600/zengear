using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace ZenGear.Api.Extensions;

/// <summary>
/// The language of the texts the API writes (DEC-117 (3)): resources in <c>Resources/</c>, chosen by
/// <c>Accept-Language</c> among the supported UI cultures; vi-VN is the default and the answer for every other
/// language (Phase 1 supports only vi-VN, DEC-104 (6)). A new language adds its <c>.resx</c> and its culture here.
/// Only the UI culture follows the request: formatting and parsing stay invariant.
/// </summary>
public static class LocalizationSetup
{
    public static readonly CultureInfo DefaultUiCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static IServiceCollection AddZenGearLocalization(this IServiceCollection services) =>
        services.AddLocalization(options => options.ResourcesPath = "Resources");

    /// <summary>
    /// First in the pipeline: the culture is async-local, so one set by a later middleware would never reach the
    /// exception handler and the status code pages, which write their texts after the inner middleware returned.
    /// </summary>
    public static IApplicationBuilder UseZenGearLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture, DefaultUiCulture),
            SupportedCultures = [CultureInfo.InvariantCulture],
            SupportedUICultures = [DefaultUiCulture],
            RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()],
        });
}
