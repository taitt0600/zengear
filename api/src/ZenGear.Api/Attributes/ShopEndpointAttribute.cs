using Microsoft.AspNetCore.Authorization;

namespace ZenGear.Api.Attributes;

/// <summary>
/// Marks a <c>*ShopController</c>: every action is anonymous. Being class-level, it wins over any access declared on
/// an action, so a shop endpoint for a signed-in customer needs a Decision that changes this attribute first.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ShopEndpointAttribute : Attribute, IAllowAnonymous;
