using System.Diagnostics;
using ZenGear.Api.ExceptionHandling;

namespace ZenGear.Api.Extensions;

public static class ProblemDetailsSetup
{
    /// <summary>
    /// Problem Details (RFC 9457) for every error response, each with <c>traceId</c>.
    /// Handlers run in registration order; an unhandled exception becomes a 500 without details.
    /// </summary>
    public static IServiceCollection AddZenGearProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        services.AddExceptionHandler<BusinessRuleExceptionHandler>();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<ConcurrencyExceptionHandler>();

        return services;
    }
}
