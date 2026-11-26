namespace ZenGear.Services.Identity.StaffAccounts;

/// <summary>Who created a PENDING staff account (R-USR-17, DEC-137 (1)).</summary>
public sealed record StaffCreatorDto(Guid Id, string Name);
