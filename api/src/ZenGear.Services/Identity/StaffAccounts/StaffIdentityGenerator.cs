using System.Globalization;
using System.Text;

namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// The staff identifiers of R-USR-24 (DEC-145), computed from the full name, the year of the joined date and the number
/// that <see cref="StaffIdentityIssuer"/> takes from the yearly counter. Pure: no database, no clock.
/// </summary>
public static class StaffIdentityGenerator
{
    /// <summary>"ZG" + 2-digit year + 4-digit number.</summary>
    public const int EmployeeCodeLength = 8;

    public const int MaxNumber = 9999;

    /// <summary>
    /// The years of the joined date the code names without ambiguity (DEC-156 (4)): the code keeps 2 digits of the year
    /// while <c>employee_code_sequences</c> counts per full year, so two years 100 apart would hand out the same codes.
    /// </summary>
    public const int MinYear = 2000;

    public const int MaxYear = 2099;

    /// <summary>
    /// <paramref name="name"/> → ZG + <paramref name="year"/> + <paramref name="number"/>, the username and the email.
    /// </summary>
    /// <exception cref="ArgumentException">The name holds no Latin letter (the validator refuses such a name first).</exception>
    public static StaffIdentity Generate(string name, int year, int number, string emailDomain)
    {
        var abbreviation = Abbreviation(name)
            ?? throw new ArgumentException("R-USR-24: the name holds no letter to build a username from.", nameof(name));
        var code = EmployeeCode(year, number);
        var username = abbreviation + code;
        return new StaffIdentity(code, username, Email(username, emailDomain));
    }

    /// <summary>
    /// The name abbreviation (Tên viết tắt): the given name (the last word) with its first letter in upper case and the
    /// rest in lower case, then the upper-case first letter of each other word in order; Vietnamese diacritics removed
    /// (đ → d) and every character that is not a Latin letter dropped; a word left empty is skipped. Null when no letter
    /// is left.
    /// </summary>
    public static string? Abbreviation(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(LatinLetters)
            .Where(w => w.Length > 0)
            .ToList();
        if (words.Count == 0)
        {
            return null;
        }

        var given = words[^1];
        var builder = new StringBuilder(char.ToUpperInvariant(given[0]).ToString())
            .Append(given[1..].ToLowerInvariant());
        foreach (var word in words.Take(words.Count - 1))
        {
            builder.Append(char.ToUpperInvariant(word[0]));
        }

        return builder.ToString();
    }

    /// <summary>The length of the username <paramref name="name"/> gives; null when it gives none.</summary>
    public static int? UsernameLength(string name) => Abbreviation(name)?.Length + EmployeeCodeLength;

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="year"/> is not within 2000–2099 or
    /// <paramref name="number"/> not within 1–9999.</exception>
    public static string EmployeeCode(int year, int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, MinYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, MaxYear);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxNumber);
        return string.Create(CultureInfo.InvariantCulture, $"ZG{year % 100:00}{number:0000}");
    }

    /// <summary>The company email: the username in lower case on the configured domain.</summary>
    public static string Email(string username, string emailDomain) =>
        $"{username.ToLowerInvariant()}@{emailDomain.ToLowerInvariant()}";

    private static string LatinLetters(string word)
    {
        var builder = new StringBuilder(word.Length);
        foreach (var c in word.Normalize(NormalizationForm.FormD))
        {
            var letter = c switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => c,
            };
            if (letter is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
            {
                builder.Append(letter);
            }
        }

        return builder.ToString();
    }
}
