using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity;
using ZenGear.Services.Identity.Models;
using ZenGear.Services.Identity.Permissions;
using ZenGear.Services.Identity.Validators;

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

    /// <summary>
    /// The staff accounts, ACTIVE and DISABLED, never ANONYMIZED nor customers, with their roles (STF-05, R-USR-13). No
    /// parameter searches by name, email or phone: personal data stays out of the query string; a client filters the
    /// rows it loaded.
    /// </summary>
    /// <response code="400">An invalid <c>status</c>, <c>roleId</c>, <c>sort</c>, <c>page</c> or <c>pageSize</c> (R-USR-13).</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>staff.read</c> missing.</response>
    [HttpGet]
    [RequirePermission(PermissionKeys.StaffRead)]
    [BadRequestRule(StaffListRequestValidator.RuleId)]
    [ProducesResponseType<PagedDto<StaffListItemDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public Task<PagedDto<StaffListItemDto>> List(
        [FromQuery, Description("ACTIVE or DISABLED; both when left out.")] string? status,
        [FromQuery, Description("Only staff holding this role; a role that does not exist gives an empty page.")]
        Guid? roleId,
        [FromQuery, Description("name (default, Vietnamese order), -name, employeeCode, -employeeCode, joinedDate or "
            + "-joinedDate; empty codes and dates come last.")]
        string? sort,
        CancellationToken cancellationToken,
        [FromQuery, Description("From 1.")] int page = 1,
        [FromQuery, Description("1 to 100.")] int pageSize = PageRequest.DefaultPageSize) =>
        staff.ListAsync(new StaffListRequest(status, roleId, sort, page, pageSize), cancellationToken);

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
