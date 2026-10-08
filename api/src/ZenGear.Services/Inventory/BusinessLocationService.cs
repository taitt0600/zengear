using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using ZenGear.Data;
using ZenGear.Data.Entities.Inventory;
using ZenGear.Services.Common;
using ZenGear.Services.Inventory.Models;

namespace ZenGear.Services.Inventory;

/// <summary>
/// The business location for the Admin (LOC-01, UC-070). Phase 1 has exactly one, created at go-live: there is no
/// method to add or remove one (R-LOC-01).
/// </summary>
public sealed class BusinessLocationService(
    ZenGearDbContext db,
    IValidator<BusinessLocationUpdateRequest> validator,
    IClock clock)
{
    /// <summary>The location <paramref name="id"/>; null when there is none.</summary>
    public async Task<BusinessLocationDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await db.BusinessLocations
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new
            {
                Location = b,
                ProvinceName = db.Provinces.Where(p => p.Id == b.ProvinceId).Select(p => p.Name).First(),
                WardName = db.Wards.Where(w => w.Id == b.WardId).Select(w => w.Name).First(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return null;
        }

        var location = found.Location;
        return new BusinessLocationDto(location.Id, location.Name, location.Phone, location.Email, location.AddressDetail,
            location.ProvinceId, found.ProvinceName, location.WardId, found.WardName,
            [.. location.RegularHours.Select(RegularHoursPeriodDto.From)],
            [.. (location.SpecialHours ?? []).Select(SpecialHoursPeriodDto.From)],
            location.UpdatedAt, location.Version);
    }

    /// <summary>
    /// Replaces the address, hotline and hours of the location (UC-070); holiday entries before today
    /// (Asia/Ho_Chi_Minh) are dropped. Checks, in order: the input (400), the location
    /// exists (null), the version, the ward belongs to the province (UC-070 3a, 400); a ward or province chosen anew must
    /// still be in use, while one kept from before may have been retired since. A refused request changes nothing.
    /// </summary>
    /// <param name="expectedVersion">The <c>If-Match</c> version; null for <c>If-Match: *</c>.</param>
    /// <returns>The location after the change; null when there is no location <paramref name="id"/>.</returns>
    /// <exception cref="DbUpdateConcurrencyException">The location changed since <paramref name="expectedVersion"/>.</exception>
    public async Task<BusinessLocationDto?> UpdateAsync(Guid id, uint? expectedVersion, BusinessLocationUpdateRequest request,
        Guid userId, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var location = await db.BusinessLocations.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (location is null)
        {
            return null;
        }

        if (expectedVersion is { } expected)
        {
            // An unchanged location issues no UPDATE, so the stored version is compared here as well as on save.
            if (location.Version != expected)
            {
                throw new DbUpdateConcurrencyException("The business location changed since it was read.");
            }

            db.Entry(location).Property(b => b.Version).OriginalValue = expected;
        }

        var today = clock.Today;
        var provinceId = request.ProvinceId!.Value;
        var wardId = request.WardId!.Value;
        await CheckWardAsync(provinceId, provinceId != location.ProvinceId, wardId, wardId != location.WardId,
            cancellationToken);

        location.Update(new BusinessLocationDetails(request.Name!, request.Phone!, request.Email, request.AddressDetail!,
                provinceId, wardId,
                [.. request.RegularHours!.Select(p => p.ToPeriod())],
                // Past holidays no longer change any hours: not stored, so they never pile up against MaxSpecialHours.
                request.SpecialHours is null ? null : [.. request.SpecialHours.Select(s => s.ToPeriod()).Where(s => s.Date >= today)]),
            userId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>The province exists and the ward belongs to it (UC-070 3a); each must be active when newly chosen.</summary>
    private async Task CheckWardAsync(int provinceId, bool provinceChanged, int wardId, bool wardChanged,
        CancellationToken cancellationToken)
    {
        var province = await db.Provinces.AsNoTracking()
            .Where(p => p.Id == provinceId)
            .Select(p => new { p.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (province is null || (provinceChanged && !province.IsActive))
        {
            throw Invalid(nameof(BusinessLocationUpdateRequest.ProvinceId), "No province in use has this id.");
        }

        var ward = await db.Wards.AsNoTracking()
            .Where(w => w.Id == wardId)
            .Select(w => new { w.ProvinceId, w.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (ward is null || ward.ProvinceId != provinceId)
        {
            throw Invalid(nameof(BusinessLocationUpdateRequest.WardId), "The ward does not belong to the chosen province.");
        }

        if (wardChanged && !ward.IsActive)
        {
            throw Invalid(nameof(BusinessLocationUpdateRequest.WardId), "The ward is no longer in use.");
        }
    }

    private static ValidationException Invalid(string propertyName, string message) =>
        new([new ValidationFailure(propertyName, message)]);
}
