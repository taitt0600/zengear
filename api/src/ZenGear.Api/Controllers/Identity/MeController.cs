using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Api.Extensions;
using ZenGear.Services.Identity.Account;

namespace ZenGear.Api.Controllers.Identity;

[ApiController]
[Route("me")]
public sealed class MeController(CurrentUserService currentUsers) : ControllerBase
{
    /// <summary>
    /// The signed-in user; called by the web right after Firebase sign-in (AUT-01). The <c>ETag</c> header is the
    /// version of the account (<c>users</c>), sent back in <c>If-Match</c> by <c>PATCH /api/v1/admin/me</c>.
    /// </summary>
    /// <response code="401">Missing or invalid Firebase ID token.</response>
    /// <response code="403">
    /// <c>/problems/account-disabled</c> (R-SEC-04) or <c>/problems/account-not-registered</c>.
    /// </response>
    [HttpGet]
    [AnyRegisteredUser]
    [AllowWhilePasswordChangeRequired]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<CurrentUserDto> Get(CancellationToken cancellationToken)
    {
        var user = await currentUsers.GetAsync(User.GetUserId(), cancellationToken);
        Response.SetETag(user.Version);
        return user;
    }

    /// <summary>
    /// Changes the display name of the signed-in user (STF-03, R-USR-04): the only field a user edits here; any other
    /// property of the body, such as <c>email</c>, is ignored. Send the <c>ETag</c> of <c>GET /api/v1/admin/me</c> in
    /// <c>If-Match</c> (<c>*</c> skips the check). The tag covers the account row only, not
    /// <c>mustChangePassword</c>; a new sign-in of the same account (recorded in <c>last_login_at</c>, R-SEC-11)
    /// changes it too, so on 412 read the account again and retry.
    /// </summary>
    /// <response code="400">Blank name, or longer than 100 characters.</response>
    /// <response code="403"><c>/problems/password-change-required</c> (R-USR-03), or as for <c>GET</c>.</response>
    /// <response code="412">The account changed since the <c>ETag</c> was read, or <c>If-Match</c> is not a tag of this API.</response>
    /// <response code="428"><c>/problems/if-match-required</c>: no <c>If-Match</c> header.</response>
    [HttpPatch]
    [AnyRegisteredUser]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<ActionResult<CurrentUserDto>> Update(CurrentUserUpdateRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Problem(statusCode: StatusCodes.Status428PreconditionRequired, type: "/problems/if-match-required",
                title: "If-Match required", detail: "Send the ETag of GET /api/v1/admin/me in the If-Match header.");
        }

        if (!ETagExtensions.TryParseIfMatch(ifMatch, out var version))
        {
            return Problem(statusCode: StatusCodes.Status412PreconditionFailed);
        }

        var user = await currentUsers.UpdateAsync(User.GetUserId(), version, request, cancellationToken);
        Response.SetETag(user.Version);
        return user;
    }

    /// <summary>
    /// Changes the password of the signed-in staff member: the forced change while <c>mustChangePassword</c> is true
    /// (R-USR-03) and the self change (STF-03), through the Firebase Admin SDK (DEC-094). The sign-in must be at most 5
    /// minutes old. On success <c>mustChangePassword</c> becomes false and every refresh token of the account is
    /// revoked, this device's included: the web signs in again with the new password. ID tokens already issued stay
    /// valid until they expire. The password is never logged.
    /// </summary>
    /// <response code="204">Password changed.</response>
    /// <response code="400">The new password breaks R-USR-11 (<c>ruleId</c> <c>R-USR-11</c>, errors on <c>newPassword</c>); nothing changed.</response>
    /// <response code="403"><c>/problems/recent-login-required</c> (R-USR-04): sign in again, then retry; or as for <c>GET</c>.</response>
    /// <response code="409"><c>/problems/staff-account-required</c>: a customer account.</response>
    [HttpPost("change-password")]
    [AnyRegisteredUser]
    [AllowWhilePasswordChangeRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> ChangePassword(CurrentUserPasswordChangeRequest request, CancellationToken cancellationToken)
    {
        await currentUsers.ChangePasswordAsync(User.GetUserId(), User.GetAuthTime(), request, cancellationToken);
        return NoContent();
    }
}
