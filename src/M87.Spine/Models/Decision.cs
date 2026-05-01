namespace M87.Spine.Models;

/// <summary>
/// Outcome of policy evaluation. APPROVE allows the function to invoke; DENY throws GovernanceVetoException.
/// </summary>
public enum Decision
{
    Approve,
    Deny,
}

/// <summary>
/// Policy evaluation result paired with the deny reason on DENY (null on APPROVE).
/// </summary>
public sealed record PolicyResult(Decision Decision, string? DenyReason);
