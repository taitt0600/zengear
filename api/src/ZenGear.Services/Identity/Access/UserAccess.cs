using System.Collections.ObjectModel;
using ZenGear.Data.Entities.Identity.Enums;

namespace ZenGear.Services.Identity.Access;

/// <summary>What the API needs about the caller on every request.</summary>
/// <param name="LastLoginAt"><c>auth_time</c> of the last recorded sign-in.</param>
/// <param name="MustChangePassword">Staff only; false for a customer.</param>
public sealed record UserAccess(Guid Id, UserStatus Status, DateTimeOffset? LastLoginAt, bool MustChangePassword)
{
    private static readonly IReadOnlyDictionary<string, ScopingType> NoPermissions =
        ReadOnlyDictionary<string, ScopingType>.Empty;

    /// <summary>
    /// Permission keys granted through any of the user's roles, with the widest scope among those roles
    /// (R-SEC-03); built by <see cref="MergeRoles"/>.
    /// </summary>
    public IReadOnlyDictionary<string, ScopingType> Permissions { get; init; } = NoPermissions;

    /// <summary>Only an ACTIVE user may sign in and use the system (R-SEC-04).</summary>
    public bool IsActive => Status == UserStatus.Active;

    /// <summary>A token whose <c>auth_time</c> is later than <c>last_login_at</c> starts a new session (R-SEC-11).</summary>
    public bool IsNewSignIn(DateTimeOffset authTime) => LastLoginAt is null || authTime > LastLoginAt;

    /// <summary>Scope of <paramref name="permissionKey"/>; null when no role grants it (R-SEC-03).</summary>
    public ScopingType? ScopeOf(string permissionKey) =>
        Permissions.TryGetValue(permissionKey, out var scope) ? scope : null;

    public bool HasPermission(string permissionKey) => Permissions.ContainsKey(permissionKey);

    /// <summary>
    /// Union of the permissions of <paramref name="roles"/>. A key granted by several roles gets the
    /// widest scope: NONE (all data) wins over SELF (own data).
    /// </summary>
    public static IReadOnlyDictionary<string, ScopingType> MergeRoles(
        IEnumerable<(ScopingType Scope, IEnumerable<string> PermissionKeys)> roles)
    {
        var merged = new Dictionary<string, ScopingType>(StringComparer.Ordinal);
        foreach (var (scope, keys) in roles)
        {
            foreach (var key in keys)
            {
                if (!merged.ContainsKey(key) || scope == ScopingType.None)
                {
                    merged[key] = scope;
                }
            }
        }

        return merged;
    }
}
