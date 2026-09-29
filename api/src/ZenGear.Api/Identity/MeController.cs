using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Auth;
using ZenGear.Services.Identity;

namespace ZenGear.Api.Identity;

[ApiController]
[Route("api/v1/me")]
public sealed class MeController(CurrentUserService currentUsers) : ControllerBase
{
    /// <summary>The signed-in user; called by the web right after Firebase sign-in (AUT-01).</summary>
    /// <response code="401">Missing or invalid Firebase ID token.</response>
    /// <response code="403">
    /// <c>/problems/account-disabled</c> (R-SEC-04) or <c>/problems/account-not-registered</c>.
    /// </response>
    [HttpGet]
    [AllowWhilePasswordChangeRequired]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public Task<CurrentUserDto> Get(CancellationToken cancellationToken) =>
        currentUsers.GetAsync(User.GetUserId(), cancellationToken);
}
