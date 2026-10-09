using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using ZenGear.Api.Attributes;
using ZenGear.Api.ExceptionHandling;

namespace ZenGear.Api.Extensions;

public static class ProblemDetailsSetup
{
    /// <summary>
    /// Problem Details (RFC 9457) for every error response, each with <c>traceId</c>. The 400 of model binding repeats
    /// no value sent and carries the <c>ruleId</c> of <see cref="BadRequestRuleAttribute"/>.
    /// Handlers run in registration order; an unhandled exception becomes a 500 without details.
    /// </summary>
    public static IServiceCollection AddZenGearProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        services.AddExceptionHandler<BusinessRuleExceptionHandler>();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<ConcurrencyExceptionHandler>();
        services.AddExceptionHandler<RecentSignInRequiredExceptionHandler>();

        services.Configure<MvcOptions>(options => WithoutAttemptedValues(options.ModelBindingMessageProvider));
        services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            var problem = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context => WithBadRequestRule(problem(context), context);
        });

        return services;
    }

    /// <summary>
    /// The messages of model binding name the field only, like <see cref="ValidationExceptionHandler"/>: a value sent
    /// in the wrong place (an email in <c>?roleId=</c>) is never written back.
    /// </summary>
    private static void WithoutAttemptedValues(DefaultModelBindingMessageProvider messages)
    {
        messages.SetAttemptedValueIsInvalidAccessor((_, field) => $"The value is not valid for {field}.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => "The value is not valid.");
        messages.SetValueIsInvalidAccessor(_ => "The value is not valid.");
        messages.SetValueMustNotBeNullAccessor(_ => "The value must not be null.");
    }

    /// <summary>The <c>ruleId</c> of the action's <see cref="BadRequestRuleAttribute"/> on its model-binding 400.</summary>
    private static IActionResult WithBadRequestRule(IActionResult result, ActionContext context)
    {
        if (result is ObjectResult { Value: ProblemDetails problem }
            && !problem.Extensions.ContainsKey("ruleId")
            && context.ActionDescriptor.EndpointMetadata.OfType<BadRequestRuleAttribute>().FirstOrDefault() is { } rule)
        {
            problem.Extensions["ruleId"] = rule.RuleId;
        }

        return result;
    }
}
