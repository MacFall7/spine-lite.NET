using M87.Spine;
using M87.Spine.Models;

namespace M87.Spine.Tests;

public class EffectClassifierTests
{
    private static EffectClassifier ClassifierWith(params ManifestEntry[] entries)
    {
        var json = TestSupport.BuildManifestJson("1.0.0", Posture.Normal, entries);
        var gate = ManifestGate.LoadFromJson(json);
        return new EffectClassifier(gate);
    }

    [Fact]
    public void Test05_ClassifiesShellSafe_ForKnownReadOnlyFunction()
    {
        var c = ClassifierWith(TestSupport.Entry("Fs", "Read", EffectClass.ShellSafe, true));
        Assert.Equal(EffectClass.ShellSafe, c.Classify("Fs", "Read"));
    }

    [Fact]
    public void Test06_ClassifiesScopedWrite_ForWriteFunction()
    {
        var c = ClassifierWith(TestSupport.Entry("Fs", "Write", EffectClass.ScopedWrite, true));
        Assert.Equal(EffectClass.ScopedWrite, c.Classify("Fs", "Write"));
    }

    [Fact]
    public void Test07_ClassifiesNetworkAttempt_ForNetworkFunction()
    {
        var c = ClassifierWith(TestSupport.Entry("Net", "Get", EffectClass.NetworkAttempt, true));
        Assert.Equal(EffectClass.NetworkAttempt, c.Classify("Net", "Get"));
    }

    [Fact]
    public void Test08_ClassifiesShellDangerous_ForDestructiveFunction()
    {
        var c = ClassifierWith(TestSupport.Entry("Fs", "Delete", EffectClass.ShellDangerous, false, "destructive"));
        Assert.Equal(EffectClass.ShellDangerous, c.Classify("Fs", "Delete"));
    }

    [Fact]
    public void Test09_ClassifiesShellMutating_ForPrivilegeOrStateChange()
    {
        var c = ClassifierWith(TestSupport.Entry("Fs", "Mkdir", EffectClass.ShellMutating, true));
        Assert.Equal(EffectClass.ShellMutating, c.Classify("Fs", "Mkdir"));
    }

    [Fact]
    public void Test10_ReturnsUnknown_ForUnclassifiableFunction_FailClosed()
    {
        var c = ClassifierWith(TestSupport.Entry("Fs", "Read", EffectClass.ShellSafe, true));
        Assert.Equal(EffectClass.Unknown, c.Classify("Ghost", "Missing"));
    }
}
