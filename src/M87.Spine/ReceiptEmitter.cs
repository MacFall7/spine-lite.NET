using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using M87.Spine.Internal;
using M87.Spine.Models;

namespace M87.Spine;

/// <summary>
/// Append-only JSONL receipt log, SHA-256 chained.
/// Writes are serialized via SemaphoreSlim so a singleton-registered filter is thread-safe.
/// </summary>
public sealed class ReceiptEmitter : IDisposable
{
    private readonly string _logPath;
    private readonly string _sessionId;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private string? _previousReceiptHash;
    private int _sequenceNumber;
    private bool _disposed;

    public ReceiptEmitter(string logPath, string sessionId)
    {
        _logPath = logPath ?? throw new ArgumentNullException(nameof(logPath));
        _sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        var dir = Path.GetDirectoryName(Path.GetFullPath(logPath));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _previousReceiptHash = null;
        _sequenceNumber = 0;
    }

    /// <summary>Last receipt hash written (null until the first emit).</summary>
    public string? LastReceiptHash => _previousReceiptHash;

    /// <summary>
    /// Builds, hashes, and appends a receipt for the given action+result, returning the persisted Receipt.
    /// Throws if the underlying file write fails — fail-closed: caller must abort the invocation on throw.
    /// </summary>
    public async Task<Receipt> EmitAsync(
        string proposalId,
        Executor executor,
        ActionRecord action,
        ResultRecord result,
        BudgetSnapshot budget,
        GitContext git,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sequence = ++_sequenceNumber;
            var receiptId = Guid.NewGuid().ToString();
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffffzzz", System.Globalization.CultureInfo.InvariantCulture);

            var draft = new Receipt(
                ReceiptId: receiptId,
                SessionId: _sessionId,
                ProposalId: proposalId,
                SequenceNumber: sequence,
                Timestamp: timestamp,
                Executor: executor,
                Action: action,
                Result: result,
                BudgetSnapshot: budget,
                GitContext: git,
                PreviousReceiptHash: _previousReceiptHash,
                ReceiptHash: string.Empty);

            var hash = ReceiptCanonicalizer.ComputeHash(draft);
            var sealed_ = draft with { ReceiptHash = hash };

            var line = ReceiptCanonicalizer.CanonicalJson(sealed_);
            await AppendLineAsync(line, cancellationToken).ConfigureAwait(false);

            _previousReceiptHash = hash;
            return sealed_;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task AppendLineAsync(string line, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        using var stream = new FileStream(
            _logPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Verifies an existing JSONL receipt log: genesis null, chain links, self-hash. Returns true on success.</summary>
    public static bool VerifyChain(string logPath, out string? failureReason)
    {
        failureReason = null;
        if (!File.Exists(logPath))
        {
            failureReason = $"Log file not found: {logPath}";
            return false;
        }

        var lines = File.ReadAllLines(logPath);
        string? prior = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var stored = root.GetProperty("receipt_hash").GetString();
            var prevField = root.GetProperty("previous_receipt_hash");
            var storedPrev = prevField.ValueKind == JsonValueKind.Null ? null : prevField.GetString();

            if (i == 0 && storedPrev != null)
            {
                failureReason = "Genesis receipt has non-null previous_receipt_hash.";
                return false;
            }

            if (i > 0 && !string.Equals(storedPrev, prior, StringComparison.Ordinal))
            {
                failureReason = $"Chain break at sequence {i + 1}: previous_receipt_hash != prior receipt's hash.";
                return false;
            }

            var computed = ReceiptCanonicalizer.ComputeHash(line);
            if (!string.Equals(computed, stored, StringComparison.Ordinal))
            {
                failureReason = $"Self-hash mismatch at sequence {i + 1}: stored={stored}, computed={computed}.";
                return false;
            }

            prior = stored;
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _writeLock.Dispose();
        _disposed = true;
    }
}
