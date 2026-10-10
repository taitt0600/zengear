using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Api.Extensions;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Permissions;
using ZenGear.Services.Locations.BusinessLocations;

namespace ZenGear.Api.Controllers.Locations;

/// <summary>
/// The business location for the Admin (LOC-01). Phase 1 has exactly one, created at go-live: there is no
/// <c>POST</c> or <c>DELETE</c>, and both answer 405 (R-LOC-01).
/// </summary>
[ApiController]
[Route("business-locations")]
public sealed class BusinessLocationsController(BusinessLocationService locations) : ControllerBase
{
    /// <summary>The business locations by name, as the Admin edits them; the screen reads the id of the one location here.</summary>
    /// <response code="400"><c>page</c> below 1, or <c>pageSize</c> outside 1–100.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>business_location.update</c> missing.</response>
    [HttpGet]
    [RequirePermission(PermissionKeys.BusinessLocationUpdate)]
    [ProducesResponseType<PagedDto<BusinessLocationDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public Task<PagedDto<BusinessLocationDto>> List(CancellationToken cancellationToken, [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize) =>
        locations.ListAsync(new PageRequest(page, pageSize), cancellationToken);

    /// <summary>
    /// The location as the Admin edits it, with the whole holiday schedule. The <c>ETag</c> header is its version, sent
    /// back in <c>If-Match</c> by <c>PUT</c>.
    /// </summary>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>business_location.update</c> missing.</response>
    /// <response code="404">No business location with this id.</response>
    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionKeys.BusinessLocationUpdate)]
    [ProducesResponseType<BusinessLocationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<BusinessLocationDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (await locations.GetAsync(id, cancellationToken) is not { } location)
        {
            return NotFound();
        }

        Response.SetETag(location.Version);
        return location;
    }

    /// <summary>
    /// Replaces the name, hotline, email, address, weekly hours and holiday schedule (UC-070); the website, receipts and
    /// emails read them from here (R-LOC-01). Send every field: the lists replace the stored ones. Send the <c>ETag</c> of
    /// <c>GET</c> in <c>If-Match</c> (<c>*</c> skips the check).
    /// </summary>
    /// <response code="400">
    /// Invalid input; a ward outside the province, or a ward or province chosen anew that is no longer in use (UC-070 3a);
    /// a closed holiday date with hours (<c>ruleId</c> <c>R-LOC-01</c>). Nothing changed.
    /// </response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>business_location.update</c> missing.</response>
    /// <response code="404">No business location with this id.</response>
    /// <response code="412">The location changed since the <c>ETag</c> was read, or <c>If-Match</c> is not a tag of this API.</response>
    /// <response code="428"><c>/problems/if-match-required</c>: no <c>If-Match</c> header.</response>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.BusinessLocationUpdate)]
    [ProducesResponseType<BusinessLocationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<ActionResult<BusinessLocationDto>> Update(Guid id, BusinessLocationUpdateRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Problem(statusCode: StatusCodes.Status428PreconditionRequired, type: "/problems/if-match-required",
                title: "If-Match required", detail: "Send the ETag of GET /api/v1/admin/business-locations/{id} in the If-Match header.");
        }

        if (!ETagExtensions.TryParseIfMatch(ifMatch, out var version))
        {
            return Problem(statusCode: StatusCodes.Status412PreconditionFailed);
        }

        if (await locations.UpdateAsync(id, version, request, User.GetUserId(), cancellationToken) is not { } location)
        {
            return NotFound();
        }

        Response.SetETag(location.Version);
        return location;
    }
}
