namespace ZenGear.Services.Identity;

public enum AccessBumpKind
{
    /// <summary>Status, profile flag, Firebase uid or roles of one user.</summary>
    User,

    /// <summary>Scope or permissions of one role.</summary>
    Role,

    /// <summary>Every cached entry (after a Redis outage).</summary>
    Epoch,
}

/// <summary>A version of the access cache to increment (R-SEC-07).</summary>
public readonly record struct AccessBump(AccessBumpKind Kind, Guid Id)
{
    public static readonly AccessBump Epoch = new(AccessBumpKind.Epoch, Guid.Empty);

    public static AccessBump User(Guid userId) => new(AccessBumpKind.User, userId);

    public static AccessBump Role(Guid roleId) => new(AccessBumpKind.Role, roleId);
}
