using System;
using M87.Spine.Models;

namespace M87.Spine;

/// <summary>
/// Thrown by <see cref="SpineFilter"/> after a DENY receipt is written.
/// Immutable. Carries the persisted receipt for caller inspection.
/// </summary>
public sealed class GovernanceVetoException : Exception
{
    public Receipt Receipt { get; }
    public string DenyReason { get; }

    public GovernanceVetoException(Receipt receipt, string denyReason)
        : base($"Governance veto: {denyReason} (receipt {receipt.ReceiptId}, sequence {receipt.SequenceNumber})")
    {
        Receipt = receipt;
        DenyReason = denyReason;
    }
}
