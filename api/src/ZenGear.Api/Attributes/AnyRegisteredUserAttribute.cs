namespace ZenGear.Api.Attributes;

/// <summary>
/// Marks an endpoint open to every registered, ACTIVE user without a permission (for example <c>/me</c>).
/// Metadata only: the default and fallback policies still apply. Every controller action declares its
/// access with this or <see cref="RequirePermissionAttribute"/>, or its controller with <see cref="ShopEndpointAttribute"/>
/// (AUT-04, checked by a test).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AnyRegisteredUserAttribute : Attribute;
