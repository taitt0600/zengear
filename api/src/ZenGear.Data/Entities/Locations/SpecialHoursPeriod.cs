namespace ZenGear.Data.Entities.Locations;

/// <summary>
/// One entry of the holiday schedule (lịch riêng ngày lễ, <c>business_locations.special_hours</c>): the entries of a date
/// replace the weekly hours of that day (R-LOC-01). Stored as <c>{"date": "2027-02-06", "closed": true}</c> or
/// <c>{"date": "2027-02-10", "closed": false, "open": "10:00", "close": "18:00"}</c>.
/// </summary>
/// <param name="Closed">True: closed all day; the only entry of its date, without hours.</param>
/// <param name="Open">Set, and before <paramref name="Close"/>, when not <paramref name="Closed"/>.</param>
public sealed record SpecialHoursPeriod(DateOnly Date, bool Closed, TimeOnly? Open, TimeOnly? Close);
