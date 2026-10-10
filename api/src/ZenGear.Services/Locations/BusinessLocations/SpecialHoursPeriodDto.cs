using ZenGear.Data.Entities.Locations;

namespace ZenGear.Services.Locations.BusinessLocations;

/// <summary>One entry of the holiday schedule; its entries replace the weekly hours of their date (R-LOC-01).</summary>
/// <param name="Closed">Closed all day; <paramref name="Open"/> and <paramref name="Close"/> are null.</param>
/// <param name="Open"><c>HH:mm</c>; null when closed.</param>
/// <param name="Close"><c>HH:mm</c>; null when closed.</param>
public sealed record SpecialHoursPeriodDto(DateOnly Date, bool Closed, string? Open, string? Close)
{
    internal static SpecialHoursPeriodDto From(SpecialHoursPeriod period) =>
        new(period.Date, period.Closed,
            period.Open is { } open ? OpeningPeriodDto.Format(open) : null,
            period.Close is { } close ? OpeningPeriodDto.Format(close) : null);
}
