


namespace ZenGear.Services.Identity.Access.AccessCache;

/// <summary>A version of the access cache to increment (R-SEC-07).</summary>
public readonly record struct AccessBump(AccessBumpKind Kind, Guid Id)
{
    public static readonly AccessBump Epoch = new(AccessBumpKind.Epoch, Guid.Empty);

    public static AccessBump User(Guid userId) => new(AccessBumpKind.User, userId);

    public static AccessBump Role(Guid roleId) => new(AccessBumpKind.Role, roleId);
}
