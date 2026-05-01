using System;
using System.IO;
using System.Threading.Tasks;
using M87.Spine;
using M87.Spine.Models;

namespace M87.Spine.Tests;

public class ReceiptEmitterTests
{
    private static (Executor, ActionRecord, ResultRecord, BudgetSnapshot, GitContext) Demo()
    {
        var executor = new Executor("semantic_kernel", "unknown", Guid.NewGuid().ToString());
        var action = new ActionRecord(
            "semantic_kernel", "function_invoke", EffectClass.ShellSafe, 0.0, "demo", null,
            Array.Empty<string>(), "REVERSIBLE");
        var result = new ResultRecord("success", 0, null, null,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null);
        var budget = new BudgetSnapshot(0, 0, 0, 0, 0, 0.0, 0.0, Posture.Normal);
        var git = new GitContext("master", null, null);
        return (executor, action, result, budget, git);
    }

    [Fact]
    public async Task Test15_FirstReceiptHasNullPreviousReceiptHash()
    {
        var path = TestSupport.TempReceiptLogPath();
        try
        {
            using var emitter = new ReceiptEmitter(path, "session-15");
            var (e, a, r, b, g) = Demo();
            var receipt = await emitter.EmitAsync("p-1", e, a, r, b, g);
            Assert.Null(receipt.PreviousReceiptHash);
            Assert.Equal(1, receipt.SequenceNumber);
            Assert.Equal(64, receipt.ReceiptHash.Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Test16_SecondReceiptPreviousHashEqualsFirstHash()
    {
        var path = TestSupport.TempReceiptLogPath();
        try
        {
            using var emitter = new ReceiptEmitter(path, "session-16");
            var (e, a, r, b, g) = Demo();
            var first = await emitter.EmitAsync("p-1", e, a, r, b, g);
            var second = await emitter.EmitAsync("p-2", e, a, r, b, g);
            Assert.Equal(first.ReceiptHash, second.PreviousReceiptHash);
            Assert.Equal(2, second.SequenceNumber);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Test17_TamperedReceiptLogFailsChainVerification()
    {
        var path = TestSupport.TempReceiptLogPath();
        try
        {
            using (var emitter = new ReceiptEmitter(path, "session-17"))
            {
                var (e, a, r, b, g) = Demo();
                await emitter.EmitAsync("p-1", e, a, r, b, g);
                await emitter.EmitAsync("p-2", e, a, r, b, g);
            }

            Assert.True(ReceiptEmitter.VerifyChain(path, out _));

            // Flip one byte inside an action description; chain self-hash must fail.
            var contents = File.ReadAllText(path);
            File.WriteAllText(path, contents.Replace("\"description\":\"demo\"", "\"description\":\"hack\""));

            Assert.False(ReceiptEmitter.VerifyChain(path, out var reason));
            Assert.NotNull(reason);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
