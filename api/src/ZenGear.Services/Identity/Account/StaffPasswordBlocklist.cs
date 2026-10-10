using System.Collections.Frozen;

namespace ZenGear.Services.Identity.Account;

/// <summary>
/// Common passwords a staff member may not choose (R-USR-11, DEC-102), compared without regard to case. Picked by hand
/// from the patterns that top public lists of leaked passwords (digit runs, keyboard walks, "password" or "admin"
/// with digits, one character repeated) plus Vietnamese ones; every entry has at least
/// <see cref="CurrentUserPasswordChangeRequestValidator.MinLength"/> characters, since shorter ones are refused by
/// length anyway. Changed by a commit.
/// </summary>
public static class StaffPasswordBlocklist
{
    private static readonly FrozenSet<string> Entries = new[]
    {
        // Digit runs and repeats.
        "123456789012", "1234567890123", "12345678901234", "123456789123", "123123123123", "121212121212",
        "111111111111", "000000000000", "112233445566", "098765432109", "987654321098", "147258369147",
        "123456123456", "123456654321", "012345678910",
        // Keyboard walks.
        "qwertyuiop12", "qwertyuiop123", "qwerty123456", "qwertyqwerty", "qwertyuiopas", "asdfghjkl123",
        "zxcvbnm12345", "1qaz2wsx3edc", "1q2w3e4r5t6y", "q1w2e3r4t5y6", "zaq12wsxcde3",
        "!qaz2wsx3edc", "qazwsxedcrfv", "1qazxsw23edc", "1q2w3e4r5t6y7u8i",
        // Words with digits.
        "password1234", "password12345", "password123456", "passwordpassword", "password@123", "p@ssw0rd1234",
        "passw0rd1234", "admin1234567", "admin@123456", "administrator", "administrator1", "changeme1234",
        "welcome12345", "welcome@1234", "letmein12345", "iloveyou1234", "abc123456789", "abcd12345678",
        "abcdefghijkl", "abcdefgh1234", "aaaaaaaaaaaa", "football1234", "baseball1234", "superman1234",
        "starwars1234", "princess1234", "sunshine1234", "trustno11234", "computer1234", "internet1234",
        // Public common passwords, not a secret: gitleaks reads the next line as an API key (owner-approved exception).
        "1234password", "123456abcdef", "qwerty@12345", // gitleaks:allow
        // Vietnamese.
        "matkhau12345", "matkhau123456", "matkhaumoi123", "matkhaumoi1234", "anhyeuem1234", "anhyeuem123456",
        "emyeuanh1234", "emyeuanh123456", "iloveyou123456", "vietnam12345", "vietnam123456", "hanoi1234567",
        "saigon123456", "nhanvien1234", "nhanvien123456", "cuahang12345", "banhang12345",
        "doimatkhau123", "doimatkhau1234",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool Contains(string password) => Entries.Contains(password);
}
