using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace M87.Spine.Internal;

internal static class SpineJson
{
    /// <summary>
    /// Shared JsonSerializerOptions for receipt and manifest serialization.
    /// - Nulls written explicitly (Python parity: "command":null appears in canonical form).
    /// - UnsafeRelaxedJsonEscaping matches Python json.dumps default escaping for ASCII content.
    /// - CanonicalDoubleConverter preserves 0.0 vs 0 distinction.
    /// </summary>
    public static readonly JsonSerializerOptions Options = BuildOptions();

    /// <summary>
    /// Indented variant for human-readable receipt log output. Hash is computed from the
    /// canonicalized form regardless of on-disk indentation.
    /// </summary>
    public static readonly JsonSerializerOptions IndentedOptions = BuildOptions(indented: true);

    private static JsonSerializerOptions BuildOptions(bool indented = false)
    {
        var opts = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = indented,
        };
        opts.Converters.Add(new CanonicalDoubleConverter());
        return opts;
    }
}
