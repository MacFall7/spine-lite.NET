using M87.Spine;
using M87.Spine.Models;

namespace M87.Spine.Tests;

public class PolicyEvaluatorTests
{
    private static readonly PolicyEvaluator Evaluator = new();

    [Fact]
    public void Test11_NormalPosture_ApprovesAllowedShellSafe()
    {
        var entry = TestSupport.Entry("Fs", "Read", EffectClass.ShellSafe, true);
        var r = Evaluator.Evaluate(entry, EffectClass.ShellSafe, Posture.Normal);
        Assert.Equal(Decision.Approve, r.Decision);
        Assert.Null(r.DenyReason);
    }

    [Fact]
    public void Test12_NormalPosture_DeniesDisallowedShellDangerous()
    {
        var entry = TestSupport.Entry("Fs", "Delete", EffectClass.ShellDangerous, false, "destructive");
        var r = Evaluator.Evaluate(entry, EffectClass.ShellDangerous, Posture.Normal);
        Assert.Equal(Decision.Deny, r.Decision);
        Assert.Equal("destructive", r.DenyReason);
    }

    [Fact]
    public void Test13_ElevatedPosture_DeniesAllNetworkAttempt_RegardlessOfManifest()
    {
        var entry = TestSupport.Entry("Net", "Get", EffectClass.NetworkAttempt, true);
        var r = Evaluator.Evaluate(entry, EffectClass.NetworkAttempt, Posture.Elevated);
        Assert.Equal(Decision.Deny, r.Decision);
        Assert.Equal("ELEVATED_BLOCKS_NETWORK", r.DenyReason);
    }

    [Fact]
    public void Test14_LockdownPosture_DeniesEverythingExceptShellSafeOnAllowed()
    {
        var safeEntry = TestSupport.Entry("Fs", "Read", EffectClass.ShellSafe, true);
        var writeEntry = TestSupport.Entry("Fs", "Write", EffectClass.ScopedWrite, true);

        var safeResult = Evaluator.Evaluate(safeEntry, EffectClass.ShellSafe, Posture.Lockdown);
        var writeResult = Evaluator.Evaluate(writeEntry, EffectClass.ScopedWrite, Posture.Lockdown);

        Assert.Equal(Decision.Approve, safeResult.Decision);
        Assert.Equal(Decision.Deny, writeResult.Decision);
        Assert.Equal("LOCKDOWN_ALLOWS_SHELL_SAFE_ONLY", writeResult.DenyReason);
    }
}
