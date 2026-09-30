using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZenGear.Services.Common;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>Business rule or state conflict → 409 with <c>ruleId</c> (docs/API_CONVENTIONS.md, Errors).</summary>
public sealed class BusinessRuleExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
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
            Type = $"/problems/{ex.ProblemType}",
            Title = ex.Title,
            Status = StatusCodes.Status409Conflict,
            Detail = ex.Detail,
        };
        if (ex.RuleId is not null)
        {
            problem.Extensions["ruleId"] = ex.RuleId;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
