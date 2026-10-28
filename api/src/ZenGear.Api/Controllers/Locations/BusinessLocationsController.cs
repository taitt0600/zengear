using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Api.Extensions;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Permissions;
using ZenGear.Services.Locations.BusinessLocations;

namespace ZenGear.Api.Controllers.Locations;

/// <summary>The business location for the Admin (LOC-01); no <c>POST</c> or <c>DELETE</c>: both answer 405 (R-LOC-01).</summary>
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
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "PAGE_OUT_OF_RANGE", "PAGE_SIZE_OUT_OF_RANGE")]
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
    /// Replaces the name, hotline, email, address, weekly hours and holiday schedule (UC-070). Send every field: the
    /// lists replace the stored ones. Send the <c>ETag</c> of <c>GET</c> in <c>If-Match</c> (<c>*</c> skips the check).
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
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "NAME_REQUIRED", "NAME_TOO_LONG", "PHONE_REQUIRED", "PHONE_INVALID",
        "EMAIL_INVALID", "EMAIL_TOO_LONG", "ADDRESS_DETAIL_REQUIRED", "ADDRESS_DETAIL_TOO_LONG",
        "PROVINCE_ID_REQUIRED", "PROVINCE_ID_INVALID", "PROVINCE_NOT_IN_USE", "WARD_ID_REQUIRED", "WARD_ID_INVALID",
        "WARD_NOT_IN_PROVINCE", "WARD_NOT_IN_USE", "REGULAR_HOURS_REQUIRED", "REGULAR_HOURS_EMPTY", "PERIOD_REQUIRED",
        "DAY_REQUIRED", "DAY_OUT_OF_RANGE", "OPEN_TIME_INVALID", "CLOSE_TIME_INVALID", "PERIOD_OPENS_AFTER_CLOSING",
        "PERIODS_PER_DAY_TOO_MANY", "PERIODS_OVERLAP", "SPECIAL_HOURS_TOO_MANY", "DATE_REQUIRED", "CLOSED_REQUIRED",
        "SPECIAL_HOURS_CLOSED_DATE_WITH_HOURS")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<ActionResult<BusinessLocationDto>> Update(Guid id, BusinessLocationUpdateRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
    {
        if (!this.TryReadIfMatch(ifMatch, out var version, out var refusal))
        {
            return refusal;
        }

        if (await locations.UpdateAsync(id, version, request, User.GetUserId(), cancellationToken) is not { } location)
        {
            return NotFound();
        }

        Response.SetETag(location.Version);
        return location;
    }
}
