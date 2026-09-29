using Microsoft.Extensions.Time.Testing;
using Shouldly;
using ZenGear.Services.Common;

namespace ZenGear.Services.Tests.Common;

public class ClockTests
{
    [Test]
    public void R_TZ_01_Today_after_17_00_UTC_is_next_day_in_Vietnam()
    {
        var clock = new Clock(new FakeTimeProvider(new DateTimeOffset(2026, 9, 29, 17, 30, 0, TimeSpan.Zero)));

        clock.Today.ShouldBe(new DateOnly(2026, 9, 30));
    }

    [Test]
    public void R_TZ_01_Today_before_17_00_UTC_is_same_day_in_Vietnam()
    {
        var clock = new Clock(new FakeTimeProvider(new DateTimeOffset(2026, 9, 29, 16, 59, 59, TimeSpan.Zero)));

        clock.Today.ShouldBe(new DateOnly(2026, 9, 29));
    }

    [Test]
    public void R_TZ_01_DayRangeUtc_starts_at_17_00_UTC_previous_day()
    {
        var clock = new Clock(new FakeTimeProvider());

        var (startUtc, endUtc) = clock.DayRangeUtc(new DateOnly(2026, 9, 30));

        startUtc.ShouldBe(new DateTimeOffset(2026, 9, 29, 17, 0, 0, TimeSpan.Zero));
        endUtc.ShouldBe(new DateTimeOffset(2026, 9, 30, 17, 0, 0, TimeSpan.Zero));
        startUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Test]
    public void R_TZ_01_ToBusinessDate_uses_Vietnam_time_for_any_offset()
    {
        var clock = new Clock(new FakeTimeProvider());

        clock.ToBusinessDate(new DateTimeOffset(2026, 12, 31, 18, 0, 0, TimeSpan.Zero)).ShouldBe(new DateOnly(2027, 1, 1));
        clock.ToBusinessDate(new DateTimeOffset(2027, 1, 1, 0, 30, 0, TimeSpan.FromHours(7))).ShouldBe(new DateOnly(2027, 1, 1));
    }

    [Test]
    public void R_TZ_01_UtcNow_has_zero_offset()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 29, 9, 30, 0, TimeSpan.Zero));
        var clock = new Clock(time);

        time.Advance(TimeSpan.FromMinutes(5));

        clock.UtcNow.ShouldBe(new DateTimeOffset(2026, 9, 29, 9, 35, 0, TimeSpan.Zero));
        clock.UtcNow.Offset.ShouldBe(TimeSpan.Zero);
    }
}
