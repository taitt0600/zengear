using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZenGear.Data;
using ZenGear.Data.Entities.Catalog;
using ZenGear.Services.Catalog.Brands;
using ZenGear.Services.Common;
using ZenGear.Services.Common.Models;

namespace ZenGear.Services.Catalog.Series;

/// <summary>Product series, for the staff screens (PRD-03): each belongs to exactly one brand; none is ever deleted (R-DEL-01).</summary>
public sealed class ProductSeriesService(
    ZenGearDbContext db,
    IValidator<ProductSeriesCreateRequest> createValidator,
    IValidator<ProductSeriesUpdateRequest> updateValidator,
    IValidator<PageRequest> pageValidator,
    IClock clock)
{
    /// <summary>
    /// One page of the series by name. <paramref name="brandId"/> with <paramref name="isActive"/> true gives the series a
    /// product of that brand can take (UC-012 step 4, R-DEL-01).
    /// </summary>
    public async Task<PagedDto<ProductSeriesDto>> ListAsync(Guid? brandId, bool? isActive, PageRequest page,
        CancellationToken cancellationToken)
    {
        await pageValidator.ValidateAndThrowAsync(page, cancellationToken);

        var series = db.ProductSeries.AsNoTracking();
        if (brandId is { } brand)
        {
            series = series.Where(s => s.BrandId == brand);
        }

        if (isActive is { } active)
        {
            series = series.Where(s => s.IsActive == active);
        }

        // Names sort in Vietnamese order through the database collation; the id keeps pages stable.
        return await PagedQuery.ToPagedDtoAsync(ToDto(series.OrderBy(s => s.Name).ThenBy(s => s.Id)), page, cancellationToken);
    }

    /// <summary>The series <paramref name="id"/>, retired or not; null when there is none.</summary>
    public Task<ProductSeriesDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToDto(db.ProductSeries.AsNoTracking().Where(s => s.Id == id)).SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Creates an active series of an active brand, its slug made from the name (UC-012). Checks, in order: the input
    /// (400), the brand exists (400), the brand is in use (409, R-DEL-01), the name is free (409).
    /// </summary>
    /// <param name="actorId">The signed-in staff member.</param>
    public async Task<ProductSeriesDto> CreateAsync(ProductSeriesCreateRequest request, Guid actorId,
        CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var brandId = request.BrandId!.Value;
        await EnsureBrandInUseAsync(brandId, nameof(ProductSeriesCreateRequest.BrandId), cancellationToken);
        await EnsureNameFreeAsync(request.Name!, exceptId: null, cancellationToken);

        var series = ProductSeries.Create(request.Name!, CatalogSlug.From(request.Name!), brandId, actorId, clock.UtcNow);
        db.ProductSeries.Add(series);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // A parallel request created the same slug after the check above.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw NameTaken();
        }

        // The insert returns the generated id and xmin: the tracked series is the answer, no read after the commit.
        return ToDto(series);
    }

    /// <summary>
    /// Renames the series, moves it to another brand, retires or restores it (UC-013); the slug stays. Checks, in order:
    /// the input (400), the series exists (null), the version, a new brand — or the brand of a series being restored —
    /// exists (400) and is in use (409, R-DEL-01), the new name is free (409). A series in use may stay in a brand
    /// retired since. Whether its products allow a move is checked with them (R-PRD-07, PRD-05).
    /// </summary>
    /// <param name="expectedVersion">The <c>If-Match</c> version; null for <c>If-Match: *</c>.</param>
    /// <returns>The series after the change; null when there is no series <paramref name="id"/>.</returns>
    /// <exception cref="DbUpdateConcurrencyException">The series changed since <paramref name="expectedVersion"/>.</exception>
    public async Task<ProductSeriesDto?> UpdateAsync(Guid id, uint? expectedVersion, ProductSeriesUpdateRequest request,
        Guid actorId, CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var series = await db.ProductSeries.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (series is null)
        {
            return null;
        }

        if (expectedVersion is { } expected)
        {
            // An unchanged series issues no UPDATE, so the stored version is compared here as well as on save.
            if (series.Version != expected)
            {
                throw new DbUpdateConcurrencyException("The series changed since it was read.");
            }

            db.Entry(series).Property(s => s.Version).OriginalValue = expected;
        }

        var brandId = request.BrandId!.Value;
        // A series that moves or comes back into use needs a brand in use; one already in use may stay in a brand
        // retired since (retiring a brand never cascades).
        var restored = request.IsActive!.Value && !series.IsActive;
        if (brandId != series.BrandId || restored)
        {
            await EnsureBrandInUseAsync(brandId, nameof(ProductSeriesUpdateRequest.BrandId), cancellationToken);
        }

        if (request.Name!.Trim() != series.Name)
        {
            await EnsureNameFreeAsync(request.Name, series.Id, cancellationToken);
        }

        series.Update(request.Name, brandId, request.IsActive.Value, actorId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(series);
    }

    /// <summary>
    /// Refuses an unknown brand as input (400 on <paramref name="field"/>) and a retired one as a conflict: the screens
    /// that create data offer only the brands in use (R-DEL-01).
    /// </summary>
    private async Task EnsureBrandInUseAsync(Guid brandId, string field, CancellationToken cancellationToken)
    {
        var isActive = await db.Brands.Where(b => b.Id == brandId).Select(b => (bool?)b.IsActive)
            .SingleOrDefaultAsync(cancellationToken);
        if (isActive is null)
        {
            throw new ValidationException([new ValidationFailure(field, BrandNotFound) { ErrorCode = BrandNotFound }]);
        }

        if (isActive == false)
        {
            throw new BusinessRuleException("R-DEL-01", "brand-retired", field);
        }
    }

    /// <summary>
    /// Refuses a name whose slug another series holds, or that another series' name also gives: a renamed series keeps
    /// the slug of its first name, so both are compared. The slug is unique across brands; retired series count.
    /// </summary>
    private async Task EnsureNameFreeAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var slug = CatalogSlug.From(name);
        // A store has tens to hundreds of series: all of them are read, since the slug of a name is computed here, not in SQL.
        var others = await db.ProductSeries.AsNoTracking()
            .Where(s => s.Id != exceptId)
            .Select(s => new { s.Name, s.Slug })
            .ToListAsync(cancellationToken);
        if (others.Exists(s => s.Slug == slug || CatalogSlug.From(s.Name) == slug))
        {
            throw NameTaken();
        }
    }

    private static IQueryable<ProductSeriesDto> ToDto(IQueryable<ProductSeries> series) =>
        series.Select(s => new ProductSeriesDto(s.Id, s.Name, s.Slug, s.IsActive, s.BrandId, s.Version));

    private static ProductSeriesDto ToDto(ProductSeries series) =>
        new(series.Id, series.Name, series.Slug, series.IsActive, series.BrandId, series.Version);

    private static BusinessRuleException NameTaken() =>
        new(null, "product-series-already-exists", nameof(ProductSeriesCreateRequest.Name));

    /// <summary>The code of a brand id that names no brand (400 on the field, DEC-117 (2)).</summary>
    public const string BrandNotFound = "BRAND_NOT_FOUND";
}
