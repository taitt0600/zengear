using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Permissions;
using ZenGear.Services.Identity.Roles;

namespace ZenGear.Api.Controllers.Identity;

/// <summary>The permissions the Admin grants to roles (AUT-05): defined in code, read only (DEC-095).</summary>
[ApiController]
[Route("permissions")]
public sealed class PermissionController(RoleService roles) : ControllerBase
{
    /// <summary>A page of the permissions, by key, with their labels.</summary>
    /// <response code="400"><c>page</c> below 1, or <c>pageSize</c> outside 1–100.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>role.read</c> missing.</response>
    [HttpGet]
    [RequirePermission(PermissionKeys.RoleRead)]
    [ProducesResponseType<PagedDto<PermissionDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "PAGE_OUT_OF_RANGE", "PAGE_SIZE_OUT_OF_RANGE")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public PagedDto<PermissionDto> List(
        [FromQuery, Description("From 1.")] int page = 1,
        [FromQuery, Description("1 to 100.")] int pageSize = PageRequest.DefaultPageSize) =>
        roles.ListPermissions(new PageRequest(page, pageSize));
}
