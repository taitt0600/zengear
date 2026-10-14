using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using ZenGear.Api.Attributes;
using ZenGear.Api.Constants;
using ZenGear.Api.Extensions;
using ZenGear.Services.Catalog.Brands;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Identity.Permissions;

namespace ZenGear.Api.Controllers.Catalog;

/// <summary>The brands for the staff (PRD-02); no <c>DELETE</c>: a brand is retired, never deleted (405, R-DEL-01).</summary>
[ApiController]
[Route("brands")]
public sealed class BrandController(BrandService brands) : ControllerBase
{
    /// <summary>The brands by name (Vietnamese order), retired ones included unless <c>isActive</c> is given.</summary>
    /// <response code="400"><c>page</c> below 1, or <c>pageSize</c> outside 1–100.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>brand.read</c> missing.</response>
    [HttpGet]
    [RequirePermission(PermissionKeys.BrandRead)]
    [ProducesResponseType<PagedDto<BrandDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public Task<PagedDto<BrandDto>> List(
        [FromQuery, Description("true: the brands in use, the list a screen that creates data offers (R-DEL-01); "
            + "false: the retired ones; both when left out.")]
        bool? isActive,
        CancellationToken cancellationToken,
        [FromQuery, Description("From 1.")] int page = 1,
        [FromQuery, Description("1 to 100.")] int pageSize = PageRequest.DefaultPageSize) =>
        brands.ListAsync(isActive, new PageRequest(page, pageSize), cancellationToken);

    /// <summary>
    /// A brand, retired or not. The <c>ETag</c> header is its version, sent back in <c>If-Match</c> by <c>PUT</c>.
    /// </summary>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>brand.read</c> missing.</response>
    /// <response code="404">No brand with this id.</response>
    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionKeys.BrandRead)]
    [ProducesResponseType<BrandDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<BrandDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (await brands.GetAsync(id, cancellationToken) is not { } brand)
        {
            return NotFound();
        }

        Response.SetETag(brand.Version);
        return brand;
    }

    /// <summary>Creates an active brand; its slug is made from the name and kept when the brand is renamed (UC-010).</summary>
    /// <response code="400">A blank name, longer than 100 characters, or without a Latin letter or digit.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>brand.create</c> missing.</response>
    /// <response code="409">
    /// <c>/problems/brand-already-exists</c>: another brand, maybe retired, has this name or one that gives the same slug.
    /// </response>
    [HttpPost]
    [RequirePermission(PermissionKeys.BrandCreate)]
    [ProducesResponseType<BrandDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<BrandDto>> Create(BrandCreateRequest request, CancellationToken cancellationToken)
    {
        var created = await brands.CreateAsync(request, User.GetUserId(), cancellationToken);
        Response.SetETag(created.Version);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Renames, retires (<c>isActive</c> false) or restores a brand (UC-011); the slug stays. Send the <c>ETag</c> of
    /// <c>GET</c> in <c>If-Match</c> (<c>*</c> skips the check).
    /// </summary>
    /// <response code="400">A blank name, longer than 100 characters, or without a Latin letter or digit. Nothing changed.</response>
    /// <response code="403"><c>/problems/permission-denied</c>: <c>brand.update</c> missing.</response>
    /// <response code="404">No brand with this id.</response>
    /// <response code="409">
    /// <c>/problems/brand-already-exists</c>: another brand, maybe retired, has this name or one that gives the same slug.
    /// </response>
    /// <response code="412">The brand changed since the <c>ETag</c> was read, or <c>If-Match</c> is not a tag of this API.</response>
    /// <response code="428"><c>/problems/if-match-required</c>: no <c>If-Match</c> header.</response>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.BrandUpdate)]
    [ProducesResponseType<BrandDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, "application/problem+json")]
    public async Task<ActionResult<BrandDto>> Update(Guid id, BrandUpdateRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
    {
        if (!this.TryReadIfMatch(ifMatch, "/api/v1/admin/brands/{id}", out var version, out var refusal))
        {
            return refusal;
        }

        if (await brands.UpdateAsync(id, version, request, User.GetUserId(), cancellationToken) is not { } brand)
        {
            return NotFound();
        }

        Response.SetETag(brand.Version);
        return brand;
    }
}
