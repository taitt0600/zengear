using FluentValidation.TestHelper;
using Shouldly;
using ZenGear.Data.Entities.Locations;
using ZenGear.Services.Locations.BusinessLocations;

namespace ZenGear.Services.Tests.Locations.BusinessLocations;

public class BusinessLocationUpdateRequestValidatorTests
{
    private static readonly DateOnly Holiday = new(2027, 2, 6);
    private readonly BusinessLocationUpdateRequestValidator _validator = new();

    private static BusinessLocationUpdateRequest Valid() => new(
        "ZenGear Sài Gòn", "02838123456", "cskh@zengear.test", "123 Lê Lợi", 79, 26734,
        [new RegularHoursPeriodRequest(1, "08:30", "21:30")],
        [new SpecialHoursPeriodRequest(Holiday, true, null, null)]);

    [Test]
    public void A_valid_request_passes() => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Test]
    public void Without_email_or_holidays_passes() =>
        _validator.TestValidate(Valid() with { Email = null, SpecialHours = null }).ShouldNotHaveAnyValidationErrors();

    [TestCase(null)]
    [TestCase(" ")]
    public void Name_is_required(string? name) =>
        _validator.TestValidate(Valid() with { Name = name }).ShouldHaveValidationErrorFor(r => r.Name)
            .WithErrorCode(BusinessLocationUpdateRequestValidator.NameRequired);

    [Test]
    public void Name_is_at_most_150_characters()
    {
        var failure = _validator.TestValidate(Valid() with { Name = new string('a', 151) }).ShouldHaveValidationErrorFor(r => r.Name)
            .WithErrorCode(BusinessLocationUpdateRequestValidator.NameTooLong).ShouldHaveSingleItem();
        failure.FormattedMessagePlaceholderValues["MaxLength"].ShouldBe(BusinessLocationUpdateRequestValidator.NameMaxLength);
    }

    [TestCase("1900636123", true)]
    [TestCase("19006361", true)]
    [TestCase("0912345678", true)]
    [TestCase("1234567", false)]
    [TestCase("028381234567", false)]
    [TestCase("+84912345678", false)]
    [TestCase("0283 812 3456", false)]
    public void Hotline_is_8_to_11_digits(string phone, bool valid)
    {
        var result = _validator.TestValidate(Valid() with { Phone = phone });
        if (valid)
        {
            result.ShouldNotHaveValidationErrorFor(r => r.Phone);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(r => r.Phone).WithErrorCode(BusinessLocationUpdateRequestValidator.PhoneInvalid);
        }
    }

    [Test]
    public void Email_must_be_valid() =>
        _validator.TestValidate(Valid() with { Email = "not-an-email" }).ShouldHaveValidationErrorFor(r => r.Email)
            .WithErrorCode(BusinessLocationUpdateRequestValidator.EmailInvalid);

    [Test]
    public void Address_province_and_ward_are_required()
    {
        var result = _validator.TestValidate(Valid() with { AddressDetail = "", ProvinceId = null, WardId = 0 });
        result.ShouldHaveValidationErrorFor(r => r.AddressDetail)
            .WithErrorCode(BusinessLocationUpdateRequestValidator.AddressDetailRequired);
        result.ShouldHaveValidationErrorFor(r => r.ProvinceId).WithErrorCode(BusinessLocationUpdateRequestValidator.ProvinceIdRequired);
        result.ShouldHaveValidationErrorFor(r => r.WardId).WithErrorCode(BusinessLocationUpdateRequestValidator.WardIdInvalid);
    }

    [Test]
    public void Each_weekly_period_is_checked_on_its_own_path()
    {
        var result = _validator.TestValidate(Valid() with
        {
            RegularHours = [new(8, "8h", "21:00"), new(2, "18:00", "08:00"), new(3, "24:00", "25:00")],
        });

        result.ShouldHaveValidationErrorFor("RegularHours[0].Day").WithErrorCode(BusinessLocation.DayOutOfRange);
        result.ShouldHaveValidationErrorFor("RegularHours[0].Open").WithErrorCode(BusinessLocation.OpenTimeInvalid);
        result.ShouldHaveValidationErrorFor("RegularHours[1]").WithErrorCode(BusinessLocation.PeriodOpensAfterClosing);
        result.ShouldHaveValidationErrorFor("RegularHours[2].Open").WithErrorCode(BusinessLocation.OpenTimeInvalid);
        result.ShouldHaveValidationErrorFor("RegularHours[2].Close").WithErrorCode(BusinessLocationUpdateRequestValidator.CloseTimeInvalid);
    }

    [Test]
    public void A_null_period_or_holiday_is_a_validation_error()
    {
        var result = _validator.TestValidate(Valid() with { RegularHours = [null!], SpecialHours = [null!] });

        result.ShouldHaveValidationErrorFor("RegularHours[0]").WithErrorCode(BusinessLocationUpdateRequestValidator.PeriodRequired);
        result.ShouldHaveValidationErrorFor("SpecialHours[0]").WithErrorCode(BusinessLocationUpdateRequestValidator.PeriodRequired);
    }

    [Test]
    public void R_LOC_01_weekly_hours_need_a_period_and_no_overlap()
    {
        _validator.TestValidate(Valid() with { RegularHours = [] }).ShouldHaveValidationErrorFor(r => r.RegularHours)
            .WithErrorCode(BusinessLocation.RegularHoursEmpty);
        _validator.TestValidate(Valid() with { RegularHours = null }).ShouldHaveValidationErrorFor(r => r.RegularHours)
            .WithErrorCode(BusinessLocationUpdateRequestValidator.RegularHoursRequired);
        _validator.TestValidate(Valid() with { RegularHours = [new(1, "08:00", "12:00"), new(1, "11:00", "14:00")] })
            .ShouldHaveValidationErrorFor(r => r.RegularHours).WithErrorCode(BusinessLocation.PeriodsOverlap);
    }

    [Test]
    public void R_LOC_01_a_closed_holiday_with_hours_names_the_rule()
    {
        var result = _validator.TestValidate(Valid() with
        {
            SpecialHours = [new(Holiday, true, null, null), new(Holiday, false, "08:00", "12:00")],
        });

        result.ShouldHaveValidationErrorFor(r => r.SpecialHours).WithErrorCode(BusinessLocation.ClosedDateWithHours)
            .WithCustomState(BusinessLocationUpdateRequestValidator.ClosedDateRuleId);
    }

    [Test]
    public void Too_many_periods_a_day_gives_its_limit_to_the_text()
    {
        var result = _validator.TestValidate(Valid() with
        {
            RegularHours =
            [
                new(3, "06:00", "07:00"), new(3, "08:00", "09:00"), new(3, "10:00", "11:00"), new(3, "12:00", "13:00"),
            ],
        });

        var failure = result.ShouldHaveValidationErrorFor(r => r.RegularHours)
            .WithErrorCode(BusinessLocation.PeriodsPerDayTooMany).ShouldHaveSingleItem();
        failure.CustomState.ShouldBeNull();
        failure.FormattedMessagePlaceholderValues["MaxCount"].ShouldBe(BusinessLocation.MaxPeriodsPerDay);
    }

    [Test]
    public void An_open_holiday_needs_both_times()
    {
        _validator.TestValidate(Valid() with { SpecialHours = [new(Holiday, false, null, "12:00")] })
            .ShouldHaveValidationErrorFor("SpecialHours[0].Open").WithErrorCode(BusinessLocation.OpenTimeInvalid);
        _validator.TestValidate(Valid() with { SpecialHours = [new(null, true, null, null)] })
            .ShouldHaveValidationErrorFor("SpecialHours[0].Date").WithErrorCode(BusinessLocationUpdateRequestValidator.DateRequired);
        _validator.TestValidate(Valid() with { SpecialHours = [new(Holiday, null, null, null)] })
            .ShouldHaveValidationErrorFor("SpecialHours[0].Closed").WithErrorCode(BusinessLocationUpdateRequestValidator.ClosedRequired);
    }

    [Test]
    public void At_most_100_holiday_entries() =>
        _validator.TestValidate(Valid() with
        {
            SpecialHours = [.. Enumerable.Range(0, 101).Select(i => new SpecialHoursPeriodRequest(Holiday.AddDays(i), true, null, null))],
        }).ShouldHaveValidationErrorFor(r => r.SpecialHours).WithErrorCode(BusinessLocationUpdateRequestValidator.SpecialHoursTooMany);
}
