using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Services.Identity.Permissions;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Api.Controllers.Identity;

/// <summary>Disables and restores staff accounts (STF-02).</summary>
[ApiController]
[Route("staff")]
public sealed class StaffStatusController(StaffStatusService staffStatus) : ControllerBase
{
    /// <summary>
    /// Disables a staff account: records <c>leftDate</c> in the same change when given (R-USR-07) and revokes its
    /// Firebase sessions (R-SEC-04). Repeating it is safe: it records a new left date and revokes again. The body may be
    /// left out to disable without a left date.
    /// </summary>
    /// <response code="400">Invalid input.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>staff.disable</c> missing.</response>
    /// <response code="404">No staff account with this id.</response>
    /// <response code="409">
    /// <c>/problems/account-anonymized</c> (R-USR-03), <c>/problems/own-account-not-lockable</c> (R-USR-12),
    /// <c>/problems/admin-account-not-lockable</c> (R-TX-11), <c>/problems/last-active-admin</c> (R-USR-12),
    /// <c>/problems/left-date-before-joined-date</c> (R-USR-07), <c>/problems/left-date-in-future</c> (R-USR-07),
    /// <c>/problems/concurrency-conflict</c>.
    /// </response>
    /// <response code="500">The account is disabled but its sessions were not revoked: repeat the request.</response>
    [HttpPost("{id:guid}/disable")]
    [RequirePermission(PermissionKeys.StaffDisable)]
    [ProducesResponseType<StaffDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<StaffDto>> Disable(Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] StaffDisableRequest? request,
        CancellationToken cancellationToken) =>
        await staffStatus.DisableAsync(id, request ?? new StaffDisableRequest(null), User.GetUserId(), cancellationToken)
            is { } disabled ? disabled : NotFound();

    /// <summary>Restores a DISABLED staff account to ACTIVE and clears its left date (R-USR-03).</summary>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>staff.disable</c> missing.</response>
    /// <response code="404">No staff account with this id.</response>
    /// <response code="409">
    /// <c>/problems/account-anonymized</c> (R-USR-03), <c>/problems/admin-account-not-restorable</c> (R-USR-12),
    /// <c>/problems/concurrency-conflict</c>.
    /// </response>
    [HttpPost("{id:guid}/restore")]
    [RequirePermission(PermissionKeys.StaffDisable)]
    [ProducesResponseType<StaffDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<StaffDto>> Restore(Guid id, CancellationToken cancellationToken) =>
        await staffStatus.RestoreAsync(id, User.GetUserId(), cancellationToken) is { } restored ? restored : NotFound();
}
