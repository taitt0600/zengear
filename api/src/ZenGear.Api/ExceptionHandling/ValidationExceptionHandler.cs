using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ZenGear.Api.Extensions;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// Validation failed in a service (FluentValidation) → 400 <c>/problems/validation</c> with <c>errors</c>, one item per
/// failure: the pointer of the body field, or the query parameter when the action has no body, the code of the reason and
/// its text (DEC-117 (1)); and <c>ruleId</c> when every failure names the same business rule through its state
/// (<c>.WithState(_ => "R-USR-11")</c>, docs/API_CONVENTIONS.md, Errors). The attempted values are never written.
/// </summary>
public sealed partial class ValidationExceptionHandler(IProblemDetailsService problemDetails, ProblemTexts texts) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException ex)
        {
            return false;
        }

        var inBody = HasBody(httpContext);
        var errors = ex.Errors.Select(failure => (Failure: failure, Code: CodeOf(failure)))
            .Select(e => new ProblemError(
                inBody ? ProblemPointers.FromPath(e.Failure.PropertyName) : null,
                inBody ? null : ToCamelCasePath(e.Failure.PropertyName),
                e.Code,
                texts.ErrorDetail(e.Code, e.Failure.FormattedMessagePlaceholderValues)))
            .Distinct()
            .ToList();

        var problem = new ProblemDetails
        {
            Type = ProblemDetailsSetup.ProblemTypePrefix + ProblemDetailsSetup.ValidationType,
            Status = StatusCodes.Status400BadRequest,
        };
        problem.Extensions["errors"] = errors;
        if (SharedRuleId(ex) is { } ruleId)
        {
            problem.Extensions["ruleId"] = ruleId;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>
    /// Whether the failing action reads a body, so the failures are fields of it. The endpoint comes from the exception
    /// feature: the middleware clears the request's endpoint before the handlers run.
    /// </summary>
    private static bool HasBody(HttpContext httpContext) =>
        httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action
        && action.Parameters.Any(p => p.BindingInfo?.BindingSource == BindingSource.Body);

    /// <summary>
    /// The code the validator set (DEC-117 (2)); FluentValidation's own codes (<c>NotEmptyValidator</c>) are not codes of
    /// the API, and a test keeps every rule from leaving one.
    /// </summary>
    private static string CodeOf(ValidationFailure failure) =>
        failure.ErrorCode is { } code && CodePattern().IsMatch(code) ? code : ProblemDetailsSetup.ValueInvalid;

    /// <summary>The rule every failure names through its state; null when one failure names none or another.</summary>
    private static string? SharedRuleId(ValidationException ex)
    {
        var rules = ex.Errors.Select(e => e.CustomState as string).Distinct(StringComparer.Ordinal).ToList();
        return rules is [{ } rule] && RuleIdPattern().IsMatch(rule) ? rule : null;
    }

    [GeneratedRegex(@"^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    internal static partial Regex CodePattern();

    [GeneratedRegex(@"^R-[A-Z]+-\d+$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex RuleIdPattern();

    /// <summary><c>Items[0].UnitPrice</c> → <c>items[0].unitPrice</c>.</summary>
    internal static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
