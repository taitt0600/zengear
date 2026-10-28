using ZenGear.Data.Common;

namespace ZenGear.Data.Entities.Locations;

/// <summary>
/// The store's business location (<c>business_locations</c>, LOC-01). It is created at go-live by
/// <c>api/seed/08_business_location.sql</c>: the code offers no way to add one (R-LOC-01).
/// </summary>
public class BusinessLocation : IAggregateRoot
{
    /// <summary>The most opening periods of one weekday or one holiday date.</summary>
    public const int MaxPeriodsPerDay = 3;

    // The codes the hours checks return (DEC-117 (2)); the API writes their texts.
    public const string RegularHoursEmpty = "REGULAR_HOURS_EMPTY";
    public const string DayOutOfRange = "DAY_OUT_OF_RANGE";
    public const string OpenTimeInvalid = "OPEN_TIME_INVALID";
    public const string PeriodsPerDayTooMany = "PERIODS_PER_DAY_TOO_MANY";
    public const string PeriodOpensAfterClosing = "PERIOD_OPENS_AFTER_CLOSING";
    public const string PeriodsOverlap = "PERIODS_OVERLAP";

    protected BusinessLocation()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Hotline, digits only.</summary>
    public string Phone { get; private set; } = null!;

    /// <summary>Contact email, lowercase.</summary>
    public string? Email { get; private set; }

    /// <summary>Street-level detail of the address; the ward and province are <see cref="WardId"/> and <see cref="ProvinceId"/>.</summary>
    public string AddressDetail { get; private set; } = null!;

    public IReadOnlyList<RegularHoursPeriod> RegularHours { get; private set; } = [];

    /// <summary>Holiday schedule; null when there is none.</summary>
    public IReadOnlyList<SpecialHoursPeriod>? SpecialHours { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int ProvinceId { get; private set; }

    /// <summary>Belongs to <see cref="ProvinceId"/> (<c>fk_business_locations_wards_ward_id_province_id</c>).</summary>
    public int WardId { get; private set; }

    public uint Version { get; private set; }

    /// <summary>
    /// Replaces the details the Admin edits (UC-070); stamps the change only when something differs. Text is trimmed,
    /// the email lowercased, the periods sorted by day or date and opening time, an empty holiday schedule stored as none. The service validates first for a clear error;
    /// these checks are the backstop.
    /// </summary>
    /// <exception cref="ArgumentException">A blank name, hotline or address, or hours that <see cref="CheckHours"/> refuses.</exception>
    public void Update(BusinessLocationDetails details, Guid updatedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var name = Required(details.Name, nameof(details.Name));
        var phone = Required(details.Phone, nameof(details.Phone));
        var addressDetail = Required(details.AddressDetail, nameof(details.AddressDetail));
        var email = string.IsNullOrWhiteSpace(details.Email) ? null : details.Email.Trim().ToLowerInvariant();
        ArgumentNullException.ThrowIfNull(details.RegularHours);
        CheckHours(details.RegularHours, details.SpecialHours);
        List<RegularHoursPeriod> regularHours = [.. details.RegularHours.OrderBy(r => r.Day).ThenBy(r => r.Open)];
        List<SpecialHoursPeriod>? specialHours = details.SpecialHours is { Count: > 0 } special
            ? [.. special.OrderBy(s => s.Date).ThenBy(s => s.Open)]
            : null;

        var unchanged = name == Name && phone == Phone && email == Email && addressDetail == AddressDetail
            && details.ProvinceId == ProvinceId && details.WardId == WardId
            // The stored lists are compared sorted too: a row written by hand may hold them in another order.
            && regularHours.SequenceEqual(RegularHours.OrderBy(r => r.Day).ThenBy(r => r.Open))
            && (specialHours ?? []).SequenceEqual((SpecialHours ?? []).OrderBy(s => s.Date).ThenBy(s => s.Open));
        if (unchanged)
        {
            return;
        }

        Name = name;
        Phone = phone;
        Email = email;
        AddressDetail = addressDetail;
        ProvinceId = details.ProvinceId;
        WardId = details.WardId;
        RegularHours = regularHours;
        SpecialHours = specialHours;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>The periods the location is open on <paramref name="date"/>, by opening time (R-LOC-01).</summary>
    public static IReadOnlyList<OpeningPeriod> HoursOn(IReadOnlyList<RegularHoursPeriod> regularHours,
        IReadOnlyList<SpecialHoursPeriod>? specialHours, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(regularHours);
        var special = (specialHours ?? []).Where(s => s.Date == date).ToList();
        if (special.Count > 0)
        {
            return [.. special
                .Where(s => !s.Closed && s.Open is not null && s.Close is not null)
                .Select(s => new OpeningPeriod(s.Open!.Value, s.Close!.Value))
                .OrderBy(p => p.Open)];
        }

        var day = WeekDay(date);
        return [.. regularHours.Where(r => r.Day == day).Select(r => new OpeningPeriod(r.Open, r.Close)).OrderBy(p => p.Open)];
    }

    /// <summary>1 = Monday … 7 = Sunday, the day numbers of <see cref="RegularHoursPeriod.Day"/>.</summary>
    public static int WeekDay(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    /// <summary>The shape of the weekly hours and the holiday schedule (R-LOC-01).</summary>
    /// <exception cref="ArgumentException">
    /// The weekly hours or the holiday schedule have the wrong shape (<see cref="RegularHoursProblem"/>,
    /// <see cref="SpecialHoursProblem"/>).
    /// </exception>
    public static void CheckHours(IReadOnlyList<RegularHoursPeriod> regularHours, IReadOnlyList<SpecialHoursPeriod>? specialHours)
    {
        if (RegularHoursProblem(regularHours) is { } regularProblem)
        {
            throw new ArgumentException(regularProblem, nameof(regularHours));
        }

        if (SpecialHoursProblem(specialHours) is { } specialProblem)
        {
            throw new ArgumentException(specialProblem, nameof(specialHours));
        }
    }

    /// <summary>The code of what is wrong with the weekly hours (<see cref="CheckHours"/>); null when nothing is.</summary>
    public static string? RegularHoursProblem(IReadOnlyList<RegularHoursPeriod> regularHours)
    {
        ArgumentNullException.ThrowIfNull(regularHours);
        if (regularHours.Count == 0)
        {
            return RegularHoursEmpty;
        }

        if (regularHours.Any(r => r.Day is < 1 or > 7))
        {
            return DayOutOfRange;
        }

        return regularHours.GroupBy(r => r.Day)
            .Select(day => PeriodsProblem([.. day.Select(r => new OpeningPeriod(r.Open, r.Close))]))
            .FirstOrDefault(problem => problem is not null);
    }

    /// <summary>
    /// The code of what is wrong with the holiday schedule (<see cref="CheckHours"/>); null when nothing is.
    /// <see cref="ClosedDateWithHours"/> breaks R-LOC-01; an open date without its times is <see cref="OpenTimeInvalid"/>.
    /// </summary>
    public static string? SpecialHoursProblem(IReadOnlyList<SpecialHoursPeriod>? specialHours)
    {
        foreach (var date in (specialHours ?? []).GroupBy(s => s.Date))
        {
            var entries = date.ToList();
            if (entries.Exists(s => s.Closed))
            {
                if (entries.Count > 1 || entries[0].Open is not null || entries[0].Close is not null)
                {
                    return ClosedDateWithHours;
                }

                continue;
            }

            if (entries.Exists(s => s.Open is null || s.Close is null))
            {
                return OpenTimeInvalid;
            }

            if (PeriodsProblem([.. entries.Select(s => new OpeningPeriod(s.Open!.Value, s.Close!.Value))]) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    /// <summary>The code of a closed holiday date that also has hours or other entries (R-LOC-01).</summary>
    public const string ClosedDateWithHours = "SPECIAL_HOURS_CLOSED_DATE_WITH_HOURS";

    private static string? PeriodsProblem(List<OpeningPeriod> periods)
    {
        if (periods.Count > MaxPeriodsPerDay)
        {
            return PeriodsPerDayTooMany;
        }

        if (periods.Exists(p => p.Open >= p.Close))
        {
            return PeriodOpensAfterClosing;
        }

        var ordered = periods.OrderBy(p => p.Open).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Open < ordered[i - 1].Close)
            {
                return PeriodsOverlap;
            }
        }

        return null;
    }

    private static string Required(string? value, string paramName)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length == 0 ? throw new ArgumentException("A required value is blank.", paramName) : trimmed;
    }
}
