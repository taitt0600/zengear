namespace ZenGear.Api.Auth;

/// <summary>
/// Marks an endpoint open to every registered, ACTIVE user without a permission (for example <c>/me</c>).
/// Metadata only: the default and fallback policies still apply. Every controller action declares its
/// access with this, <see cref="RequirePermissionAttribute"/> or <c>[AllowAnonymous]</c> (AUT-04, checked by a test).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AnyRegisteredUserAttribute : Attribute;
