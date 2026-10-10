namespace ZenGear.Services.Locations.BusinessLocations;

/// <summary>
/// What the Admin edits on the business location (UC-070): every field is sent, the lists replace the stored ones.
/// Values are nullable so the validator, not model binding, answers a missing one.
/// </summary>
/// <param name="Phone">Hotline, 8 to 11 digits (<c>02838123456</c>, <c>1900636123</c>).</param>
/// <param name="WardId">Belongs to <paramref name="ProvinceId"/> (UC-070 3a).</param>
/// <param name="RegularHours">The weekly hours: at least one period; a weekday without one is closed.</param>
/// <param name="SpecialHours">The holiday schedule; null or empty for none. Its entries replace the weekly hours of their date (R-LOC-01).</param>
public sealed record BusinessLocationUpdateRequest(
    string? Name,
    string? Phone,
    string? Email,
    string? AddressDetail,
    int? ProvinceId,
    int? WardId,
    IReadOnlyList<RegularHoursPeriodRequest>? RegularHours,
    IReadOnlyList<SpecialHoursPeriodRequest>? SpecialHours);
