using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace ZenGear.Api.ExceptionHandling;

/// <summary>
/// The texts of the problems the API writes, in the UI culture of the request (DEC-117 (3)), from
/// <c>Resources/ExceptionHandling.ProblemTexts.&lt;culture&gt;.resx</c>: <c>&lt;type&gt;.title</c> and
/// <c>&lt;type&gt;.detail</c> for a problem type such as <c>validation</c>, and the code itself for an item of
/// <c>errors</c> (<c>NAME_TOO_LONG</c>). A missing text is logged and replaced by the one of <see cref="FallbackType"/>,
/// so a type or code without a text never turns the answer into a 500.
/// </summary>
public sealed partial class ProblemTexts(IStringLocalizer<ProblemTexts> localizer, ILogger<ProblemTexts> logger)
{
    public const string FallbackType = "unexpected-error";

    public string Title(string problemType) => Text($"{problemType}.title", $"{FallbackType}.title");

    public string Detail(string problemType) => Text($"{problemType}.detail", $"{FallbackType}.detail");

    /// <summary>
    /// The text of an <c>errors</c> item, its <c>{Name}</c> placeholders filled from <paramref name="values"/> (the
    /// limits a validator gives, never the value sent); numbers in the format of the UI culture (21.474.836).
    /// </summary>
    public string ErrorDetail(string code, IReadOnlyDictionary<string, object>? values = null)
    {
        var text = Text(code, $"{FallbackType}.detail");
        return values is null ? text : Placeholder().Replace(text, match =>
            values.TryGetValue(match.Groups["name"].Value, out var value) ? Format(value) : match.Value);
    }

    private static string Format(object value) => value switch
    {
        int or long or short => ((IFormattable)value).ToString("N0", CultureInfo.CurrentUICulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentUICulture),
        _ => value.ToString() ?? "",
    };

    private string Text(string key, string fallbackKey)
    {
        var text = localizer[key];
        if (!text.ResourceNotFound)
        {
            return text.Value;
        }

        LogMissingText(logger, key, CultureInfo.CurrentUICulture.Name);
        return localizer[fallbackKey].Value;
    }

    [GeneratedRegex(@"\{(?<name>[A-Za-z]+)\}", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex Placeholder();

    [LoggerMessage(Level = LogLevel.Error, Message = "No problem text {Key} for the culture {Culture}.")]
    private static partial void LogMissingText(ILogger logger, string key, string culture);
}
