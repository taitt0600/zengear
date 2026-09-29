namespace ZenGear.Services.Common;

public sealed class Clock(TimeProvider timeProvider) : IClock
{
    public const string BusinessTimeZoneId = "Asia/Ho_Chi_Minh";

    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZoneId);

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateOnly Today => ToBusinessDate(UtcNow);

    public DateOnly ToBusinessDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, BusinessTimeZone).DateTime);

    public (DateTimeOffset StartUtc, DateTimeOffset EndUtc) DayRangeUtc(DateOnly date) =>
        (StartOfDayUtc(date), StartOfDayUtc(date.AddDays(1)));

    private static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, BusinessTimeZone), TimeSpan.Zero);
    }
}
