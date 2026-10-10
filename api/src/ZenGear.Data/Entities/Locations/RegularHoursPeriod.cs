namespace ZenGear.Data.Entities.Locations;

/// <summary>
/// One opening period of the weekly hours (<c>business_locations.regular_hours</c>); a day without one is closed.
/// </summary>
/// <param name="Day">1 = Monday … 7 = Sunday.</param>
/// <param name="Open">Before <paramref name="Close"/>: no period runs past midnight.</param>
public sealed record RegularHoursPeriod(int Day, TimeOnly Open, TimeOnly Close);
