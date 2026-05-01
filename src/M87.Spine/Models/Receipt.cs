using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace M87.Spine.Models;

public sealed record Executor(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("instance_id")] string InstanceId);

public sealed record ActionRecord(
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("effect_class")] EffectClass EffectClass,
    [property: JsonPropertyName("risk_delta")] double RiskDelta,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("command")] string? Command,
    [property: JsonPropertyName("target_paths")] IReadOnlyList<string> TargetPaths,
    [property: JsonPropertyName("reversibility")] string Reversibility);

public sealed record ResultRecord(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("exit_code")] int? ExitCode,
    [property: JsonPropertyName("blocked_by")] string? BlockedBy,
    [property: JsonPropertyName("diff_hash")] string? DiffHash,
    [property: JsonPropertyName("files_created")] IReadOnlyList<string> FilesCreated,
    [property: JsonPropertyName("files_modified")] IReadOnlyList<string> FilesModified,
    [property: JsonPropertyName("files_deleted")] IReadOnlyList<string> FilesDeleted,
    [property: JsonPropertyName("stdout_truncated")] string? StdoutTruncated);

public sealed record BudgetSnapshot(
    [property: JsonPropertyName("steps_used")] int StepsUsed,
    [property: JsonPropertyName("steps_remaining")] int StepsRemaining,
    [property: JsonPropertyName("commands_used")] int CommandsUsed,
    [property: JsonPropertyName("writes_used")] int WritesUsed,
    [property: JsonPropertyName("files_touched")] int FilesTouched,
    [property: JsonPropertyName("runtime_elapsed_seconds")] double RuntimeElapsedSeconds,
    [property: JsonPropertyName("session_risk_score")] double SessionRiskScore,
    [property: JsonPropertyName("current_posture")] Posture CurrentPosture);

public sealed record GitContext(
    [property: JsonPropertyName("branch")] string Branch,
    [property: JsonPropertyName("commit_before")] string? CommitBefore,
    [property: JsonPropertyName("commit_after")] string? CommitAfter);

public sealed record Receipt(
    [property: JsonPropertyName("receipt_id")] string ReceiptId,
    [property: JsonPropertyName("session_id")] string SessionId,
    [property: JsonPropertyName("proposal_id")] string ProposalId,
    [property: JsonPropertyName("sequence_number")] int SequenceNumber,
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("executor")] Executor Executor,
    [property: JsonPropertyName("action")] ActionRecord Action,
    [property: JsonPropertyName("result")] ResultRecord Result,
    [property: JsonPropertyName("budget_snapshot")] BudgetSnapshot BudgetSnapshot,
    [property: JsonPropertyName("git_context")] GitContext GitContext,
    [property: JsonPropertyName("previous_receipt_hash")] string? PreviousReceiptHash,
    [property: JsonPropertyName("receipt_hash")] string ReceiptHash);
