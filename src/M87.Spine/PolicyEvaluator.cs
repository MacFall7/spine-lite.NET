using M87.Spine.Models;

namespace M87.Spine;

/// <summary>
/// Posture-aware allow/deny decision. Stateless. Pure function of (entry, effectClass, posture).
/// </summary>
/// <remarks>
/// Posture matrix (BLUEPRINT §6 tests 11-14):
///   NORMAL    : honor manifest's allowed flag.
///   ELEVATED  : deny all NETWORK_ATTEMPT regardless of manifest; otherwise honor manifest.
///   LOCKDOWN  : deny everything except SHELL_SAFE on allowed entries.
/// Always-deny inputs (precede the matrix):
///   - Unknown function (entry == null)
///   - EffectClass.Unknown (classification failure)
///   - RestrictedWrite (always blocked)
/// </remarks>
public sealed class PolicyEvaluator
{
    public PolicyResult Evaluate(ManifestEntry? entry, EffectClass effectClass, Posture posture)
    {
        if (entry is null)
        {
            return new PolicyResult(Decision.Deny, "TOOL_NOT_IN_MANIFEST");
        }

        if (effectClass == EffectClass.Unknown)
        {
            return new PolicyResult(Decision.Deny, "EFFECT_CLASS_UNKNOWN");
        }

        if (effectClass == EffectClass.RestrictedWrite)
        {
            return new PolicyResult(Decision.Deny, "RESTRICTED_WRITE_FORBIDDEN");
        }

        if (!entry.Allowed)
        {
            return new PolicyResult(Decision.Deny, entry.Reason ?? "MANIFEST_DENIED");
        }

        return posture switch
        {
            Posture.Normal => new PolicyResult(Decision.Approve, null),

            Posture.Elevated => effectClass == EffectClass.NetworkAttempt
                ? new PolicyResult(Decision.Deny, "ELEVATED_BLOCKS_NETWORK")
                : new PolicyResult(Decision.Approve, null),

            Posture.Lockdown => effectClass == EffectClass.ShellSafe
                ? new PolicyResult(Decision.Approve, null)
                : new PolicyResult(Decision.Deny, "LOCKDOWN_ALLOWS_SHELL_SAFE_ONLY"),

            _ => new PolicyResult(Decision.Deny, "UNKNOWN_POSTURE"),
        };
    }
}
