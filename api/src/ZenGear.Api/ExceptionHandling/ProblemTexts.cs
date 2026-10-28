using System.Globalization;
using Microsoft.Extensions.Localization;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// The texts of the problems the API writes, in the UI culture of the request (DEC-117 (3)), from
/// <c>Resources/ExceptionHandling.ProblemTexts.&lt;culture&gt;.resx</c>: <c>&lt;type&gt;.title</c> and
/// <c>&lt;type&gt;.detail</c> for a problem type such as <c>validation</c>. A missing text is logged and replaced by the
/// one of <see cref="FallbackType"/>, so a type without a text never turns the answer into a 500.
/// </summary>
public sealed partial class ProblemTexts(IStringLocalizer<ProblemTexts> localizer, ILogger<ProblemTexts> logger)
{
    public const string FallbackType = "unexpected-error";

    public string Title(string problemType) => Text(problemType, "title");

    public string Detail(string problemType) => Text(problemType, "detail");

    private string Text(string problemType, string part)
    {
        var text = localizer[$"{problemType}.{part}"];
        if (!text.ResourceNotFound)
        {
            return text.Value;
        }

        LogMissingText(logger, text.Name, CultureInfo.CurrentUICulture.Name);
        return localizer[$"{FallbackType}.{part}"].Value;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No problem text {Key} for the culture {Culture}.")]
    private static partial void LogMissingText(ILogger logger, string key, string culture);
}
