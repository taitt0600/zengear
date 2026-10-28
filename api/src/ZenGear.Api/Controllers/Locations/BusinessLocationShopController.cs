using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Locations.BusinessLocations;

namespace ZenGear.Api.Controllers.Locations;

/// <summary>The business location for the storefront (R-LOC-01).</summary>
[ApiController]
[ShopEndpoint]
[Route("business-locations")]
public sealed class BusinessLocationShopController(BusinessLocationShopService locations) : ControllerBase
{
    /// <summary>The business locations by name, as the website shows them (R-LOC-01).</summary>
    /// <response code="400"><c>page</c> below 1, or <c>pageSize</c> outside 1–100.</response>
    [HttpGet]
    [ProducesResponseType<PagedDto<BusinessLocationShopDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "PAGE_OUT_OF_RANGE", "PAGE_SIZE_OUT_OF_RANGE")]
    public Task<PagedDto<BusinessLocationShopDto>> List(CancellationToken cancellationToken, [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageRequest.DefaultPageSize) =>
        locations.ListAsync(new PageRequest(page, pageSize), cancellationToken);
}
