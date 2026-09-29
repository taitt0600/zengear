namespace ZenGear.Services.Common;

/// <summary>
/// A business rule or state conflict found by a service. The API returns it as Problem Details
/// with status 409, <c>type</c> <c>/problems/{ProblemType}</c> and <c>ruleId</c> (docs/API_CONVENTIONS.md).
/// </summary>
/// <param name="ruleId">Violated rule, e.g. <c>R-INV-02</c>; null for a state conflict without a rule.</param>
/// <param name="problemType">Kebab-case slug, e.g. <c>stock-not-available</c>.</param>
/// <param name="title">Short summary that does not change between occurrences.</param>
/// <param name="detail">Explanation of this occurrence; never contains secrets.</param>
public class BusinessRuleException(string? ruleId, string problemType, string title, string detail)
    : Exception(detail)
{
    public string? RuleId { get; } = ruleId;

    public string ProblemType { get; } = problemType;

    public string Title { get; } = title;

    public string Detail { get; } = detail;
}
