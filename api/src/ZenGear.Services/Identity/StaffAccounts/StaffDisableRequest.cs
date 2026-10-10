namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>Disables a staff account (STF-02).</summary>
/// <param name="LeftDate">
/// Last working day, when the staff member leaves; recorded in the same change (R-USR-07). Not before the joined date
/// and not after today in Asia/Ho_Chi_Minh. Null to disable without recording one (a stored one is kept).
/// </param>
public sealed record StaffDisableRequest(DateOnly? LeftDate);
