using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Extensions;
using ZenGear.Services.Common;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// Business rule or state conflict → 409 with <c>ruleId</c> (docs/API_CONVENTIONS.md, Errors). A conflict about one
/// field of the request also gets <c>errors</c> with that field's pointer and the type as its code
/// (<c>/problems/email-already-registered</c> → <c>EMAIL_ALREADY_REGISTERED</c>, DEC-117 (1)), so the web shows it under
/// the field. Title and detail come from <c>ProblemDetailsSetup</c>.
/// </summary>
public sealed class BusinessRuleExceptionHandler(IProblemDetailsService problemDetails, ProblemTexts texts) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BusinessRuleException ex)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        var problem = new ProblemDetails
        {
            Type = ProblemDetailsSetup.ProblemTypePrefix + ex.ProblemType,
            Status = StatusCodes.Status409Conflict,
        };
        if (ex.RuleId is not null)
        {
            problem.Extensions["ruleId"] = ex.RuleId;
        }

        if (ex.Field is not null)
        {
            problem.Extensions["errors"] = new[]
            {
                new ProblemError(ProblemPointers.FromPath(ex.Field), Parameter: null, CodeOf(ex.ProblemType), texts.Detail(ex.ProblemType)),
            };
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary><c>email-already-registered</c> → <c>EMAIL_ALREADY_REGISTERED</c>.</summary>
    public static string CodeOf(string problemType) => problemType.Replace('-', '_').ToUpperInvariant();
}
