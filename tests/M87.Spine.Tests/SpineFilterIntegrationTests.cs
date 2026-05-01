using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using M87.Spine;
using M87.Spine.Configuration;
using M87.Spine.Models;

namespace M87.Spine.Tests;

public class SpineFilterIntegrationTests
{
    public sealed class DemoPlugin
    {
        public int CallCount { get; private set; }

        [KernelFunction]
        [Description("Read a fake file.")]
        public string ReadFile(string path)
        {
            CallCount++;
            return $"contents of {path}";
        }

        [KernelFunction]
        [Description("Delete a fake file.")]
        public string DeleteFile(string path)
        {
            CallCount++;
            return $"deleted {path}";
        }

        [KernelFunction]
        [Description("Mystery function — unclassified.")]
        public string Mystery()
        {
            CallCount++;
            return "mystery";
        }
    }

    private sealed class ThrowingClassifier : EffectClassifier
    {
        public ThrowingClassifier(ManifestGate gate) : base(gate) { }
        public override EffectClass Classify(string plugin, string function)
            => throw new InvalidOperationException("simulated classifier failure");
    }

    private static (Kernel kernel, DemoPlugin demo, ReceiptEmitter emitter, string logPath) BuildKernel(
        Posture posture = Posture.Normal,
        bool injectThrowingClassifier = false,
        bool excludeMystery = false,
        bool excludeRead = false)
    {
        var entries = new System.Collections.Generic.List<ManifestEntry>
        {
            TestSupport.Entry(nameof(DemoPlugin), nameof(DemoPlugin.DeleteFile), EffectClass.ShellDangerous, allowed: false, reason: "destructive"),
        };
        if (!excludeRead)
        {
            entries.Add(TestSupport.Entry(nameof(DemoPlugin), nameof(DemoPlugin.ReadFile), EffectClass.ShellSafe, allowed: true));
        }
        if (!excludeMystery)
        {
            entries.Add(TestSupport.Entry(nameof(DemoPlugin), nameof(DemoPlugin.Mystery), EffectClass.ShellSafe, allowed: true));
        }

        var json = TestSupport.BuildManifestJson("1.0.0", posture, entries);
        var gate = ManifestGate.LoadFromJson(json);
        var classifier = injectThrowingClassifier ? new ThrowingClassifier(gate) : new EffectClassifier(gate);
        var evaluator = new PolicyEvaluator();

        var logPath = TestSupport.TempReceiptLogPath();
        var emitter = new ReceiptEmitter(logPath, "session-int");

        var options = new SpineOptions
        {
            ManifestPath = "(in-memory)",
            ReceiptLogPath = logPath,
            Posture = posture,
            ExecutorType = "semantic_kernel",
            ExecutorModel = "test",
        };

        var filter = new SpineFilter(gate, classifier, evaluator, emitter, options);
        var demo = new DemoPlugin();

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IFunctionInvocationFilter>(filter);
        var kernel = builder.Build();
        kernel.Plugins.AddFromObject(demo, nameof(DemoPlugin));

        return (kernel, demo, emitter, logPath);
    }

    private static void Cleanup(string logPath)
    {
        if (File.Exists(logPath)) File.Delete(logPath);
    }

    [Fact]
    public async Task Test18_SkInvokesAllowedFunction_FilterApproves_ReceiptEmitted_FunctionExecutes()
    {
        var (kernel, demo, emitter, logPath) = BuildKernel();
        try
        {
            var fn = kernel.Plugins[nameof(DemoPlugin)][nameof(DemoPlugin.ReadFile)];
            var result = await kernel.InvokeAsync(fn, new KernelArguments { ["path"] = "x.txt" });

            Assert.Equal("contents of x.txt", result.GetValue<string>());
            Assert.Equal(1, demo.CallCount);
            Assert.NotNull(emitter.LastReceiptHash);
            Assert.True(File.Exists(logPath));
            Assert.True(ReceiptEmitter.VerifyChain(logPath, out _));
        }
        finally { emitter.Dispose(); Cleanup(logPath); }
    }

    [Fact]
    public async Task Test19_SkInvokesDeniedFunction_FilterThrows_ReceiptEmitted_FunctionNeverExecutes()
    {
        var (kernel, demo, emitter, logPath) = BuildKernel();
        try
        {
            var fn = kernel.Plugins[nameof(DemoPlugin)][nameof(DemoPlugin.DeleteFile)];

            // SK wraps filter exceptions in KernelFunctionCanceledException-style; assert the inner is GovernanceVetoException.
            var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
                await kernel.InvokeAsync(fn, new KernelArguments { ["path"] = "x.txt" }));

            var veto = ex as GovernanceVetoException ?? ex.InnerException as GovernanceVetoException;
            Assert.NotNull(veto);
            Assert.Equal("destructive", veto!.DenyReason);
            Assert.Equal(0, demo.CallCount);
            Assert.True(ReceiptEmitter.VerifyChain(logPath, out _));
        }
        finally { emitter.Dispose(); Cleanup(logPath); }
    }

    [Fact]
    public async Task Test20_UndeclaredFunction_DeniesFailClosed_ManifestGate()
    {
        var (kernel, demo, emitter, logPath) = BuildKernel(excludeMystery: true);
        try
        {
            var fn = kernel.Plugins[nameof(DemoPlugin)][nameof(DemoPlugin.Mystery)];

            var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await kernel.InvokeAsync(fn));
            var veto = ex as GovernanceVetoException ?? ex.InnerException as GovernanceVetoException;
            Assert.NotNull(veto);
            Assert.Equal("TOOL_NOT_IN_MANIFEST", veto!.DenyReason);
            Assert.Equal(0, demo.CallCount);
        }
        finally { emitter.Dispose(); Cleanup(logPath); }
    }

    [Fact]
    public async Task Test21_ClassifierException_Denies_WithDenyReasonPopulated()
    {
        var (kernel, demo, emitter, logPath) = BuildKernel(injectThrowingClassifier: true);
        try
        {
            var fn = kernel.Plugins[nameof(DemoPlugin)][nameof(DemoPlugin.ReadFile)];
            var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
                await kernel.InvokeAsync(fn, new KernelArguments { ["path"] = "x.txt" }));
            var veto = ex as GovernanceVetoException ?? ex.InnerException as GovernanceVetoException;
            Assert.NotNull(veto);
            Assert.StartsWith("EVALUATION_EXCEPTION", veto!.DenyReason);
            Assert.Contains("simulated classifier failure", veto.DenyReason);
            Assert.Equal(0, demo.CallCount);
        }
        finally { emitter.Dispose(); Cleanup(logPath); }
    }
}
