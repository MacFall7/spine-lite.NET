using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using M87.Spine.Models;

namespace M87.Spine.Internal;

/// <summary>
/// Receipt canonicalization byte-faithful to Spine Lite v0.1.0 Python implementation.
///
/// Rules (CANONICALIZATION.md §2):
///   1. Exclude receipt_hash and previous_receipt_hash from hash input.
///   2. Sort keys alphabetically at every level (recursive).
///   3. No whitespace. Separators are exactly "," and ":".
///   4. UTF-8 encoding.
///   5. No trailing newline.
///   6. Numbers: 0.0 != 0; preserve raw text from parsed JSON.
///   7. Null fields are present in the hash input.
/// </summary>
internal static class ReceiptCanonicalizer
{
    private static readonly HashSet<string> HashExcludedFields = new(StringComparer.Ordinal)
    {
        "receipt_hash",
        "previous_receipt_hash",
    };

    /// <summary>Canonical JSON of a full receipt (hash fields included). Used for self-hash check + persistence.</summary>
    public static string CanonicalJson(Receipt receipt)
    {
        var json = JsonSerializer.Serialize(receipt, SpineJson.Options);
        using var doc = JsonDocument.Parse(json);
        return CanonicalizeElement(doc.RootElement, excludeHashFields: false);
    }

    /// <summary>Canonical JSON suitable for SHA-256 hashing (hash fields excluded).</summary>
    public static string HashableJson(Receipt receipt)
    {
        var json = JsonSerializer.Serialize(receipt, SpineJson.Options);
        using var doc = JsonDocument.Parse(json);
        return CanonicalizeElement(doc.RootElement, excludeHashFields: true);
    }

    /// <summary>SHA-256 lowercase hex of the canonical hashable form of <paramref name="receipt"/>.</summary>
    public static string ComputeHash(Receipt receipt)
        => HexHash(HashableJson(receipt));

    /// <summary>SHA-256 lowercase hex of the canonical hashable form of a raw receipt JSON document.</summary>
    public static string ComputeHash(string receiptJson)
    {
        using var doc = JsonDocument.Parse(receiptJson);
        var canonical = CanonicalizeElement(doc.RootElement, excludeHashFields: true);
        return HexHash(canonical);
    }

    private static string HexHash(string canonical)
    {
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string CanonicalizeElement(JsonElement root, bool excludeHashFields)
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
            WriteSorted(writer, root, excludeHashFields, isTopLevel: true);
            writer.Flush();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSorted(Utf8JsonWriter writer, JsonElement element, bool excludeHashFields, bool isTopLevel)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var props = element.EnumerateObject()
                    .Where(p => !(isTopLevel && excludeHashFields && HashExcludedFields.Contains(p.Name)))
                    .OrderBy(p => p.Name, StringComparer.Ordinal);
                foreach (var prop in props)
                {
                    writer.WritePropertyName(prop.Name);
                    WriteSorted(writer, prop.Value, excludeHashFields, isTopLevel: false);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteSorted(writer, item, excludeHashFields, isTopLevel: false);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                // Preserve exact source representation (0.0 vs 0).
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

            case JsonValueKind.Undefined:
            default:
                throw new InvalidOperationException($"Unexpected JsonValueKind: {element.ValueKind}");
        }
    }
}
