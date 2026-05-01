using System.IO;
using M87.Spine;
using M87.Spine.Models;

namespace M87.Spine.Tests;

public class ManifestGateTests
{
    [Fact]
    public void Test01_LoadsValidManifest_ComputesHash_ExposesLookup()
    {
        var json = TestSupport.BuildManifestJson("1.0.0", Posture.Normal, new[]
        {
            TestSupport.Entry("FsPlugin", "Read", EffectClass.ShellSafe, allowed: true),
        });

        var gate = ManifestGate.LoadFromJson(json);
        Assert.NotNull(gate);
        Assert.Equal(64, gate.ManifestHash.Length);
        Assert.NotNull(gate.Lookup("FsPlugin", "Read"));
    }

    [Fact]
    public void Test02_RejectsManifestMissingVersion_FailClosed()
    {
        var bad = "{\"posture\":\"NORMAL\",\"functions\":[],\"manifest_hash\":\"deadbeef\"}";
        var ex = Assert.Throws<System.InvalidOperationException>(() => ManifestGate.LoadFromJson(bad));
        Assert.Contains("version", ex.Message);
    }

    [Fact]
    public void Test03_RejectsManifestWithHashMismatchOnReload()
    {
        var json = TestSupport.BuildManifestJson("1.0.0", Posture.Normal, new[]
        {
            TestSupport.Entry("FsPlugin", "Read", EffectClass.ShellSafe, allowed: true),
        });

        // Tamper with one field after the hash was computed.
        var tampered = json.Replace("\"FsPlugin\"", "\"OtherPlugin\"");
        var ex = Assert.Throws<System.InvalidOperationException>(() => ManifestGate.LoadFromJson(tampered));
        Assert.Contains("hash mismatch", ex.Message);
    }

    [Fact]
    public void Test04_ReturnsNullForUndeclaredFunction()
    {
        var json = TestSupport.BuildManifestJson("1.0.0", Posture.Normal, new[]
        {
            TestSupport.Entry("FsPlugin", "Read", EffectClass.ShellSafe, allowed: true),
        });
        var gate = ManifestGate.LoadFromJson(json);
        Assert.Null(gate.Lookup("Ghost", "Missing"));
    }
}
