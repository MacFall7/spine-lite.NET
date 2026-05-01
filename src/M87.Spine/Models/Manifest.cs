using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace M87.Spine.Models;

public sealed record Manifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("posture")] Posture Posture,
    [property: JsonPropertyName("functions")] IReadOnlyList<ManifestEntry> Functions);
