namespace ZenGear.Services.Identity.Models;

/// <summary>Disables a staff account (STF-02).</summary>
/// <param name="LeftDate">
/// Last working day, when the staff member leaves; recorded in the same change (R-USR-07). Not before the joined date.
/// Null to disable without recording one (a stored one is kept).
/// </param>
public sealed record StaffDisableRequest(DateOnly? LeftDate);
