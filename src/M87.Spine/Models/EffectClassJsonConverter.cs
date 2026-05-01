using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace M87.Spine.Models;

internal sealed class EffectClassJsonConverter : JsonConverter<EffectClass>
{
    public override EffectClass Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string for EffectClass, got {reader.TokenType}.");
        }

        var value = reader.GetString();
        return value switch
        {
            "SHELL_SAFE" => EffectClass.ShellSafe,
            "SHELL_MUTATING" => EffectClass.ShellMutating,
            "SHELL_DANGEROUS" => EffectClass.ShellDangerous,
            "NETWORK_ATTEMPT" => EffectClass.NetworkAttempt,
            "SCOPED_WRITE" => EffectClass.ScopedWrite,
            "RESTRICTED_WRITE" => EffectClass.RestrictedWrite,
            _ => throw new JsonException($"Unknown effect_class wire value: '{value}'."),
        };
    }

    public override void Write(Utf8JsonWriter writer, EffectClass value, JsonSerializerOptions options)
    {
        var wire = value switch
        {
            EffectClass.ShellSafe => "SHELL_SAFE",
            EffectClass.ShellMutating => "SHELL_MUTATING",
            EffectClass.ShellDangerous => "SHELL_DANGEROUS",
            EffectClass.NetworkAttempt => "NETWORK_ATTEMPT",
            EffectClass.ScopedWrite => "SCOPED_WRITE",
            EffectClass.RestrictedWrite => "RESTRICTED_WRITE",
            EffectClass.Unknown => throw new JsonException("EffectClass.Unknown is a sentinel and must not be serialized."),
            _ => throw new JsonException($"Unmapped EffectClass: {value}."),
        };
        writer.WriteStringValue(wire);
    }
}
