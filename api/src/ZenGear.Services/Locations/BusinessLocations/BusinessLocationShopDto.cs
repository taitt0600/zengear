namespace ZenGear.Services.Locations.BusinessLocations;

/// <summary>
/// The business location as the website, receipts and emails show it (R-LOC-01; the seller's information of the
/// storefront): the address by name, the hours and the holiday schedule.
/// </summary>
/// <param name="SpecialHours">Holiday entries from today on (Asia/Ho_Chi_Minh).</param>
/// <param name="WeekHours">The 7 days from today, each with its hours after the holiday schedule replaced the weekly ones.</param>
public sealed record BusinessLocationShopDto(
    Guid Id,
    string Name,
    string Phone,
    string? Email,
    string AddressDetail,
    string WardName,
    string ProvinceName,
    IReadOnlyList<RegularHoursPeriodDto> RegularHours,
    IReadOnlyList<SpecialHoursPeriodDto> SpecialHours,
    IReadOnlyList<DayHoursDto> WeekHours);
