using Microsoft.AspNetCore.Mvc;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// The schema of a 400 and of a 409 that names a field (DEC-117 (1)): Problem Details with <c>errors</c>. It describes the
/// OpenAPI document only; the handlers write <see cref="ProblemDetails"/> with the same <see cref="ProblemError"/> list in
/// its extensions, and the document gives each operation the enum of its codes (<c>OpenApiSetup</c>).
/// </summary>
public sealed class ValidationProblem : ProblemDetails
{
    public IReadOnlyList<ProblemError> Errors { get; init; } = [];
}
