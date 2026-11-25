namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>The generated identifiers of a staff account (R-USR-24): employee code, username and company email.</summary>
public sealed record StaffIdentity(string EmployeeCode, string Username, string Email)
{
    /// <summary>Without the username and the email (personal data), so that a log line never carries them (R-SEC-11).</summary>
    public override string ToString() => $"StaffIdentity {{ EmployeeCode = {EmployeeCode} }}";
}
