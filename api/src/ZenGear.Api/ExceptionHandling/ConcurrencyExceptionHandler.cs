using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// The row changed since it was read (<c>xmin</c>): 412 when the request sent <c>If-Match</c>,
/// otherwise 409 <c>/problems/concurrency-conflict</c> without <c>ruleId</c>
/// (docs/API_CONVENTIONS.md, Concurrency and idempotency).
/// </summary>
public sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        var problem = httpContext.Request.Headers.ContainsKey(HeaderNames.IfMatch)
            ? new ProblemDetails { Status = StatusCodes.Status412PreconditionFailed }
            : new ProblemDetails
            {
                Type = "/problems/concurrency-conflict",
                Title = "Concurrency conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = "The resource was changed by another request. Reload it and try again.",
            };

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
