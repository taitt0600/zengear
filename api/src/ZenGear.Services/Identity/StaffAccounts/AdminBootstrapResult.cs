namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>
/// The first Admin created by the <c>bootstrap-admin</c> command (DEC-096). Returned to that host command only, never
/// to a controller.
/// </summary>
/// <param name="PasswordResetLink">A credential, printed to the operator's terminal only; null when Firebase could not
/// generate it after the Admin was created.</param>
public sealed record AdminBootstrapResult(Guid UserId, string? PasswordResetLink)
{
    /// <summary>Without the link, so that a log line or an exception message never carries it.</summary>
    public override string ToString() => $"AdminBootstrapResult {{ UserId = {UserId} }}";
}
