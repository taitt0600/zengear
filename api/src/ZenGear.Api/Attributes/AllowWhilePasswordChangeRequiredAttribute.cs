namespace ZenGear.Api.Attributes;

/// <summary>The endpoint stays usable by staff who must change their password first (R-USR-03).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWhilePasswordChangeRequiredAttribute : Attribute;
