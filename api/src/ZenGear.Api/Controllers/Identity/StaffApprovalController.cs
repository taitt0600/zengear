using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Api.Extensions;
using ZenGear.Services.Identity.Permissions;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Api.Controllers.Identity;

/// <summary>
/// Approves, rejects and withdraws PENDING staff accounts (STF-09, UC-140). Each command requires <c>If-Match</c> with the
/// <c>ETag</c> of <c>GET /staff/{id}</c> or the <c>version</c> of the list item, the deliberate exception of DEC-140 (2) to
/// the rule that POST commands take no <c>If-Match</c> (docs/API_CONVENTIONS.md): missing gives 428, another version 412,
/// and <c>If-Match: *</c> is refused with 412 too, since it names no version and the approver must act on the data they saw.
/// </summary>
[ApiController]
[Route("staff")]
public sealed class StaffApprovalController(StaffApprovalService approvals) : ControllerBase
{
    /// <summary>
    /// Approves a PENDING account: ACTIVE, with a sign-in account on the default password that must be changed at the
    /// first sign-in (R-USR-17, R-USR-03).
    /// </summary>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>staff.approve</c> missing.</response>
    /// <response code="404">No staff account with this id.</response>
    /// <response code="409">
    /// <c>/problems/account-not-pending</c> (R-USR-17), <c>/problems/own-account-request-not-approvable</c> (R-USR-17: the
    /// caller created it), <c>/problems/account-out-of-scope</c> (R-SEC-12: a role of the account outside the caller's
    /// manageable set), <c>/problems/auth-account-exists</c> (another sign-in account holds its email; it stays PENDING).
    /// </response>
    /// <response code="412">The account changed since its version was read, or <c>If-Match</c> names no version of it (<c>*</c> included).</response>
    /// <response code="428"><c>/problems/if-match-required</c>: no <c>If-Match</c> header.</response>
    [HttpPost("{id:guid}/approve")]
    [RequirePermission(PermissionKeys.StaffApprove)]
    [ProducesResponseType<StaffDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<StaffDto>> Approve(Guid id, [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!TryReadVersion(ifMatch, out var version, out var refusal))
        {
            return refusal;
        }

        if (await approvals.ApproveAsync(id, version, User.GetUserId(), cancellationToken) is not { } approved)
        {
            return NotFound();
        }

        Response.SetETag(approved.Version);
        return approved;
    }

    /// <summary>Rejects a PENDING account: it is deleted with its profile and roles (R-USR-17).</summary>
    /// <response code="204">Rejected; the account no longer exists.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>staff.approve</c> missing.</response>
    /// <response code="404">No staff account with this id.</response>
    /// <response code="409">
    /// <c>/problems/account-not-pending</c> (R-USR-17), <c>/problems/own-account-request-not-approvable</c> (R-USR-17),
    /// <c>/problems/account-out-of-scope</c> (R-SEC-12).
    /// </response>
    /// <response code="412">The account changed since its version was read, or <c>If-Match</c> names no version of it (<c>*</c> included).</response>
    /// <response code="428"><c>/problems/if-match-required</c>: no <c>If-Match</c> header.</response>
    [HttpPost("{id:guid}/reject")]
    [RequirePermission(PermissionKeys.StaffApprove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<ActionResult> Reject(Guid id, [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!TryReadVersion(ifMatch, out var version, out var refusal))
        {
            return refusal;
        }

        return await approvals.RejectAsync(id, version, User.GetUserId(), cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>
    /// The creator withdraws their own PENDING account, which is deleted as a rejection does, to enter it again with the
    /// right data (DEC-140 (1)).
    /// </summary>
    /// <response code="204">Withdrawn; the account no longer exists.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>staff.create</c> missing.</response>
    /// <response code="404">No staff account with this id.</response>
    /// <response code="409">
    /// <c>/problems/account-not-pending</c> (R-USR-17), <c>/problems/not-account-creator</c> (R-USR-17: the caller did not
    /// create it).
    /// </response>
    /// <response code="412">The account changed since its version was read, or <c>If-Match</c> names no version of it (<c>*</c> included).</response>
    /// <response code="428"><c>/problems/if-match-required</c>: no <c>If-Match</c> header.</response>
    [HttpPost("{id:guid}/withdraw")]
    [RequirePermission(PermissionKeys.StaffCreate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<ActionResult> Withdraw(Guid id, [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (!TryReadVersion(ifMatch, out var version, out var refusal))
        {
            return refusal;
        }

        return await approvals.WithdrawAsync(id, version, User.GetUserId(), cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>
    /// The version <c>If-Match</c> names: missing gives 428, a tag that can never match 412, and <c>*</c> 412 as well,
    /// since these commands act only on the version the caller saw (DEC-140 (2)).
    /// </summary>
    private bool TryReadVersion(string? ifMatch, out uint version, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out ActionResult? refusal)
    {
        version = 0;
        if (!this.TryReadIfMatch(ifMatch, out var parsed, out refusal))
        {
            return false;
        }

        if (parsed is not { } named)
        {
            refusal = Problem(statusCode: StatusCodes.Status412PreconditionFailed, type: ETagExtensions.PreconditionFailedType);
            return false;
        }

        version = named;
        return true;
    }
}
