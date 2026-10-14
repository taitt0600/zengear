using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;

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
    /// The version an update sends in <c>If-Match</c> (<see cref="TryParseIfMatch"/>); when it sends none, the 428
    /// <c>/problems/if-match-required</c> naming <paramref name="readPath"/>, the <c>GET</c> that gives the tag; when it
    /// sends a tag that can never match, 412.
    /// </summary>
    public static bool TryReadIfMatch(this ControllerBase controller, string? ifMatch, string readPath, out uint? version,
        [NotNullWhen(false)] out ActionResult? refusal)
    {
        ArgumentNullException.ThrowIfNull(controller);
        version = null;
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            refusal = controller.Problem(statusCode: StatusCodes.Status428PreconditionRequired, type: "/problems/if-match-required",
                title: "If-Match required", detail: $"Send the ETag of GET {readPath} in the If-Match header.");
            return false;
        }

        if (!TryParseIfMatch(ifMatch, out version))
        {
            refusal = controller.Problem(statusCode: StatusCodes.Status412PreconditionFailed);
            return false;
        }

        refusal = null;
        return true;
    }

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
