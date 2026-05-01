using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using M87.Spine.Configuration;
using M87.Spine.Models;

namespace M87.Spine;

/// <summary>
/// Semantic Kernel <see cref="IFunctionInvocationFilter"/> that enforces Spine Lite governance.
/// Pipeline (BLUEPRINT §3.1): ManifestGate -> EffectClassifier -> PolicyEvaluator -> ReceiptEmitter -> APPROVE/DENY.
/// </summary>
public sealed class SpineFilter : IFunctionInvocationFilter
{
    private readonly ManifestGate _gate;
    private readonly EffectClassifier _classifier;
    private readonly PolicyEvaluator _evaluator;
    private readonly ReceiptEmitter _emitter;
    private readonly SpineOptions _options;

    public SpineFilter(
        ManifestGate gate,
        EffectClassifier classifier,
        PolicyEvaluator evaluator,
        ReceiptEmitter emitter,
        SpineOptions options)
    {
        _gate = gate;
        _classifier = classifier;
        _evaluator = evaluator;
        _emitter = emitter;
        _options = options;
    }

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var plugin = context.Function.PluginName ?? "(no_plugin)";
        var function = context.Function.Name;
        var proposalId = $"{plugin}.{function}.{Guid.NewGuid():N}";

        ManifestEntry? entry;
        EffectClass effectClass;
        PolicyResult result;

        try
        {
            entry = _gate.Lookup(plugin, function);
            effectClass = _classifier.Classify(plugin, function);
            result = _evaluator.Evaluate(entry, effectClass, _options.Posture);
        }
        catch (Exception ex)
        {
            var denyReason = $"EVALUATION_EXCEPTION: {ex.GetType().Name}: {ex.Message}";
            var failReceipt = await EmitDenyAsync(plugin, function, proposalId, EffectClass.Unknown, denyReason).ConfigureAwait(false);
            throw new GovernanceVetoException(failReceipt, denyReason);
        }

        if (result.Decision == Decision.Deny)
        {
            var receipt = await EmitDenyAsync(plugin, function, proposalId, effectClass, result.DenyReason ?? "DENIED").ConfigureAwait(false);
            throw new GovernanceVetoException(receipt, result.DenyReason ?? "DENIED");
        }

        // APPROVE: emit receipt before invocation per CLAUDE.md no-partial-receipts rule.
        await EmitApproveAsync(plugin, function, proposalId, effectClass).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }

    private Task<Receipt> EmitApproveAsync(string plugin, string function, string proposalId, EffectClass effectClass)
    {
        var action = new ActionRecord(
            Tool: "semantic_kernel",
            Operation: "function_invoke",
            EffectClass: effectClass,
            RiskDelta: 0.0,
            Description: $"{plugin}.{function}",
            Command: null,
            TargetPaths: Array.Empty<string>(),
            Reversibility: "REVERSIBLE");

        var resultRecord = new ResultRecord(
            Status: "success",
            ExitCode: 0,
            BlockedBy: null,
            DiffHash: null,
            FilesCreated: Array.Empty<string>(),
            FilesModified: Array.Empty<string>(),
            FilesDeleted: Array.Empty<string>(),
            StdoutTruncated: null);

        return EmitAsync(proposalId, action, resultRecord);
    }

    private Task<Receipt> EmitDenyAsync(string plugin, string function, string proposalId, EffectClass effectClass, string reason)
    {
        var action = new ActionRecord(
            Tool: "semantic_kernel",
            Operation: "function_invoke",
            EffectClass: effectClass == EffectClass.Unknown ? EffectClass.ShellDangerous : effectClass,
            RiskDelta: 0.0,
            Description: $"BLOCKED: {plugin}.{function}",
            Command: null,
            TargetPaths: Array.Empty<string>(),
            Reversibility: "REVERSIBLE");

        var resultRecord = new ResultRecord(
            Status: "blocked",
            ExitCode: 1,
            BlockedBy: reason,
            DiffHash: null,
            FilesCreated: Array.Empty<string>(),
            FilesModified: Array.Empty<string>(),
            FilesDeleted: Array.Empty<string>(),
            StdoutTruncated: null);

        return EmitAsync(proposalId, action, resultRecord);
    }

    private Task<Receipt> EmitAsync(string proposalId, ActionRecord action, ResultRecord result)
    {
        var executor = new Executor(_options.ExecutorType, _options.ExecutorModel, Guid.NewGuid().ToString());
        var budget = new BudgetSnapshot(0, 0, 0, 0, 0, 0.0, 0.0, _options.Posture);
        var git = new GitContext("master", null, null);
        return _emitter.EmitAsync(proposalId, executor, action, result, budget, git);
    }
}
