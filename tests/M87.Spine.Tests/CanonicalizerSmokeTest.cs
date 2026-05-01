using System.IO;
using System.Text.Json;
using M87.Spine.Internal;

namespace M87.Spine.Tests;

/// <summary>
/// Pre-suite smoke test. Verifies the canonicalizer matches Python receipt hashes BEFORE the full suite
/// is built. If this fails, every receipt-emitter test downstream is meaningless.
/// </summary>
public class CanonicalizerSmokeTest
{
    [Fact]
    public void EveryFixtureReceipt_RecomputedHashEqualsEmbeddedReceiptHash()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "spine_lite_receipt_sample.jsonl");
        Assert.True(File.Exists(path), $"Fixture not found: {path}");

        var lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        Assert.Equal(5, lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            using var doc = JsonDocument.Parse(lines[i]);
            var embedded = doc.RootElement.GetProperty("receipt_hash").GetString();
            Assert.NotNull(embedded);

            var computed = ReceiptCanonicalizer.ComputeHash(lines[i]);
            Assert.Equal(embedded, computed);
        }
    }
}
