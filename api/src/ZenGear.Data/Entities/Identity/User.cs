using ZenGear.Data.Entities.Common;

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
}
