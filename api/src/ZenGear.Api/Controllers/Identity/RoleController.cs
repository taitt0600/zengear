using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Api.Extensions;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Permissions;
using ZenGear.Services.Identity.Roles;

namespace ZenGear.Api.Controllers.Identity;

/// <summary>
/// The roles, their permissions and manageable sets, for the Admin (AUT-05). The <c>role.*</c> permissions are held by the
/// Admin only: the API never grants them to another role.
/// </summary>
[ApiController]
[Route("roles")]
public sealed class RoleController(RoleService roles) : ControllerBase
{
    /// <summary>A page of the roles by name (Vietnamese order), with whether each is a system or a default role.</summary>
    /// <response code="400"><c>page</c> below 1, or <c>pageSize</c> outside 1–100.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>role.read</c> missing.</response>
    [HttpGet]
    [RequirePermission(PermissionKeys.RoleRead)]
    [ProducesResponseType<PagedDto<RoleListItemDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "PAGE_OUT_OF_RANGE", "PAGE_SIZE_OUT_OF_RANGE")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public Task<PagedDto<RoleListItemDto>> List(CancellationToken cancellationToken,
        [FromQuery, Description("From 1.")] int page = 1,
        [FromQuery, Description("1 to 100.")] int pageSize = PageRequest.DefaultPageSize) =>
        roles.ListAsync(new PageRequest(page, pageSize), cancellationToken);

    /// <summary>
    /// A role with its permission keys and manageable roles. The <c>ETag</c> header is its version, sent back in
    /// <c>If-Match</c> by <c>PUT</c>.
    /// </summary>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>role.read</c> missing.</response>
    /// <response code="404">No role with this id.</response>
    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionKeys.RoleRead)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RoleDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (await roles.GetAsync(id, cancellationToken) is not { } role)
        {
            return NotFound();
        }

        Response.SetETag(role.Version);
        return role;
    }

    /// <summary>
    /// Creates a staff role with its permissions and discount (UC-002); it manages no role until the Admin grants it
    /// (R-SEC-12).
    /// </summary>
    /// <response code="400">
    /// Invalid input, a permission key that does not exist, or a <c>role.*</c> key (<c>PERMISSION_NOT_GRANTABLE</c>).
    /// </response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>role.create</c> missing.</response>
    /// <response code="409"><c>/problems/role-already-exists</c>: another role has this name, whatever the case.</response>
    [HttpPost]
    [RequirePermission(PermissionKeys.RoleCreate)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "NAME_REQUIRED", "NAME_TOO_LONG", "PERMISSION_KEYS_REQUIRED",
        "PERMISSION_NOT_FOUND", "PERMISSION_NOT_GRANTABLE", "MAX_DISCOUNT_PERCENT_OUT_OF_RANGE",
        "MAX_DISCOUNT_PERCENT_TOO_PRECISE")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status409Conflict, "ROLE_ALREADY_EXISTS")]
    public async Task<ActionResult<RoleDto>> Create(RoleCreateRequest request, CancellationToken cancellationToken)
    {
        var created = await roles.CreateAsync(request, User.GetUserId(), cancellationToken);
        Response.SetETag(created.Version);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Sets the name, permissions, discount and manageable roles of a role (UC-003). A system role (Admin, Customer)
    /// changes only its discount; a default role keeps its name. Send the <c>ETag</c> of <c>GET</c> in <c>If-Match</c>
    /// (<c>*</c> skips the check).
    /// </summary>
    /// <response code="400">
    /// Invalid input, a permission key or role that does not exist, or a <c>role.*</c> key for a role that is not a system
    /// role (<c>PERMISSION_NOT_GRANTABLE</c>). Nothing changed.
    /// </response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>role.update</c> missing.</response>
    /// <response code="404">No role with this id.</response>
    /// <response code="409">
    /// <c>/problems/system-role-not-editable</c> (R-SEC-03), <c>/problems/default-role-not-renamable</c>,
    /// <c>/problems/role-already-exists</c>, <c>/problems/role-not-manageable</c> (R-SEC-12: Admin or Customer in
    /// <c>managedRoleIds</c>).
    /// </response>
    /// <response code="412">The role changed since the <c>ETag</c> was read, or <c>If-Match</c> is not a tag of this API.</response>
    /// <response code="428"><c>/problems/if-match-required</c>: no <c>If-Match</c> header.</response>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.RoleUpdate)]
    [ProducesResponseType<RoleDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "NAME_REQUIRED", "NAME_TOO_LONG", "PERMISSION_KEYS_REQUIRED",
        "PERMISSION_NOT_FOUND", "PERMISSION_NOT_GRANTABLE", "MAX_DISCOUNT_PERCENT_OUT_OF_RANGE",
        "MAX_DISCOUNT_PERCENT_TOO_PRECISE", "MANAGED_ROLE_IDS_REQUIRED", "ROLE_NOT_FOUND")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status409Conflict, "ROLE_ALREADY_EXISTS", "DEFAULT_ROLE_NOT_RENAMABLE", "ROLE_NOT_MANAGEABLE")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<ActionResult<RoleDto>> Update(Guid id, RoleUpdateRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
    {
        if (!this.TryReadIfMatch(ifMatch, out var version, out var refusal))
        {
            return refusal;
        }

        if (await roles.UpdateAsync(id, version, request, User.GetUserId(), cancellationToken) is not { } role)
        {
            return NotFound();
        }

        Response.SetETag(role.Version);
        return role;
    }

    /// <summary>Deletes a role no account holds, with its grants and manageable pairs (UC-003).</summary>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>role.delete</c> missing.</response>
    /// <response code="404">No role with this id.</response>
    /// <response code="409">
    /// <c>/problems/role-not-deletable</c> (a system or default role), <c>/problems/role-in-use</c> (an account, whatever
    /// its status, holds the role).
    /// </response>
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionKeys.RoleDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await roles.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
