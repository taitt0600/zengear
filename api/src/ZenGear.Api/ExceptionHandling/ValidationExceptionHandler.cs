using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// Validation failed in a service (FluentValidation) → 400 <see cref="ValidationProblemDetails"/>
/// with <c>errors</c> keyed by camelCase field path, and <c>ruleId</c> when the failures name one business rule
/// (docs/API_CONVENTIONS.md, Errors). The attempted values are never written.
/// </summary>
public sealed partial class ValidationExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException ex)
        {
            return false;
        }

        var errors = ex.Errors
            .GroupBy(e => ToCamelCasePath(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest };
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
    /// The rule every failure names through its error code (<c>.WithErrorCode("R-USR-11")</c>); null when one failure
    /// names none or another. FluentValidation's own codes (<c>NotEmptyValidator</c>) never look like a rule ID.
    /// </summary>
    private static string? SharedRuleId(ValidationException ex)
    {
        var codes = ex.Errors.Select(e => e.ErrorCode).Distinct(StringComparer.Ordinal).ToList();
        return codes is [{ } code] && RuleIdPattern().IsMatch(code) ? code : null;
    }

    [GeneratedRegex(@"^R-[A-Z]+-\d+$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex RuleIdPattern();

    /// <summary><c>Items[0].UnitPrice</c> → <c>items[0].unitPrice</c>.</summary>
    internal static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
