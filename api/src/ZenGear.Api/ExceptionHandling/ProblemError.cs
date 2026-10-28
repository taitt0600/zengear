using System.Text.Json.Serialization;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// One item of <c>errors</c> (DEC-117 (1)): where it is, as a JSON Pointer to a field of the body (<c>#/newPassword</c>)
/// or the name of a query or route parameter (<c>roleId</c>), the code of the reason (<c>PASSWORD_TOO_COMMON</c>) and its
/// text in the language of the request. Never the value that was sent.
/// </summary>
public sealed record ProblemError(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Pointer,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Parameter,
    string Code,
    string Detail);
