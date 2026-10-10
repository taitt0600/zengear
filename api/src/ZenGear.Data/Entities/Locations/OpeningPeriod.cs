namespace ZenGear.Data.Entities.Locations;

/// <summary>A period the location is open on a given date (<see cref="BusinessLocation.HoursOn"/>).</summary>
public sealed record OpeningPeriod(TimeOnly Open, TimeOnly Close);
