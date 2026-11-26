namespace ZenGear.Data.Entities.Identity;

/// <summary>Staff-only profile data, 1-1 with <see cref="User"/> (<c>staff_profiles</c>).</summary>
public class StaffProfile
{
    protected StaffProfile()
    {
    }

    internal StaffProfile(string employeeCode, string username, DateOnly? joinedDate, string? workPhone,
        bool mustChangePassword, Guid? createdBy, DateTimeOffset now)
    {
        EmployeeCode = employeeCode;
        Username = username;
        JoinedDate = joinedDate;
        WorkPhone = workPhone;
        MustChangePassword = mustChangePassword;
        CreatedBy = createdBy;
        CreatedAt = now;
        UpdatedBy = createdBy;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }

    /// <summary>
    /// ZG + year of the joined date + yearly number (R-USR-24); never changes. Null only once the retention job of R-USR-19
    /// and R-USR-23 has erased it (DEC-156 (3)).
    /// </summary>
    public string? EmployeeCode { get; private set; }

    /// <summary>Name abbreviation + employee code (R-USR-24), unique whatever its case; the email is it in lower case.</summary>
    public string Username { get; private set; } = null!;

    public DateOnly? JoinedDate { get; private set; }

    public string? WorkPhone { get; private set; }

    /// <summary>Last working day; null = still employed.</summary>
    public DateOnly? LeftDate { get; private set; }

    /// <summary>The staff member must change the password before using anything else (R-USR-03).</summary>
    public bool MustChangePassword { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Who created the account through the API: the person who may not approve or reject it, and the only one who may
    /// withdraw it while it is PENDING (R-USR-17, DEC-128 (3), DEC-140 (1)). Null for the bootstrap and seed accounts.
    /// It is the same actor as <see cref="User.CreatedBy"/>, kept here because the rule reads the profile (DEC-128).
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Records the last working day (R-USR-07); called by <see cref="User.Disable"/> only.</summary>
    /// <exception cref="ArgumentException"><paramref name="leftDate"/> is before <see cref="JoinedDate"/>.</exception>
    internal void RecordLeftDate(DateOnly leftDate, Guid updatedBy, DateTimeOffset now)
    {
        if (leftDate < JoinedDate)
        {
            throw new ArgumentException("R-USR-07: the left date cannot be before the joined date.", nameof(leftDate));
        }

        LeftDate = leftDate;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>The staff member is employed again (<c>left_date</c> NULL); called by <see cref="User.Restore"/> only.</summary>
    internal void ClearLeftDate(Guid updatedBy, DateTimeOffset now)
    {
        if (LeftDate is null)
        {
            return;
        }

        LeftDate = null;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

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
