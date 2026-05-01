#if EMIT_SAMPLE_MANIFEST
using System.IO;
using M87.Spine.Models;
using Xunit.Abstractions;

namespace M87.Spine.Tests;

// Run only when the EMIT_SAMPLE_MANIFEST symbol is defined. Generates samples/BasicSemanticKernelHost/manifest.json.
public class SampleManifestEmitter
{
    private readonly ITestOutputHelper _output;
    public SampleManifestEmitter(ITestOutputHelper output) { _output = output; }

    [Fact]
    public void EmitSampleManifest()
    {
        var json = TestSupport.BuildManifestJson("1.0.0", Posture.Normal, new[]
        {
            TestSupport.Entry("SafeFileReader", "ReadDoc", EffectClass.ShellSafe, allowed: true),
            TestSupport.Entry("DangerousFileDeleter", "DeletePath", EffectClass.ShellDangerous, allowed: false, reason: "destructive"),
        });

        var samplePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "BasicSemanticKernelHost", "manifest.json"));
        File.WriteAllText(samplePath, json);
        _output.WriteLine($"wrote {samplePath}");
    }
}
#endif
