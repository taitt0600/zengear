using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// The JSON Pointer of a field of the request body, as a URI fragment (RFC 9457 example, DEC-117 (1)):
/// <c>RegularHours[1].Day</c> (FluentValidation) and <c>$.regularHours[1].day</c> (model binding) both give
/// <c>#/regularHours/1/day</c>; the body itself is <see cref="Root"/>.
/// </summary>
public static partial class ProblemPointers
{
    public const string Root = "#";

    public static string FromPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = Segment().Matches(path.TrimStart('$')).Select(segment => segment.Groups["name"].Value is { Length: > 0 } name
            ? JsonNamingPolicy.CamelCase.ConvertName(name)
            : segment.Groups["index"].Value);
        return string.Concat(segments.Select(segment => "/" + segment).Prepend(Root));
    }

    /// <summary>A property name, or the index of a list: <c>RegularHours</c>, <c>[1]</c>, <c>Day</c>.</summary>
    [GeneratedRegex(@"(?<name>[^.\[\]]+)|\[(?<index>\d+)\]", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex Segment();
}
