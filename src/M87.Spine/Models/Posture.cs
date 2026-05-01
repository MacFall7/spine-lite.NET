using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace M87.Spine.Models;

/// <summary>
/// Session security posture. Wire strings: NORMAL / ELEVATED / LOCKDOWN.
/// </summary>
[JsonConverter(typeof(PostureJsonConverter))]
public enum Posture
{
    Normal,
    Elevated,
    Lockdown,
}

internal sealed class PostureJsonConverter : JsonConverter<Posture>
{
    public override Posture Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string for Posture, got {reader.TokenType}.");
        }
        var value = reader.GetString();
        return value switch
        {
            "NORMAL" => Posture.Normal,
            "ELEVATED" => Posture.Elevated,
            "LOCKDOWN" => Posture.Lockdown,
            _ => throw new JsonException($"Unknown posture wire value: '{value}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, Posture value, JsonSerializerOptions options)
    {
        var wire = value switch
        {
            Posture.Normal => "NORMAL",
            Posture.Elevated => "ELEVATED",
            Posture.Lockdown => "LOCKDOWN",
            _ => throw new JsonException($"Unmapped Posture: {value}."),
        };
        writer.WriteStringValue(wire);
    }
}
