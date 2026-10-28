using System.Reflection;
using Shouldly;
using ZenGear.Data.Entities.Locations;

namespace ZenGear.Services.Tests.Locations.BusinessLocations;

/// <summary>The opening hours of the business location (R-LOC-01, UC-070 2b).</summary>
public class BusinessLocationHoursTests
{
    // 2027-02-08 is a Monday.
    private static readonly DateOnly Monday = new(2027, 2, 8);
    private static readonly DateOnly Sunday = new(2027, 2, 14);

    private static readonly RegularHoursPeriod[] Weekly =
    [
        new(1, new(8, 30), new(12, 0)),
        new(1, new(13, 30), new(21, 30)),
        new(7, new(9, 0), new(18, 0)),
    ];

    [Test]
    public void R_LOC_01_a_day_without_special_hours_uses_the_weekly_hours()
    {
        BusinessLocation.HoursOn(Weekly, null, Monday)
            .ShouldBe([new OpeningPeriod(new(8, 30), new(12, 0)), new OpeningPeriod(new(13, 30), new(21, 30))]);
        BusinessLocation.HoursOn(Weekly, [], Sunday).ShouldBe([new OpeningPeriod(new(9, 0), new(18, 0))]);
        BusinessLocation.HoursOn(Weekly, null, Monday.AddDays(1)).ShouldBeEmpty();
    }

    [Test]
    public void R_LOC_01_special_hours_replace_the_weekly_hours_of_that_date()
    {
        SpecialHoursPeriod[] special = [new(Monday, false, new(10, 0), new(16, 0))];

        BusinessLocation.HoursOn(Weekly, special, Monday).ShouldBe([new OpeningPeriod(new(10, 0), new(16, 0))]);
        BusinessLocation.HoursOn(Weekly, special, Sunday).ShouldBe([new OpeningPeriod(new(9, 0), new(18, 0))]);
    }

    [Test]
    public void R_LOC_01_a_closed_special_date_has_no_hours()
    {
        BusinessLocation.HoursOn(Weekly, [new SpecialHoursPeriod(Sunday, true, null, null)], Sunday).ShouldBeEmpty();
    }

    [Test]
    public void R_LOC_01_check_hours_accepts_weekly_and_holiday_hours()
    {
        Should.NotThrow(() => BusinessLocation.CheckHours(Weekly,
            [new(Monday, true, null, null), new(Sunday, false, new(8, 0), new(12, 0)), new(Sunday, false, new(12, 0), new(17, 0))]));
    }

    [Test]
    public void R_LOC_01_check_hours_refuses_a_closed_date_with_hours()
    {
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours(Weekly,
            [new(Monday, true, null, null), new(Monday, false, new(8, 0), new(12, 0))]));
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours(Weekly, [new(Monday, true, new(8, 0), new(12, 0))]));
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours(Weekly, [new(Monday, false, null, null)]));
    }

    [Test]
    public void R_LOC_01_check_hours_refuses_overlapping_or_reversed_periods()
    {
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours(
            [new(1, new(8, 0), new(12, 0)), new(1, new(11, 0), new(14, 0))], null));
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours([new(2, new(18, 0), new(8, 0))], null));
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours(
            Weekly, [new(Monday, false, new(8, 0), new(12, 0)), new(Monday, false, new(9, 0), new(10, 0))]));
    }

    [Test]
    public void R_LOC_01_check_hours_refuses_a_day_outside_the_week_or_no_weekly_hours()
    {
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours([new(0, new(8, 0), new(12, 0))], null));
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours([new(8, new(8, 0), new(12, 0))], null));
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours([], null));
    }

    [Test]
    public void R_LOC_01_check_hours_refuses_more_than_three_periods_a_day()
    {
        Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours(
        [
            new(3, new(6, 0), new(7, 0)), new(3, new(8, 0), new(9, 0)), new(3, new(10, 0), new(11, 0)),
            new(3, new(12, 0), new(13, 0)),
        ], null));
    }

    [Test]
    public void R_LOC_01_the_weekly_hours_problem_is_a_code()
    {
        BusinessLocation.RegularHoursProblem(Weekly).ShouldBeNull();
        BusinessLocation.RegularHoursProblem([]).ShouldBe(BusinessLocation.RegularHoursEmpty);
        BusinessLocation.RegularHoursProblem([new(8, new(8, 0), new(12, 0))]).ShouldBe(BusinessLocation.DayOutOfRange);
        BusinessLocation.RegularHoursProblem([new(2, new(18, 0), new(8, 0))]).ShouldBe(BusinessLocation.PeriodOpensAfterClosing);
        BusinessLocation.RegularHoursProblem([new(1, new(8, 0), new(12, 0)), new(1, new(11, 0), new(14, 0))])
            .ShouldBe(BusinessLocation.PeriodsOverlap);
        BusinessLocation.RegularHoursProblem(
        [
            new(3, new(6, 0), new(7, 0)), new(3, new(8, 0), new(9, 0)), new(3, new(10, 0), new(11, 0)),
            new(3, new(12, 0), new(13, 0)),
        ]).ShouldBe(BusinessLocation.PeriodsPerDayTooMany);
    }

    [Test]
    public void R_LOC_01_the_holiday_hours_problem_is_a_code()
    {
        BusinessLocation.SpecialHoursProblem(null).ShouldBeNull();
        BusinessLocation.SpecialHoursProblem([new(Monday, true, null, null), new(Monday, false, new(8, 0), new(12, 0))])
            .ShouldBe(BusinessLocation.ClosedDateWithHours);
        BusinessLocation.SpecialHoursProblem([new(Monday, true, new(8, 0), new(12, 0))]).ShouldBe(BusinessLocation.ClosedDateWithHours);
        BusinessLocation.SpecialHoursProblem([new(Monday, false, null, null)]).ShouldBe(BusinessLocation.OpenTimeInvalid);
        BusinessLocation.SpecialHoursProblem([new(Monday, false, new(8, 0), new(12, 0)), new(Monday, false, new(9, 0), new(10, 0))])
            .ShouldBe(BusinessLocation.PeriodsOverlap);
    }

    [Test]
    public void R_LOC_01_check_hours_names_the_code_and_the_list()
    {
        var exception = Should.Throw<ArgumentException>(() => BusinessLocation.CheckHours([], null));

        exception.ParamName.ShouldBe("regularHours");
        exception.Message.ShouldStartWith(BusinessLocation.RegularHoursEmpty);
    }

    [Test]
    public void R_LOC_01_business_location_cannot_be_created_by_code()
    {
        // Phase 1 has exactly the one location of the go-live seed: no public constructor, no factory (no add).
        var type = typeof(BusinessLocation);
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
        type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == type)
            .ShouldBeEmpty();
    }
}
