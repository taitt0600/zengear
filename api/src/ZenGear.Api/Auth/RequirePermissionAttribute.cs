using Microsoft.AspNetCore.Authorization;

namespace ZenGear.Api.Auth;

/// <summary>
/// The endpoint requires <see cref="Key"/>, granted through any role of the caller (R-SEC-03), on top of the
/// default policy (registered, ACTIVE user). Missing: 403 <c>/problems/permission-denied</c>.
/// The scope (NONE/SELF) is read with <see cref="UserAccessHttpContextExtensions.GetUserAccess"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string key) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Key { get; } = key;

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(Key)];
}
