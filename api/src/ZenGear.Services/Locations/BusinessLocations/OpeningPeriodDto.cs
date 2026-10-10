using System.Globalization;
using ZenGear.Data.Common;
using ZenGear.Data.Entities.Locations;

namespace ZenGear.Services.Locations.BusinessLocations;

/// <summary>A period the location is open on a given date.</summary>
/// <param name="Open"><c>HH:mm</c>.</param>
/// <param name="Close"><c>HH:mm</c>.</param>
public sealed record OpeningPeriodDto(string Open, string Close)
{
    internal static OpeningPeriodDto From(OpeningPeriod period) => new(Format(period.Open), Format(period.Close));

    /// <summary>A time as the API and the database write it, <c>HH:mm</c>.</summary>
    internal static string Format(TimeOnly time) =>
        time.ToString(HourMinuteTimeJsonConverter.Format, CultureInfo.InvariantCulture);
}
