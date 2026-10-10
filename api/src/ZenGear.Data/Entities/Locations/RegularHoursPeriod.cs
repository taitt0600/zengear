namespace ZenGear.Data.Entities.Locations;

/// <summary>
/// One opening period of the weekly hours (giờ mở cửa hằng tuần, <c>business_locations.regular_hours</c>); a day may
/// have several, a day without one is closed. Stored as <c>{"day": 1, "open": "08:30", "close": "21:30"}</c>.
/// </summary>
/// <param name="Day">1 = Monday … 7 = Sunday.</param>
/// <param name="Open">Before <paramref name="Close"/>: no period runs past midnight.</param>
public sealed record RegularHoursPeriod(int Day, TimeOnly Open, TimeOnly Close);
