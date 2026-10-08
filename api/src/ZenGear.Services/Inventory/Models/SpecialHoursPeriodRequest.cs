using ZenGear.Data.Entities.Inventory;

namespace ZenGear.Services.Inventory.Models;

/// <summary>One entry of the holiday schedule.</summary>
/// <param name="Closed">True: closed all day, without <paramref name="Open"/> and <paramref name="Close"/>, and the only entry of its date (R-LOC-01).</param>
/// <param name="Open"><c>HH:mm</c>, required when not <paramref name="Closed"/>.</param>
public sealed record SpecialHoursPeriodRequest(DateOnly? Date, bool? Closed, string? Open, string? Close)
{
    /// <summary>The entry as the entity stores it; only for an entry the validator accepted.</summary>
    internal SpecialHoursPeriod ToPeriod() =>
        new(Date!.Value, Closed!.Value,
            Open is null ? null : RegularHoursPeriodRequest.ParseTime(Open),
            Close is null ? null : RegularHoursPeriodRequest.ParseTime(Close));
}
