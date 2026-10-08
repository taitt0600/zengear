using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ZenGear.Data;
using ZenGear.Data.Entities.Inventory;
using ZenGear.Services.Common;
using ZenGear.Services.Common.Models;
using ZenGear.Services.Inventory.Models;

namespace ZenGear.Services.Inventory;

/// <summary>The business location as the storefront shows it (R-LOC-01): read by anyone, no ids of the edit form.</summary>
public sealed class BusinessLocationPublicService(ZenGearDbContext db, IValidator<PageRequest> pageValidator, IClock clock)
{
    /// <summary>Days of <see cref="BusinessLocationPublicDto.WeekHours"/>, from today.</summary>
    public const int WeekDays = 7;

    /// <summary>The locations by name (Phase 1: the one location, R-LOC-01).</summary>
    public async Task<BusinessLocationPublicPageDto> ListAsync(PageRequest page, CancellationToken cancellationToken)
    {
        await pageValidator.ValidateAndThrowAsync(page, cancellationToken);

        var locations = db.BusinessLocations.AsNoTracking();
        var rows = await locations
            .OrderBy(b => b.Name)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(b => new
            {
                Location = b,
                ProvinceName = db.Provinces.Where(p => p.Id == b.ProvinceId).Select(p => p.Name).First(),
                WardName = db.Wards.Where(w => w.Id == b.WardId).Select(w => w.Name).First(),
            })
            .ToListAsync(cancellationToken);

        // A first page that is not full holds every row: no COUNT (Phase 1 has one row).
        var total = page.Page == 1 && rows.Count < page.PageSize ? rows.Count : await locations.CountAsync(cancellationToken);
        var today = clock.Today;
        var items = rows.Select(r => ToDto(r.Location, r.WardName, r.ProvinceName, today)).ToList();
        return new BusinessLocationPublicPageDto(items, page.Page, page.PageSize, total);
    }

    private static BusinessLocationPublicDto ToDto(BusinessLocation location, string wardName, string provinceName, DateOnly today)
    {
        var weekHours = Enumerable.Range(0, WeekDays)
            .Select(offset => today.AddDays(offset))
            .Select(date => new DayHoursDto(date,
                [.. BusinessLocation.HoursOn(location.RegularHours, location.SpecialHours, date).Select(OpeningPeriodDto.From)]))
            .ToList();

        return new BusinessLocationPublicDto(location.Id, location.Name, location.Phone, location.Email,
            location.AddressDetail, wardName, provinceName,
            [.. location.RegularHours.OrderBy(r => r.Day).ThenBy(r => r.Open).Select(RegularHoursPeriodDto.From)],
            [.. (location.SpecialHours ?? []).Where(s => s.Date >= today).OrderBy(s => s.Date).ThenBy(s => s.Open).Select(SpecialHoursPeriodDto.From)],
            weekHours);
    }
}
