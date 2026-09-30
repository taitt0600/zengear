namespace ZenGear.Services.Identity.AccessCache;

public enum AccessBumpKind
{
    /// <summary>Status, profile flag, Firebase uid or roles of one user.</summary>
    User,

    /// <summary>Scope or permissions of one role.</summary>
    Role,

    /// <summary>Every cached entry (after a Redis outage).</summary>
    Epoch,
}
