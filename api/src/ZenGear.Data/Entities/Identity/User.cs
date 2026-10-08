using ZenGear.Data.Common;
using ZenGear.Data.Entities.Identity.Enums;




namespace ZenGear.Data.Entities.Identity;

/// <summary>Login account shared by staff and customers (<c>users</c>).</summary>
public class User : IAggregateRoot
{
    protected User()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Firebase Authentication uid; null for a placeholder customer created by staff.</summary>
    public string? FirebaseUid { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Lowercase; null for a customer identified by phone only.</summary>
    public string? Email { get; private set; }

    /// <summary>E.164.</summary>
    public string? Phone { get; private set; }

    public DateTimeOffset? PhoneVerifiedAt { get; private set; }

    public UserStatus Status { get; private set; }

    /// <summary><c>auth_time</c> of the last sign-in; updated once per session (R-SEC-11).</summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public StaffProfile? StaffProfile { get; private set; }

    public ICollection<UserRole> UserRoles { get; private set; } = [];

    public uint Version { get; private set; }

    /// <summary>Email as stored and compared: trimmed and lowercase (R-USR-02, <c>ck_users_email</c>).</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// A new ACTIVE staff account with its one staff profile and its roles: the only way to create a staff user
    /// (R-USR-01). The services check the rules first for a clear error; these checks are the backstop.
    /// </summary>
    /// <param name="roles">At least one (R-SEC-01: the database trigger only guards removals); a role given twice
    /// counts once.</param>
    /// <exception cref="ArgumentException">No role, or a role that is not for staff.</exception>
    public static User CreateStaff(StaffAccountData account, IReadOnlyCollection<Role> roles, Guid? createdBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0)
        {
            throw new ArgumentException("R-SEC-01: a user has at least one role.", nameof(roles));
        }

        // R-USR-01: a staff account holds internal roles only; the SELF-scoped role is the Customer's (DEC-002). A
        // STORE scope for staff roles (LOC-03) will need this test revisited: until then it refuses, never grants.
        if (roles.Any(r => r.ScopingType != ScopingType.None))
        {
            throw new ArgumentException("R-USR-01: a staff account cannot hold a customer role.", nameof(roles));
        }

        var user = new User
        {
            FirebaseUid = account.FirebaseUid,
            Name = account.Name.Trim(),
            Email = NormalizeEmail(account.Email),
            Status = UserStatus.Active,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedBy = createdBy,
            UpdatedAt = now,
        };
        user.StaffProfile = new StaffProfile(account.EmployeeCode, account.JoinedDate, account.WorkPhone,
            account.MustChangePassword, createdBy, now);
        foreach (var role in roles.DistinctBy(r => r.Id))
        {
            user.UserRoles.Add(new UserRole(role.Id, createdBy, now));
        }

        return user;
    }

    /// <summary>
    /// Disables the staff account and, when given, records the last working day in the same change (R-USR-07). A
    /// DISABLED account stays DISABLED: a later call records a new left date on the staff profile and otherwise changes
    /// nothing. The
    /// service checks the rules first for a clear error; these checks are the backstop.
    /// </summary>
    /// <exception cref="InvalidOperationException">ANONYMIZED is final (R-USR-03), or the user is not staff.</exception>
    /// <exception cref="ArgumentException"><paramref name="leftDate"/> is before the joined date (R-USR-07).</exception>
    public void Disable(DateOnly? leftDate, Guid updatedBy, DateTimeOffset now)
    {
        var profile = StaffProfileForStatusChange();
        if (leftDate is { } date && date != profile.LeftDate)
        {
            profile.RecordLeftDate(date, updatedBy, now);
        }

        // A repeat that changes nothing writes nothing: a retry after a failed revoke keeps the stamps and the version.
        if (Status == UserStatus.Disabled)
        {
            return;
        }

        Status = UserStatus.Disabled;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    /// <summary>
    /// Restores a DISABLED staff account to ACTIVE and clears its left date: the staff member is employed again
    /// (R-USR-03). An ACTIVE account is left unchanged.
    /// </summary>
    /// <exception cref="InvalidOperationException">ANONYMIZED is final (R-USR-03), or the user is not staff.</exception>
    public void Restore(Guid updatedBy, DateTimeOffset now)
    {
        var profile = StaffProfileForStatusChange();
        if (Status == UserStatus.Active)
        {
            return;
        }

        profile.ClearLeftDate(updatedBy, now);
        Status = UserStatus.Active;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    private StaffProfile StaffProfileForStatusChange()
    {
        if (Status == UserStatus.Anonymized)
        {
            throw new InvalidOperationException("R-USR-03: an anonymized account cannot change status.");
        }

        return StaffProfile ?? throw new InvalidOperationException("Only a staff account is disabled or restored here.");
    }

    /// <summary>Changes the display name, trimmed (R-USR-04); stamps the change only when the name differs.</summary>
    /// <exception cref="ArgumentException">A blank name; the validator refuses it first.</exception>
    public void Rename(string name, Guid updatedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("R-USR-04: the display name cannot be blank.", nameof(name));
        }

        if (trimmed == Name)
        {
            return;
        }

        Name = trimmed;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
