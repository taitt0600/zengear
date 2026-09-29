using Serilog.Core;
using Serilog.Events;

namespace ZenGear.Api.Logging;

/// <summary>
/// Safety net that replaces the value of a top-level log property whose name looks like a secret
/// (token, password, OTP, secret, authorization header, API key) with <c>***</c>.
///
/// Rule for every log call: never destructure (<c>{@Dto}</c>) a DTO or object that holds a secret
/// or personal data (docs/DATABASE.md, Conventions → Personal data); log ids and harmless values only.
/// This enricher cannot see inside destructured objects.
/// </summary>
public sealed class SensitivePropertyMasker : ILogEventEnricher
{
    public const string Mask = "***";

    private static readonly string[] SensitiveNameParts = ["token", "password", "otp", "secret", "authorization", "apikey"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var sensitive = logEvent.Properties.Keys.Where(IsSensitive).ToList();
        foreach (var name in sensitive)
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(Mask)));
        }
    }

    internal static bool IsSensitive(string propertyName) =>
        SensitiveNameParts.Any(part => propertyName.Contains(part, StringComparison.OrdinalIgnoreCase));
}
