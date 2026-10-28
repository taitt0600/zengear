namespace ZenGear.Api.Attributes;

/// <summary>
/// The codes of <c>errors</c> an action can answer with <paramref name="status"/> (DEC-117 (2)): the reasons of its
/// validator and service for a 400 (the model-binding codes are added for every 400), the field-bound conflicts for a 409.
/// The OpenAPI document turns them into the enum of that response (<c>OpenApiSetup</c>). The codes repeat the validator's
/// on purpose: building the document must not resolve validators, some of which need Firebase; a test checks that each
/// enum holds every code of its validator.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ErrorCodesAttribute(int status, params string[] codes) : Attribute
{
    public int Status { get; } = status;

    public IReadOnlyList<string> Codes { get; } = codes;
}
