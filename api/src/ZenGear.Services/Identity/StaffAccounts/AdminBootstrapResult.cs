namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// The first Admin created by the <c>bootstrap-admin</c> command (DEC-096, DEC-156). Returned to that host command only,
/// never to a controller.
/// </summary>
/// <param name="EmployeeCode">The generated employee code (R-USR-24), printed to the operator's terminal.</param>
/// <param name="Username">The generated sign-in name (R-USR-24), printed to the operator's terminal only, never logged.</param>
/// <param name="Email">The generated company email (R-USR-24), the sign-in identity; printed to the terminal only, never
/// logged.</param>
/// <param name="PasswordResetLink">A credential, printed to the operator's terminal only; null when Firebase could not
/// generate it after the Admin was created.</param>
public sealed record AdminBootstrapResult(Guid UserId, string EmployeeCode, string Username, string Email, string? PasswordResetLink)
{
    /// <summary>Without the username, the email and the link, so that a log line or an exception message never carries them.</summary>
    public override string ToString() => $"AdminBootstrapResult {{ UserId = {UserId}, EmployeeCode = {EmployeeCode} }}";
}
