using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace M87.Spine.Internal;

/// <summary>
/// Forces integral doubles (e.g. 0.0, 1.0) to serialize with an explicit decimal point so Python's
/// repr(float) and System.Text.Json agree byte-for-byte. Non-integral doubles use shortest round-trip ("R").
/// </summary>
internal sealed class CanonicalDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDouble();

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new JsonException("Receipt fields cannot be NaN or Infinity.");
        }

        if (value == Math.Truncate(value) && !double.IsNegative(value) || value == Math.Truncate(value))
        {
            // Integral float: emit as "<int>.0" matching Python repr(0.0) == '0.0'.
            var integral = value.ToString("F1", CultureInfo.InvariantCulture);
            writer.WriteRawValue(integral, skipInputValidation: false);
            return;
        }

        var formatted = value.ToString("R", CultureInfo.InvariantCulture);
        writer.WriteRawValue(formatted, skipInputValidation: false);
    }
}
