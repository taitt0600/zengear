namespace ZenGear.Data.Entities.Identity;

/// <summary>Staff-only profile data, 1-1 with <see cref="User"/> (<c>staff_profiles</c>).</summary>
public class StaffProfile
{
    protected StaffProfile()
    {
    }

    internal StaffProfile(string? employeeCode, DateOnly? joinedDate, string? workPhone, bool mustChangePassword,
        Guid? updatedBy, DateTimeOffset now)
    {
        EmployeeCode = employeeCode;
        JoinedDate = joinedDate;
        WorkPhone = workPhone;
        MustChangePassword = mustChangePassword;
        CreatedAt = now;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
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

    /// <summary>
    /// The password was changed through the API (DEC-094): the only way to clear <see cref="MustChangePassword"/>.
    /// Called after Firebase accepted the new password, never before.
    /// </summary>
    public void PasswordChanged(Guid updatedBy, DateTimeOffset now)
    {
        MustChangePassword = false;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
