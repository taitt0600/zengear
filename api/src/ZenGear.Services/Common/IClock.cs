namespace ZenGear.Services.Common;

/// <summary>
/// The one source of the current time (R-TZ-01): instants are UTC for storage,
/// every day-based calculation uses the business day in Asia/Ho_Chi_Minh.
/// </summary>
public interface IClock
{
    /// <summary>Current instant, offset zero.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Current business day in Asia/Ho_Chi_Minh.</summary>
    DateOnly Today { get; }

    /// <summary>Business day in Asia/Ho_Chi_Minh of an instant.</summary>
    DateOnly ToBusinessDate(DateTimeOffset instant);

    /// <summary>UTC range [00:00, next day 00:00) of a business day in Asia/Ho_Chi_Minh.</summary>
    (DateTimeOffset StartUtc, DateTimeOffset EndUtc) DayRangeUtc(DateOnly date);
}
