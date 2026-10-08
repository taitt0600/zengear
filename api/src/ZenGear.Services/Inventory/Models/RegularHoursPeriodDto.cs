using ZenGear.Data.Entities.Inventory;

namespace ZenGear.Services.Inventory.Models;

/// <summary>One opening period of the weekly hours.</summary>
/// <param name="Day">1 = Monday … 7 = Sunday.</param>
/// <param name="Open"><c>HH:mm</c>.</param>
/// <param name="Close"><c>HH:mm</c>.</param>
public sealed record RegularHoursPeriodDto(int Day, string Open, string Close)
{
    internal static RegularHoursPeriodDto From(RegularHoursPeriod period) =>
        new(period.Day, OpeningPeriodDto.Format(period.Open), OpeningPeriodDto.Format(period.Close));
}
