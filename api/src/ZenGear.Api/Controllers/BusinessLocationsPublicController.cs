using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Inventory;
using ZenGear.Services.Inventory.Models;

namespace ZenGear.Api.Controllers;

/// <summary>The business location for the storefront (R-LOC-01), on the route of <see cref="BusinessLocationsController"/>.</summary>
[ApiController]
[Route("api/v1/business-locations")]
public sealed class BusinessLocationsPublicController(BusinessLocationPublicService locations) : ControllerBase
{
    /// <summary>
    /// The business locations by name, as the website shows them: address, hotline, weekly hours, the holiday schedule
    /// from today, and the hours of the 7 days from today with the holiday schedule applied (R-LOC-01). Phase 1 has
    /// exactly one location. Public.
    /// </summary>
    /// <response code="400"><c>page</c> below 1, or <c>pageSize</c> outside 1–100.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedDto<BusinessLocationPublicDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public Task<PagedDto<BusinessLocationPublicDto>> List(CancellationToken cancellationToken, [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize) =>
        locations.ListAsync(new PageRequest(page, pageSize), cancellationToken);
}
