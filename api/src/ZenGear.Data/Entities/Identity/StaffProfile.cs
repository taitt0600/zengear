namespace ZenGear.Data.Entities.Identity;

/// <summary>Staff-only profile data, 1-1 with <see cref="User"/> (<c>staff_profiles</c>).</summary>
public class StaffProfile
{
    private StaffProfile()
    {
    }

    public Guid UserId { get; private set; }

    public string? EmployeeCode { get; private set; }

    public DateOnly? JoinedDate { get; private set; }

    public string? WorkPhone { get; private set; }

    /// <summary>Last working day; null = still employed.</summary>
    public DateOnly? LeftDate { get; private set; }

    /// <summary>The staff member must change the password before using anything else (R-USR-03).</summary>
    public bool MustChangePassword { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
