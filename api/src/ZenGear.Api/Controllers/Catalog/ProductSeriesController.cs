using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Api.ExceptionHandling;
using ZenGear.Api.Extensions;
using ZenGear.Services.Catalog.Series;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.Controllers.Catalog;

/// <summary>
/// The product series for the staff (PRD-03), each of exactly one brand; no <c>DELETE</c>: a series is retired, never
/// deleted (405, R-DEL-01).
/// </summary>
[ApiController]
[Route("product-series")]
public sealed class ProductSeriesController(ProductSeriesService series) : ControllerBase
{
    /// <summary>The series by name (Vietnamese order), of every brand unless <c>brandId</c> is given, retired ones included unless <c>isActive</c> is given.</summary>
    /// <response code="400"><c>page</c> below 1, or <c>pageSize</c> outside 1–100.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>product_series.read</c> missing.</response>
    [HttpGet]
    [RequirePermission(PermissionKeys.ProductSeriesRead)]
    [ProducesResponseType<PagedDto<ProductSeriesDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "PAGE_OUT_OF_RANGE", "PAGE_SIZE_OUT_OF_RANGE")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public Task<PagedDto<ProductSeriesDto>> List(
        [FromQuery, Description("The series of this brand only; with isActive=true, the list a product of that brand "
            + "can take (UC-012).")]
        Guid? brandId,
        [FromQuery, Description("true: the series in use, the list a screen that creates data offers (R-DEL-01); "
            + "false: the retired ones; both when left out.")]
        bool? isActive,
        CancellationToken cancellationToken,
        [FromQuery, Description("From 1.")] int page = 1,
        [FromQuery, Description("1 to 100.")] int pageSize = PageRequest.DefaultPageSize) =>
        series.ListAsync(brandId, isActive, new PageRequest(page, pageSize), cancellationToken);

    /// <summary>
    /// A series, retired or not. The <c>ETag</c> header is its version, sent back in <c>If-Match</c> by <c>PUT</c>.
    /// </summary>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>product_series.read</c> missing.</response>
    /// <response code="404">No series with this id.</response>
    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionKeys.ProductSeriesRead)]
    [ProducesResponseType<ProductSeriesDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ProductSeriesDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (await series.GetAsync(id, cancellationToken) is not { } found)
        {
            return NotFound();
        }

        Response.SetETag(found.Version);
        return found;
    }

    /// <summary>
    /// Creates an active series of an active brand; its slug is made from the name and kept when the series is renamed
    /// (UC-012).
    /// </summary>
    /// <response code="400">
    /// A blank name, longer than 100 characters, or without a Latin letter or digit; no brand, or a brand that does not exist.
    /// </response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>product_series.create</c> missing.</response>
    /// <response code="409">
    /// <c>/problems/brand-retired</c> (R-DEL-01): the brand is retired. <c>/problems/product-series-already-exists</c>:
    /// another series of any brand, maybe retired, has this name or one that gives the same slug.
    /// </response>
    [HttpPost]
    [RequirePermission(PermissionKeys.ProductSeriesCreate)]
    [ProducesResponseType<ProductSeriesDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "NAME_REQUIRED", "NAME_TOO_LONG", "NAME_WITHOUT_LETTER_OR_DIGIT",
        "BRAND_ID_REQUIRED", "BRAND_NOT_FOUND")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status409Conflict, "PRODUCT_SERIES_ALREADY_EXISTS", "BRAND_RETIRED")]
    public async Task<ActionResult<ProductSeriesDto>> Create(ProductSeriesCreateRequest request, CancellationToken cancellationToken)
    {
        var created = await series.CreateAsync(request, User.GetUserId(), cancellationToken);
        Response.SetETag(created.Version);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Renames a series, moves it to another brand, retires (<c>isActive</c> false) or restores it (UC-013); the slug
    /// stays. Send the <c>ETag</c> of <c>GET</c> in <c>If-Match</c> (<c>*</c> skips the check).
    /// </summary>
    /// <response code="400">
    /// A blank name, longer than 100 characters, or without a Latin letter or digit; no brand, or a brand that does not
    /// exist; no <c>isActive</c>. Nothing changed.
    /// </response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>product_series.update</c> missing.</response>
    /// <response code="404">No series with this id.</response>
    /// <response code="409">
    /// <c>/problems/brand-retired</c> (R-DEL-01): the new brand, or the brand of a series being restored, is retired.
    /// <c>/problems/product-series-already-exists</c>:
    /// another series of any brand, maybe retired, has this name or one that gives the same slug.
    /// </response>
    /// <response code="412">The series changed since the <c>ETag</c> was read, or <c>If-Match</c> is not a tag of this API.</response>
    /// <response code="428"><c>/problems/if-match-required</c>: no <c>If-Match</c> header.</response>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.ProductSeriesUpdate)]
    [ProducesResponseType<ProductSeriesDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status400BadRequest, "NAME_REQUIRED", "NAME_TOO_LONG", "NAME_WITHOUT_LETTER_OR_DIGIT",
        "BRAND_ID_REQUIRED", "BRAND_NOT_FOUND", "IS_ACTIVE_REQUIRED")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ValidationProblem>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ErrorCodes(StatusCodes.Status409Conflict, "PRODUCT_SERIES_ALREADY_EXISTS", "BRAND_RETIRED")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<ActionResult<ProductSeriesDto>> Update(Guid id, ProductSeriesUpdateRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
    {
        if (!this.TryReadIfMatch(ifMatch, out var version, out var refusal))
        {
            return refusal;
        }

        if (await series.UpdateAsync(id, version, request, User.GetUserId(), cancellationToken) is not { } updated)
        {
            return NotFound();
        }

        Response.SetETag(updated.Version);
        return updated;
    }
}
