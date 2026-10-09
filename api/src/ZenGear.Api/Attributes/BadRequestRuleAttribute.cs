namespace ZenGear.Api.Attributes;

/// <summary>
/// The business rule named by the 400 of model binding on this endpoint (<c>?roleId=abc</c>), refused by
/// <c>[ApiController]</c> before the service validator runs; set by <c>InvalidModelStateResponseFactory</c>
/// (<c>ProblemDetailsSetup</c>). Any other 400 keeps the rule it names, or none (docs/API_CONVENTIONS.md, Errors).
/// </summary>
/// <param name="ruleId">The rule, e.g. <c>R-USR-13</c>.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BadRequestRuleAttribute(string ruleId) : Attribute
{
    public string RuleId { get; } = ruleId;
}
