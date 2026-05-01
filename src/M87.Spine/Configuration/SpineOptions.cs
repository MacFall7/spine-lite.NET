using M87.Spine.Models;

namespace M87.Spine.Configuration;

/// <summary>
/// Construction-time configuration for the Spine filter pipeline.
/// </summary>
public sealed class SpineOptions
{
    public required string ManifestPath { get; init; }
    public required string ReceiptLogPath { get; init; }
    public required Posture Posture { get; init; }
    public string SessionId { get; init; } = System.Guid.NewGuid().ToString("N").Substring(0, 12);
    public string ExecutorType { get; init; } = "semantic_kernel";
    public string ExecutorModel { get; init; } = "unknown";
}
