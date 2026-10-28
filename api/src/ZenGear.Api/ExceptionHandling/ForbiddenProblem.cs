using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Extensions;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// 403 Problem Details written outside MVC; the Problem Details service adds its title, detail and <c>traceId</c>.
/// </summary>
internal static class ForbiddenProblem
{
    public static async Task WriteAsync(HttpContext context, IProblemDetailsService problemDetails, string problemType, string? ruleId)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        var problem = new ProblemDetails
        {
            Type = ProblemDetailsSetup.ProblemTypePrefix + problemType,
            Status = StatusCodes.Status403Forbidden,
        };
        if (ruleId is not null)
        {
            problem.Extensions["ruleId"] = ruleId;
        }

        await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
