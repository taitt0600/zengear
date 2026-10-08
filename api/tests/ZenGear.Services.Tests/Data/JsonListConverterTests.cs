using Shouldly;
using ZenGear.Data.Common;
using ZenGear.Data.Entities.Inventory;

namespace ZenGear.Services.Tests.Data;

/// <summary>The opening hours are stored in the shape of docs/DATABASE.md (<c>business_locations</c>).</summary>
public class JsonListConverterTests
{
    [Test]
    public void R_LOC_01_weekly_hours_are_stored_as_day_open_close()
    {
        var converter = new JsonListConverter<RegularHoursPeriod>();

        var json = converter.ConvertToProviderExpression.Compile()(new[] { new RegularHoursPeriod(1, new(8, 30), new(21, 30)) });

        json.ShouldBe("""[{"day":1,"open":"08:30","close":"21:30"}]""");
        converter.ConvertFromProviderExpression.Compile()(json).ShouldBe(new[] { new RegularHoursPeriod(1, new(8, 30), new(21, 30)) });
    }

    [Test]
    public void R_LOC_01_holiday_hours_are_stored_as_date_closed_and_optional_times()
    {
        var converter = new JsonListConverter<SpecialHoursPeriod>();
        SpecialHoursPeriod[] hours =
        [
            new(new DateOnly(2027, 2, 6), true, null, null),
            new(new DateOnly(2027, 2, 10), false, new(10, 0), new(18, 0)),
        ];

        var json = converter.ConvertToProviderExpression.Compile()(hours);

        json.ShouldBe("""[{"date":"2027-02-06","closed":true},{"date":"2027-02-10","closed":false,"open":"10:00","close":"18:00"}]""");
        converter.ConvertFromProviderExpression.Compile()(json).ShouldBe(hours);
    }

    [Test]
    public void A_time_filled_by_hand_still_loads()
    {
        var converter = new JsonListConverter<RegularHoursPeriod>();

        converter.ConvertFromProviderExpression.Compile()("""[{"day": 1, "open": "8:30", "close": "21:30:45"}]""")
            .ShouldBe(new[] { new RegularHoursPeriod(1, new(8, 30), new(21, 30)) });
    }

    [Test]
    public void Comparer_sees_a_changed_element()
    {
        var comparer = new JsonListConverter<RegularHoursPeriod>().Comparer;
        IReadOnlyList<RegularHoursPeriod> a = [new(1, new(8, 0), new(12, 0))];

        comparer.Equals(a, [new(1, new(8, 0), new(12, 0))]).ShouldBeTrue();
        comparer.Equals(a, [new(1, new(8, 0), new(13, 0))]).ShouldBeFalse();
        comparer.Equals(a, null).ShouldBeFalse();
    }
}
