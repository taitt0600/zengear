namespace ZenGear.Data.Entities.Identity;

/// <summary>What a new staff account is created with (<see cref="User.CreateStaff"/>).</summary>
/// <param name="FirebaseUid">The Firebase Authentication account it signs in with.</param>
/// <param name="EmployeeCode">The generated employee code (R-USR-24).</param>
/// <param name="Username">The generated username (R-USR-24); <paramref name="Email"/> is it in lower case on the domain.</param>
/// <param name="MustChangePassword">True for an account created with the default password (R-USR-03, DEC-094).</param>
public sealed record StaffAccountData(
    string FirebaseUid,
    string Name,
    string Email,
    string EmployeeCode,
    string Username,
    DateOnly JoinedDate,
    string? WorkPhone,
    bool MustChangePassword);
