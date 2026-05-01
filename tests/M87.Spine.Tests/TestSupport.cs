using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using M87.Spine;
using M87.Spine.Internal;
using M87.Spine.Models;

namespace M87.Spine.Tests;

internal static class TestSupport
{
    /// <summary>
    /// Builds a manifest JSON document with manifest_hash filled in from the canonical content hash.
    /// Returns the JSON string; the caller can write it to disk or pass to LoadFromJson directly.
    /// </summary>
    public static string BuildManifestJson(string version, Posture posture, IEnumerable<ManifestEntry> entries)
    {
        var skeleton = new
        {
            version,
            posture = posture.ToString().ToUpperInvariant(),
            functions = entries,
            manifest_hash = "PLACEHOLDER",
        };
        var withPlaceholder = JsonSerializer.Serialize(skeleton, SpineJson.Options);

        using var doc = JsonDocument.Parse(withPlaceholder);
        var hash = ManifestGate.ComputeManifestHash(doc.RootElement);

        var sealed_ = new
        {
            version,
            posture = posture.ToString().ToUpperInvariant(),
            functions = entries,
            manifest_hash = hash,
        };
        return JsonSerializer.Serialize(sealed_, SpineJson.Options);
    }

    public static string WriteManifestToTempFile(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"spine-manifest-{System.Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    public static string TempReceiptLogPath()
        => Path.Combine(Path.GetTempPath(), $"spine-receipts-{System.Guid.NewGuid():N}.jsonl");

    public static ManifestEntry Entry(string plugin, string function, EffectClass effectClass, bool allowed, string? reason = null)
        => new(plugin, function, effectClass, allowed, reason);
}
