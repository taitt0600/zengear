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

    /// <summary>
    /// <c>xmin</c>, the concurrency token and ETag. For a staff account it is also the version of DEC-140 (2) that
    /// approve, reject and withdraw compare with <c>If-Match</c>: any change of the name, employee code, work phone,
    /// joined date, email or role set must write this row too (a change of the profile or of the roles alone sets
    /// <see cref="UpdatedAt"/>), so that an approver never approves data other than what they saw.
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>Email as stored and compared: trimmed and lowercase (R-USR-02, <c>ck_users_email</c>).</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// A new ACTIVE staff account with its Firebase account, its one staff profile and its roles (R-USR-01), created by
    /// a holder of <c>staff.approve</c> or by the bootstrap. The services check the rules first for a clear error; these
    /// checks are the backstop.
    /// </summary>
    /// <param name="roles">At least one (R-SEC-01: the database trigger only guards removals); a role given twice
    /// counts once.</param>
    /// <exception cref="ArgumentException">No Firebase account, no role, or a role that is not for staff.</exception>
    public static User CreateStaff(StaffAccountData account, IReadOnlyCollection<Role> roles, Guid? createdBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.FirebaseUid is null)
        {
            throw new ArgumentException("An ACTIVE staff account signs in with a Firebase account.", nameof(account));
        }

        return NewStaff(account, roles, UserStatus.Active, createdBy, now);
    }

    /// <summary>
    /// A new PENDING staff account, created by someone without <c>staff.approve</c>: no Firebase account until another
    /// person approves it (R-USR-17, DEC-128 (3)); otherwise as <see cref="CreateStaff"/>.
    /// </summary>
    /// <exception cref="ArgumentException">A Firebase account, no role, or a role that is not for staff.</exception>
    public static User CreatePendingStaff(StaffAccountData account, IReadOnlyCollection<Role> roles, Guid createdBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.FirebaseUid is not null)
        {
            throw new ArgumentException("R-USR-17: a PENDING account has no Firebase account.", nameof(account));
        }

        return NewStaff(account, roles, UserStatus.Pending, createdBy, now);
    }

    private static User NewStaff(StaffAccountData account, IReadOnlyCollection<Role> roles, UserStatus status,
        Guid? createdBy, DateTimeOffset now)
    {
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
            Status = status,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedBy = createdBy,
            UpdatedAt = now,
        };
        user.StaffProfile = new StaffProfile(account.EmployeeCode, account.Username, account.JoinedDate, account.WorkPhone,
            account.MustChangePassword, createdBy, now);
        foreach (var role in roles.DistinctBy(r => r.Id))
        {
            user.UserRoles.Add(new UserRole(role.Id, createdBy, now));
        }

        return user;
    }

    /// <summary>
    /// Approves the PENDING staff account: ACTIVE, signing in with the Firebase account the approval created
    /// (R-USR-17, R-USR-03); <c>must_change_password</c> stays as created. The service checks the rules first for a
    /// clear error; this check is the backstop.
    /// </summary>
    /// <exception cref="InvalidOperationException">The account is not PENDING.</exception>
    public void Approve(string firebaseUid, Guid approvedBy, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firebaseUid);
        if (Status != UserStatus.Pending)
        {
            throw new InvalidOperationException("R-USR-17: only a PENDING account is approved.");
        }

        FirebaseUid = firebaseUid;
        Status = UserStatus.Active;
        UpdatedBy = approvedBy;
        UpdatedAt = now;
    }

    /// <summary>
    /// Disables the staff account and, when given, records the last working day in the same change (R-USR-07). A
    /// DISABLED account stays DISABLED: a later call with a new left date records it and stamps the account; one that
    /// changes nothing writes nothing. The service checks the rules first for a clear error; these checks are the
    /// backstop.
    /// </summary>
    /// <exception cref="InvalidOperationException">ANONYMIZED is final (R-USR-03), or the user is not staff.</exception>
    /// <exception cref="ArgumentException"><paramref name="leftDate"/> is before the joined date (R-USR-07).</exception>
    public void Disable(DateOnly? leftDate, Guid updatedBy, DateTimeOffset now)
    {
        var profile = StaffProfileForStatusChange();
        var newLeftDate = leftDate is { } date && date != profile.LeftDate;

        // A repeat that changes nothing writes nothing: a retry after a failed revoke keeps the stamps and the version.
        if (Status == UserStatus.Disabled && !newLeftDate)
        {
            return;
        }

        if (newLeftDate)
        {
            profile.RecordLeftDate(leftDate!.Value, updatedBy, now);
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

        if (Status == UserStatus.Pending)
        {
            throw new InvalidOperationException("R-USR-17: a PENDING account is approved, rejected or withdrawn, not disabled or restored.");
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
