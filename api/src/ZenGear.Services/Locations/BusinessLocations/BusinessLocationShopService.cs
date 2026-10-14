using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZenGear.Data;
using ZenGear.Data.Entities.Locations;
using ZenGear.Services.Common;
using ZenGear.Services.Common.Models;

namespace ZenGear.Services.Locations.BusinessLocations;

/// <summary>The business location as the storefront shows it (R-LOC-01): read by anyone, no ids of the edit form.</summary>
public sealed class BusinessLocationShopService(ZenGearDbContext db, IValidator<PageRequest> pageValidator, IClock clock)
{
    /// <summary>Days of <see cref="BusinessLocationShopDto.WeekHours"/>, from today.</summary>
    public const int WeekDays = 7;

    /// <summary>The locations by name (Phase 1: the one location, R-LOC-01).</summary>
    public async Task<PagedDto<BusinessLocationShopDto>> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        await pageValidator.ValidateAndThrowAsync(page, cancellationToken);

        var rows = db.BusinessLocations.AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new
            {
                Location = b,
                ProvinceName = db.Provinces.Where(p => p.Id == b.ProvinceId).Select(p => p.Name).First(),
                WardName = db.Wards.Where(w => w.Id == b.WardId).Select(w => w.Name).First(),
            });

        var today = clock.Today;
        return await PagedQuery.ToPagedDtoAsync(rows, page, r => ToDto(r.Location, r.WardName, r.ProvinceName, today),
            cancellationToken);
    }

    private static BusinessLocationShopDto ToDto(BusinessLocation location, string wardName, string provinceName, DateOnly today)
    {
        var weekHours = Enumerable.Range(0, WeekDays)
            .Select(offset => today.AddDays(offset))
            .Select(date => new DayHoursDto(date,
                [.. BusinessLocation.HoursOn(location.RegularHours, location.SpecialHours, date).Select(OpeningPeriodDto.From)]))
            .ToList();

        return new BusinessLocationShopDto(location.Id, location.Name, location.Phone, location.Email,
            location.AddressDetail, wardName, provinceName,
            [.. location.RegularHours.OrderBy(r => r.Day).ThenBy(r => r.Open).Select(RegularHoursPeriodDto.From)],
            [.. (location.SpecialHours ?? []).Where(s => s.Date >= today).OrderBy(s => s.Date).ThenBy(s => s.Open).Select(SpecialHoursPeriodDto.From)],
            weekHours);
    }
}
