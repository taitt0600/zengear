using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using ZenGear.Api.Attributes;
using ZenGear.Api.ExceptionHandling;

namespace ZenGear.Api.Extensions;

public static class ProblemDetailsSetup
{
    public const string ProblemTypePrefix = "/problems/";

    public const string ValidationType = "validation";

    // The codes of model binding (DEC-117 (2)): a value of the wrong type or none where one is needed.
    public const string BodyRequired = "BODY_REQUIRED";
    public const string BodyInvalid = "BODY_INVALID";
    public const string ValueRequired = "VALUE_REQUIRED";
    public const string ValueInvalid = "VALUE_INVALID";

    /// <summary>Every model-binding code, added to the 400 of every operation in the OpenAPI document.</summary>
    public static readonly IReadOnlyList<string> ModelBindingCodes = [BodyRequired, BodyInvalid, ValueRequired, ValueInvalid];

    /// <summary>
    /// Problem Details (RFC 9457) for every error response (DEC-117 (1)): a <c>/problems/…</c> type, its title and detail
    /// in the language of the request (none for a 500), and <c>traceId</c>. The 400 of model binding carries the
    /// <c>ruleId</c> of <see cref="BadRequestRuleAttribute"/>. Handlers run in registration order; an unhandled exception
    /// becomes a 500 without details.
    /// </summary>
    public static IServiceCollection AddZenGearProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Complete);
        services.AddSingleton<ProblemTexts>();

        services.AddExceptionHandler<BusinessRuleExceptionHandler>();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<ConcurrencyExceptionHandler>();
        services.AddExceptionHandler<RecentSignInRequiredExceptionHandler>();

        services.Configure<MvcOptions>(options => WithCodes(options.ModelBindingMessageProvider));
        services.PostConfigure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = ModelStateProblem);

        return services;
    }

    /// <summary>
    /// Runs for every problem the API writes, from the handlers, the controllers and the framework alike: a type the
    /// framework chose (<c>https://tools.ietf.org/…</c>, or none) becomes the <c>/problems/…</c> type of its status, and
    /// the title and detail come from <see cref="ProblemTexts"/> by type, so no other place writes them.
    /// </summary>
    private static void Complete(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        if (problem.Type is not { } type || !type.StartsWith(ProblemTypePrefix, StringComparison.Ordinal))
        {
            type = ProblemTypePrefix + TypeOfStatus(problem.Status);
            problem.Type = type;
        }

        var texts = context.HttpContext.RequestServices.GetRequiredService<ProblemTexts>();
        var name = type[ProblemTypePrefix.Length..];
        problem.Title = texts.Title(name);
        problem.Detail = problem.Status == StatusCodes.Status500InternalServerError ? null : texts.Detail(name);
    }

    private static string TypeOfStatus(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => ValidationType,
        StatusCodes.Status401Unauthorized => "unauthenticated",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not-found",
        StatusCodes.Status405MethodNotAllowed => "method-not-allowed",
        StatusCodes.Status406NotAcceptable => "not-acceptable",
        StatusCodes.Status412PreconditionFailed => "precondition-failed",
        StatusCodes.Status415UnsupportedMediaType => "unsupported-media-type",
        StatusCodes.Status500InternalServerError => "internal-error",
        _ => ProblemTexts.FallbackType,
    };

    /// <summary>
    /// The messages of model binding are codes: <see cref="ModelStateProblem"/> turns each into an item of <c>errors</c>.
    /// A value sent in the wrong place (an email in <c>?roleId=</c>) is never written back.
    /// </summary>
    private static void WithCodes(DefaultModelBindingMessageProvider messages)
    {
        messages.SetAttemptedValueIsInvalidAccessor((_, _) => ValueInvalid);
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => ValueInvalid);
        messages.SetUnknownValueIsInvalidAccessor(_ => ValueInvalid);
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => ValueInvalid);
        messages.SetValueIsInvalidAccessor(_ => ValueInvalid);
        messages.SetValueMustBeANumberAccessor(_ => ValueInvalid);
        messages.SetNonPropertyValueMustBeANumberAccessor(() => ValueInvalid);
        messages.SetValueMustNotBeNullAccessor(_ => ValueRequired);
        messages.SetMissingBindRequiredValueAccessor(_ => ValueRequired);
        messages.SetMissingKeyOrValueAccessor(() => ValueRequired);
        messages.SetMissingRequestBodyRequiredValueAccessor(() => BodyRequired);
    }

    /// <summary>
    /// The 400 of model binding, built through <see cref="ProblemDetailsFactory"/> so <see cref="Complete"/> runs: one
    /// item of <c>errors</c> per refused key, with the <c>ruleId</c> of the action's <see cref="BadRequestRuleAttribute"/>.
    /// </summary>
    private static IActionResult ModelStateProblem(ActionContext context)
    {
        var http = context.HttpContext;
        var problem = http.RequestServices.GetRequiredService<ProblemDetailsFactory>()
            .CreateProblemDetails(http, StatusCodes.Status400BadRequest, type: ProblemTypePrefix + ValidationType);
        problem.Extensions["errors"] = ModelStateErrors(context, http.RequestServices.GetRequiredService<ProblemTexts>());
        if (context.ActionDescriptor.EndpointMetadata.OfType<BadRequestRuleAttribute>().FirstOrDefault() is { } rule)
        {
            problem.Extensions["ruleId"] = rule.RuleId;
        }

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    /// <summary>
    /// A key of the body (<c>$.regularHours[0].day</c>, a property such as <c>name</c>, or the body itself) is a pointer;
    /// any other key is a query or route parameter. A message that is not one of the codes above (a JSON reader error, a
    /// required body) gets the code of its place.
    /// </summary>
    private static List<ProblemError> ModelStateErrors(ActionContext context, ProblemTexts texts)
    {
        var parameters = context.ActionDescriptor.Parameters;
        var body = parameters.FirstOrDefault(p => p.BindingInfo?.BindingSource == BindingSource.Body)?.Name;
        var errors = new List<ProblemError>();
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.ValidationState != ModelValidationState.Invalid)
            {
                continue;
            }

            var inBody = body is not null && (key.Length == 0 || key == body || key.StartsWith('$')
                || parameters.All(p => !string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase)));
            var bodyKey = key == body ? "" : key;
            var pointer = inBody ? ProblemPointers.FromPath(bodyKey) : null;
            foreach (var error in entry.Errors)
            {
                var code = CodeOf(error, key, pointer);
                var item = new ProblemError(pointer, inBody ? null : key, code, texts.ErrorDetail(code));
                if (!errors.Contains(item))
                {
                    errors.Add(item);
                }
            }
        }

        // A body that cannot be read also leaves the body parameter unbound, so its implicit [Required] adds a second
        // error: the body was sent, so only BODY_INVALID stays.
        if (errors.Exists(e => e.Code == BodyInvalid))
        {
            errors.RemoveAll(e => e.Code == BodyRequired);
        }

        return errors;
    }

    /// <summary>
    /// A JSON reader error keeps the path where it stopped as its key (<c>$</c>, <c>$.name</c>), even at the root of the
    /// body: the body was sent but cannot be read. Only a message on the body parameter itself (its implicit
    /// <c>[Required]</c>) means the body is missing.
    /// </summary>
    private static string CodeOf(ModelError error, string key, string? pointer)
    {
        if (error.ErrorMessage is BodyRequired or BodyInvalid or ValueRequired or ValueInvalid)
        {
            return error.ErrorMessage;
        }

        if (pointer is null)
        {
            return ValueInvalid;
        }

        return error.Exception is null && pointer == ProblemPointers.Root && !key.StartsWith('$') ? BodyRequired : BodyInvalid;
    }
}
