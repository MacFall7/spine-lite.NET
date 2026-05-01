using System.Text.Json.Serialization;

namespace M87.Spine.Models;

public sealed record ManifestEntry(
    [property: JsonPropertyName("plugin")] string Plugin,
    [property: JsonPropertyName("function")] string Function,
    [property: JsonPropertyName("effect_class")] EffectClass EffectClass,
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("max_calls_per_session")] int? MaxCallsPerSession = null);
