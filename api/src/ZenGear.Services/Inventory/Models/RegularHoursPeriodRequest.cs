using System.Globalization;
using ZenGear.Data.Entities.Inventory;

namespace ZenGear.Services.Inventory.Models;

/// <summary>One opening period of the weekly hours.</summary>
/// <param name="Day">1 = Monday … 7 = Sunday.</param>
/// <param name="Open"><c>HH:mm</c>, before <paramref name="Close"/>.</param>
/// <param name="Close"><c>HH:mm</c>; no period runs past midnight.</param>
public sealed record RegularHoursPeriodRequest(int? Day, string? Open, string? Close)
{
    /// <summary>The period as the entity stores it; only for a period the validator accepted.</summary>
    internal RegularHoursPeriod ToPeriod() => new(Day!.Value, ParseTime(Open!), ParseTime(Close!));

    /// <summary>An <c>HH:mm</c> time the validator accepted.</summary>
    internal static TimeOnly ParseTime(string value) => TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);
}
