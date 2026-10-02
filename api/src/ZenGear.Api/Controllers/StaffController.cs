using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Services.Identity;
using ZenGear.Services.Identity.Models;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.Controllers;

[ApiController]
[Route("api/v1/staff")]
public sealed class StaffController(StaffService staff) : ControllerBase
{
    /// <summary>
    /// Creates a staff account with the default password and one or more roles (STF-01). The staff member must change
    /// the password before using anything else (R-USR-03).
    /// </summary>
    /// <response code="400">Invalid input, or a role that does not exist.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>staff.create</c> missing.</response>
    /// <response code="409">
    /// <c>/problems/role-required</c> (R-SEC-01), <c>/problems/role-not-for-staff</c> (R-USR-01),
    /// <c>/problems/admin-role-not-assignable</c> (R-TX-11), <c>/problems/email-already-registered</c>,
    /// <c>/problems/employee-code-taken</c>, <c>/problems/auth-account-exists</c>.
    /// </response>
    [HttpPost]
    [RequirePermission(PermissionKeys.StaffCreate)]
    [ProducesResponseType<StaffDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<StaffDto>> Create(StaffCreateRequest request, CancellationToken cancellationToken)
    {
        var created = await staff.CreateAsync(request, User.GetUserId(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>A staff account and its roles.</summary>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>staff.read</c> missing.</response>
    /// <response code="404">No staff account with this id.</response>
    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionKeys.StaffRead)]
    [ProducesResponseType<StaffDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<StaffDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await staff.GetAsync(id, cancellationToken) is { } found ? found : NotFound();
}
