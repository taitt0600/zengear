namespace ZenGear.Services.Common;

/// <summary>
/// A business rule or state conflict found by a service. The API returns it as Problem Details with status 409,
/// <c>type</c> <c>/problems/{ProblemType}</c>, <c>ruleId</c>, and the title and detail of that type in the language of
/// the request (DEC-117 (3)): the exception carries no text. A conflict about one field of the request also names that
/// field, so the API puts it under the field (DEC-117 (1)).
/// </summary>
/// <param name="ruleId">Violated rule, e.g. <c>R-INV-02</c>; null for a state conflict without a rule.</param>
/// <param name="problemType">Kebab-case slug, e.g. <c>stock-not-available</c>; its texts are in the API's resources.</param>
/// <param name="field">The request property the conflict is about (<c>nameof(StaffCreateRequest.Email)</c>), or null.</param>
public class BusinessRuleException(string? ruleId, string problemType, string? field = null)
    : Exception(problemType)
{
    public string? RuleId { get; } = ruleId;

    public string ProblemType { get; } = problemType;

    public string? Field { get; } = field;
}
