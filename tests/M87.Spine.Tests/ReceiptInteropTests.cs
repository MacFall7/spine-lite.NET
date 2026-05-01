using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Json.Schema;
using M87.Spine;
using M87.Spine.Internal;
using M87.Spine.Models;

namespace M87.Spine.Tests;

public class ReceiptInteropTests
{
    private static string FixtureLogPath => Path.Combine(AppContext.BaseDirectory, "fixtures", "spine_lite_receipt_sample.jsonl");
    private static string SchemaPath => Path.Combine(AppContext.BaseDirectory, "fixtures", "receipt.schema.json");

    [Fact]
    public void Test22_ReadsSpineLitePythonReceiptSample_VerifiesChain()
    {
        Assert.True(File.Exists(FixtureLogPath));

        Assert.True(ReceiptEmitter.VerifyChain(FixtureLogPath, out var failureReason),
            $"Chain verification failed: {failureReason}");

        // Belt-and-braces: also confirm deserialization round-trips through the .NET Receipt record losslessly.
        var lines = File.ReadAllLines(FixtureLogPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        Assert.Equal(5, lines.Length);

        Receipt? prior = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var receipt = JsonSerializer.Deserialize<Receipt>(lines[i], SpineJson.Options);
            Assert.NotNull(receipt);

            if (i == 0)
            {
                Assert.Null(receipt!.PreviousReceiptHash);
            }
            else
            {
                Assert.Equal(prior!.ReceiptHash, receipt!.PreviousReceiptHash);
            }

            var rehash = ReceiptCanonicalizer.ComputeHash(lines[i]);
            Assert.Equal(receipt.ReceiptHash, rehash);

            prior = receipt;
        }
    }

    [Fact]
    public async Task Test23_DotNetEmittedReceipt_ValidatesAgainstSpineLiteSchema_AndRoundTripsCanonically()
    {
        Assert.True(File.Exists(SchemaPath));

        var logPath = TestSupport.TempReceiptLogPath();
        try
        {
            using var emitter = new ReceiptEmitter(logPath, "session-23");

            var executor = new Executor("semantic_kernel", "test", Guid.NewGuid().ToString());
            var action = new ActionRecord(
                "semantic_kernel", "function_invoke", EffectClass.ShellSafe, 0.0,
                "round-trip test", null, Array.Empty<string>(), "REVERSIBLE");
            var result = new ResultRecord("success", 0, null, null,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null);
            var budget = new BudgetSnapshot(1, 19, 1, 0, 0, 0.0, 0.0, Posture.Normal);
            var git = new GitContext("master", null, null);

            var receipt = await emitter.EmitAsync("p-23", executor, action, result, budget, git);

            var json = ReceiptCanonicalizer.CanonicalJson(receipt);

            // (1) Lossless deserialization
            var roundTripped = JsonSerializer.Deserialize<Receipt>(json, SpineJson.Options);
            Assert.NotNull(roundTripped);
            Assert.Equal(receipt.ReceiptHash, roundTripped!.ReceiptHash);

            // (2) Recomputed hash matches stored hash (canonical pipeline self-consistent)
            Assert.Equal(receipt.ReceiptHash, ReceiptCanonicalizer.ComputeHash(json));

            // (3) Schema validation against Spine Lite v0.1.0 receipt.schema.json
            var schemaText = File.ReadAllText(SchemaPath);
            var schema = JsonSchema.FromText(schemaText);
            using var doc = JsonDocument.Parse(json);
            var evalResults = schema.Evaluate(doc.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List,
            });
            Assert.True(evalResults.IsValid,
                $"Schema validation failed: {JsonSerializer.Serialize(evalResults)}");
        }
        finally
        {
            if (File.Exists(logPath)) File.Delete(logPath);
        }
    }
}
