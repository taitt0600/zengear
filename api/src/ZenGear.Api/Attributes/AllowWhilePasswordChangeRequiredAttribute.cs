namespace ZenGear.Api.Attributes;

/// <summary>
/// The endpoint stays usable by staff who must change their password first (R-USR-03);
/// every other protected endpoint answers 403 <c>/problems/password-change-required</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWhilePasswordChangeRequiredAttribute : Attribute;
