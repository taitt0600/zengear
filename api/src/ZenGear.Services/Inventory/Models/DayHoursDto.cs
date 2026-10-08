namespace ZenGear.Services.Inventory.Models;

/// <summary>The opening hours of one date, with the holiday schedule applied (R-LOC-01).</summary>
/// <param name="Periods">By opening time; empty when the location is closed that day.</param>
public sealed record DayHoursDto(DateOnly Date, IReadOnlyList<OpeningPeriodDto> Periods);
