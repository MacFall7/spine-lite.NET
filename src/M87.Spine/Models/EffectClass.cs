using System.Text.Json.Serialization;

namespace M87.Spine.Models;

/// <summary>
/// Six wire-faithful classes plus a fail-closed Unknown sentinel.
/// Wire strings (SCREAMING_SNAKE_CASE) are fixed by Spine Lite v0.1.0 receipt interop.
/// Unknown is never serialized; classifier output that triggers DENY.
/// </summary>
[JsonConverter(typeof(EffectClassJsonConverter))]
public enum EffectClass
{
    ShellSafe,
    ShellMutating,
    ShellDangerous,
    NetworkAttempt,
    ScopedWrite,
    RestrictedWrite,
    Unknown,
}
