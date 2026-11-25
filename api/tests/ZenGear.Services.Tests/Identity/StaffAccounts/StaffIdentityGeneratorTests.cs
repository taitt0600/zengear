using Shouldly;
using ZenGear.Services.Identity.StaffAccounts;

namespace ZenGear.Services.Tests.Identity.StaffAccounts;

/// <summary>The staff identifiers of R-USR-24 (DEC-145): name abbreviation, employee code, username, company email.</summary>
public class StaffIdentityGeneratorTests
{
    [Test]
    public void R_USR_24_the_example_of_the_rule()
    {
        var identity = StaffIdentityGenerator.Generate("Nguyễn Văn An", 2026, 12, "zengear.test");

        identity.EmployeeCode.ShouldBe("ZG260012");
        identity.Username.ShouldBe("AnNVZG260012");
        identity.Email.ShouldBe("annvzg260012@zengear.test");
    }

    [TestCase("Nguyễn Văn An", "AnNV")]
    [TestCase("Trần Minh Khoa", "KhoaTM")]
    [TestCase("Lê Thu Hà", "HaLT")]
    [TestCase("Phạm Quốc Bảo", "BaoPQ")]
    [TestCase("Đỗ Đức Đạt", "DatDD")]
    [TestCase("đỗ đức đạt", "DatDD")]
    [TestCase("ĐỖ ĐỨC ĐẠT", "DatDD")]
    [TestCase("  Võ   Thị\tLan  ", "LanVT")]
    [TestCase("Bùi Văn Tùng", "TungBV")]
    [TestCase("Huỳnh Thị Ngọc Ánh", "AnhHTN")]
    [TestCase("An", "An")]
    [TestCase("Nguyễn Văn An 2", "AnNV")]
    [TestCase("Nguyễn-Văn An", "AnN")]
    [TestCase("O'Brien Kevin", "KevinO")]
    public void R_USR_24_abbreviation_is_the_given_name_then_the_initials_without_diacritics(string name, string expected)
    {
        StaffIdentityGenerator.Abbreviation(name).ShouldBe(expected);
    }

    [TestCase("12345")]
    [TestCase("  ")]
    [TestCase("--- ...")]
    [TestCase("李小龍")]
    public void R_USR_24_a_name_without_a_latin_letter_has_no_abbreviation(string name)
    {
        StaffIdentityGenerator.Abbreviation(name).ShouldBeNull();
    }

    [TestCase(2026, 1, "ZG260001")]
    [TestCase(2026, 9999, "ZG269999")]
    [TestCase(2000, 42, "ZG000042")]
    [TestCase(2099, 7, "ZG990007")]
    public void R_USR_24_employee_code_is_ZG_the_year_and_four_digits(int year, int number, string expected)
    {
        StaffIdentityGenerator.EmployeeCode(year, number).ShouldBe(expected);
    }

    [TestCase(1999)]
    [TestCase(2100)]
    public void R_USR_24_a_year_outside_2000_to_2099_has_no_code(int year)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => StaffIdentityGenerator.EmployeeCode(year, 1));
    }

    [TestCase(0)]
    [TestCase(10000)]
    public void R_USR_24_a_number_outside_1_to_9999_is_a_programming_error(int number)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => StaffIdentityGenerator.EmployeeCode(2026, number));
    }

    [Test]
    public void R_USR_24_email_is_the_lowercase_username_on_the_domain()
    {
        StaffIdentityGenerator.Email("KhoaTMZG260001", "zengear.test").ShouldBe("khoatmzg260001@zengear.test");
    }

    [Test]
    public void R_USR_24_username_length_counts_the_abbreviation_and_the_code()
    {
        StaffIdentityGenerator.UsernameLength("Nguyễn Văn An").ShouldBe(12);
        StaffIdentityGenerator.UsernameLength("12345").ShouldBeNull();
    }
}
