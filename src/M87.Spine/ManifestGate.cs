using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using M87.Spine.Internal;
using M87.Spine.Models;

namespace M87.Spine;

/// <summary>
/// Loads the manifest, verifies its self-hash, and exposes O(1) function lookup.
/// Read-only after construction. Safe to share across threads.
/// Per CLAUDE.md: "Authority lives in the manifest, not in descriptions."
/// </summary>
public sealed class ManifestGate
{
    private readonly Dictionary<(string Plugin, string Function), ManifestEntry> _index;

    public Manifest Manifest { get; }

    /// <summary>SHA-256 (lowercase hex) of the manifest body excluding the manifest_hash field itself.</summary>
    public string ManifestHash { get; }

    private ManifestGate(Manifest manifest, string manifestHash)
    {
        Manifest = manifest;
        ManifestHash = manifestHash;
        _index = manifest.Functions.ToDictionary(
            entry => (entry.Plugin, entry.Function),
            entry => entry);
    }

    /// <summary>Loads and verifies a manifest. Throws on missing version, missing manifest_hash, or hash mismatch.</summary>
    public static ManifestGate Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Manifest not found: {path}", path);
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json);
    }

    /// <summary>JSON-string overload, primarily for tests.</summary>
    internal static ManifestGate LoadFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Manifest is missing a string 'version' field.");
        }

        if (!root.TryGetProperty("manifest_hash", out var storedHashEl) || storedHashEl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Manifest is missing a string 'manifest_hash' field.");
        }

        var storedHash = storedHashEl.GetString()!;
        var computedHash = ComputeManifestHash(root);

        if (!string.Equals(storedHash, computedHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Manifest hash mismatch. Stored={storedHash}, Computed={computedHash}.");
        }

        var manifest = JsonSerializer.Deserialize<Manifest>(json, SpineJson.Options)
            ?? throw new InvalidOperationException("Manifest deserialized to null.");

        return new ManifestGate(manifest, computedHash);
    }

    /// <summary>O(1) lookup. Returns null if the function is not declared.</summary>
    public ManifestEntry? Lookup(string plugin, string function)
        => _index.TryGetValue((plugin, function), out var entry) ? entry : null;

    /// <summary>
    /// SHA-256 (lowercase hex) of canonical-JSON of the manifest with `manifest_hash` excluded at the top level.
    /// Mirrors the receipt canonicalization pattern.
    /// </summary>
    public static string ComputeManifestHash(JsonElement root)
    {
        using var stream = new MemoryStream();
        var writerOptions = new JsonWriterOptions
        {
            Indented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            SkipValidation = false,
        };

        using (var writer = new Utf8JsonWriter(stream, writerOptions))
        {
            WriteSorted(writer, root, isTopLevel: true);
            writer.Flush();
        }

        var canonical = stream.ToArray();
        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }

    private static void WriteSorted(Utf8JsonWriter writer, JsonElement element, bool isTopLevel)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var props = element.EnumerateObject()
                    .Where(p => !(isTopLevel && string.Equals(p.Name, "manifest_hash", StringComparison.Ordinal)))
                    .OrderBy(p => p.Name, StringComparer.Ordinal);
                foreach (var prop in props)
                {
                    writer.WritePropertyName(prop.Name);
                    WriteSorted(writer, prop.Value, isTopLevel: false);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteSorted(writer, item, isTopLevel: false);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unexpected JsonValueKind: {element.ValueKind}");
        }
    }
}
