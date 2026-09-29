using Microsoft.AspNetCore.Mvc;

namespace ZenGear.Api.Errors;

/// <summary>403 Problem Details written outside MVC, with <c>traceId</c> added by the Problem Details service.</summary>
internal static class ForbiddenProblem
{
    public static async Task WriteAsync(HttpContext context, IProblemDetailsService problemDetails,
        string problemType, string title, string detail, string? ruleId)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        var problem = new ProblemDetails
        {
            Type = $"/problems/{problemType}",
            Title = title,
            Status = StatusCodes.Status403Forbidden,
            Detail = detail,
        };
        if (ruleId is not null)
        {
            problem.Extensions["ruleId"] = ruleId;
        }

        await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
