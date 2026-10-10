namespace ZenGear.Api.Attributes;

/// <summary>
/// Marks an endpoint open to every registered, ACTIVE user without a permission (for example <c>/me</c>).
/// Metadata only: the default and fallback policies still apply.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AnyRegisteredUserAttribute : Attribute;
