using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZenGear.Data.Common;

/// <summary>
/// Writes a <see cref="TimeOnly"/> as <c>"HH:mm"</c>, the shape of the opening hours stored in
/// <c>business_locations.regular_hours</c> and <c>special_hours</c> (docs/DATABASE.md); seconds are dropped. Reads any
/// invariant time (<c>"8:30"</c>, <c>"08:30:00"</c>) to the minute, so a row filled by hand at go-live still loads.
/// </summary>
public sealed class HourMinuteTimeJsonConverter : JsonConverter<TimeOnly>
{
    public const string Format = "HH:mm";

    /// <summary>
    /// The options of the jsonb columns holding opening hours (<see cref="JsonListConverter{T}"/>): camelCase, null
    /// values left out, times as <c>"HH:mm"</c>, dates as <c>"yyyy-MM-dd"</c>.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonbOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new HourMinuteTimeJsonConverter() },
    };

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var time = TimeOnly.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
        return new TimeOnly(time.Hour, time.Minute);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
