using System.Globalization;

namespace ZenGear.Api.Extensions;

/// <summary>
/// The <c>ETag</c> of an editable resource is its <c>xmin</c> as a strong tag, <c>"123"</c>, and updates send it back
/// in <c>If-Match</c> (docs/API_CONVENTIONS.md, Concurrency and idempotency).
/// </summary>
public static class ETagExtensions
{
    public static void SetETag(this HttpResponse response, uint version) =>
        response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// Reads an <c>If-Match</c> value: <c>*</c> matches any current version (<paramref name="version"/> null, RFC 9110
    /// §13.1.1), <c>"123"</c> gives 123. A weak tag, a list or anything else is false: it can never match, so the
    /// update gets 412.
    /// </summary>
    public static bool TryParseIfMatch(string ifMatch, out uint? version)
    {
        ArgumentNullException.ThrowIfNull(ifMatch);
        version = null;
        var value = ifMatch.Trim();
        if (value == "*")
        {
            return true;
        }

        if (value is ['"', .. var digits, '"']
            && uint.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            version = parsed;
            return true;
        }

        return false;
    }
}
