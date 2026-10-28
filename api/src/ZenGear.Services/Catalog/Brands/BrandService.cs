using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZenGear.Data;
using ZenGear.Data.Entities.Catalog;
using ZenGear.Services.Common;
using ZenGear.Services.Common.Models;

namespace ZenGear.Services.Catalog.Brands;

/// <summary>Brands, for the staff screens (PRD-02); none is ever deleted (R-DEL-01).</summary>
public sealed class BrandService(
    ZenGearDbContext db,
    IValidator<BrandCreateRequest> createValidator,
    IValidator<BrandUpdateRequest> updateValidator,
    IValidator<PageRequest> pageValidator,
    IClock clock)
{
    /// <summary>One page of the brands by name; <paramref name="isActive"/> true gives the list that creates new data (R-DEL-01).</summary>
    public async Task<PagedDto<BrandDto>> ListAsync(bool? isActive, PageRequest page, CancellationToken cancellationToken)
    {
        await pageValidator.ValidateAndThrowAsync(page, cancellationToken);

        var brands = db.Brands.AsNoTracking();
        if (isActive is { } active)
        {
            brands = brands.Where(b => b.IsActive == active);
        }

        // Names sort in Vietnamese order through the database collation; the id keeps pages stable.
        return await PagedQuery.ToPagedDtoAsync(ToDto(brands.OrderBy(b => b.Name).ThenBy(b => b.Id)), page, cancellationToken);
    }

    /// <summary>The brand <paramref name="id"/>, retired or not; null when there is none.</summary>
    public Task<BrandDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToDto(db.Brands.AsNoTracking().Where(b => b.Id == id)).SingleOrDefaultAsync(cancellationToken);

    /// <summary>Creates an active brand, its slug made from the name (UC-010).</summary>
    /// <param name="actorId">The signed-in staff member.</param>
    public async Task<BrandDto> CreateAsync(BrandCreateRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureNameFreeAsync(request.Name!, exceptId: null, cancellationToken);

        var brand = Brand.Create(request.Name!, CatalogSlug.From(request.Name!), actorId, clock.UtcNow);
        db.Brands.Add(brand);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // A parallel request created the same slug after the check above.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw NameTaken();
        }

        // The insert returns the generated id and xmin: the tracked brand is the answer, no read after the commit.
        return ToDto(brand);
    }

    /// <summary>
    /// Renames, retires or restores the brand (UC-011); the slug stays. Checks, in order: the input (400), the brand
    /// exists (null), the version, the new name is free (409).
    /// </summary>
    /// <param name="expectedVersion">The <c>If-Match</c> version; null for <c>If-Match: *</c>.</param>
    /// <returns>The brand after the change; null when there is no brand <paramref name="id"/>.</returns>
    /// <exception cref="DbUpdateConcurrencyException">The brand changed since <paramref name="expectedVersion"/>.</exception>
    public async Task<BrandDto?> UpdateAsync(Guid id, uint? expectedVersion, BrandUpdateRequest request, Guid actorId,
        CancellationToken cancellationToken)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var brand = await db.Brands.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (brand is null)
        {
            return null;
        }

        if (expectedVersion is { } expected)
        {
            // An unchanged brand issues no UPDATE, so the stored version is compared here as well as on save.
            if (brand.Version != expected)
            {
                throw new DbUpdateConcurrencyException("The brand changed since it was read.");
            }

            db.Entry(brand).Property(b => b.Version).OriginalValue = expected;
        }

        if (request.Name!.Trim() != brand.Name)
        {
            await EnsureNameFreeAsync(request.Name, brand.Id, cancellationToken);
        }

        brand.Update(request.Name, request.IsActive!.Value, actorId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(brand);
    }

    /// <summary>
    /// Refuses a name whose slug another brand holds, or that another brand's name also gives: a renamed brand keeps the
    /// slug of its first name, so both are compared. Retired brands count.
    /// </summary>
    private async Task EnsureNameFreeAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var slug = CatalogSlug.From(name);
        // A store has tens to hundreds of brands: all of them are read, since the slug of a name is computed here, not in SQL.
        var others = await db.Brands.AsNoTracking()
            .Where(b => b.Id != exceptId)
            .Select(b => new { b.Name, b.Slug })
            .ToListAsync(cancellationToken);
        if (others.Exists(b => b.Slug == slug || CatalogSlug.From(b.Name) == slug))
        {
            throw NameTaken();
        }
    }

    private static IQueryable<BrandDto> ToDto(IQueryable<Brand> brands) =>
        brands.Select(b => new BrandDto(b.Id, b.Name, b.Slug, b.IsActive, b.Version));

    private static BrandDto ToDto(Brand brand) => new(brand.Id, brand.Name, brand.Slug, brand.IsActive, brand.Version);

    private static BusinessRuleException NameTaken() =>
        new(null, "brand-already-exists", nameof(BrandCreateRequest.Name));
}
